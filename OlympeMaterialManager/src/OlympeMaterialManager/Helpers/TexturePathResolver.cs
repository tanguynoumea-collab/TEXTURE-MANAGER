using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Olympe.MaterialManager.Helpers;

/// <summary>
/// Résolution des chemins de texture lus dans les assets d'apparence Revit
/// (B10-TX, restauré et corrigé en DR4-1). Les valeurs brutes peuvent contenir :
/// un préfixe de bibliothèque « lib:?... » (tout ce qui précède et inclut « ? »
/// est retiré), plusieurs chemins séparés par « | » (premier existant retenu),
/// des séparateurs « / », des chemins relatifs à la bibliothèque de matériaux
/// Autodesk (ex. « Maps\UnifiedBitmap\UnifiedBitmap.png ») et des chemins
/// absolus d'une autre machine (retombée par nom de fichier). Conçu comme une
/// fonction qui a le DROIT d'échouer : introuvable → null, JAMAIS d'exception
/// (le fallback couleur d'apparence est le chemin nominal).
/// Logique pure testable en xunit via la surcharge à racines injectables.
/// </summary>
public static class TexturePathResolver
{
    /// <summary>
    /// Cache par session : valeur brute → chemin résolu (ou null si introuvable).
    /// Évite de re-sonder le disque à chaque rafraîchissement de couches/pastilles.
    /// Un échec n'est mémorisé que lorsque l'index par nom de fichier est prêt :
    /// avant, l'échec peut n'être dû qu'à l'index en construction et sera retenté
    /// au prochain rafraîchissement (spec DR4-1).
    /// </summary>
    private static readonly ConcurrentDictionary<string, string?> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Racines de recherche calculées une seule fois par session (spec DR4-1 :
    /// énumération des racines UNE fois, mise en cache).
    /// </summary>
    private static readonly Lazy<IReadOnlyList<string>> _defaultRoots = new(BuildSearchRoots);

    /// <summary>
    /// Index nom de fichier → chemin complet, construit PARESSEUSEMENT en tâche
    /// de fond au premier besoin (spec DR4-1) : ~19 000 images dans les
    /// bibliothèques Autodesk, l'énumération ne doit jamais bloquer le thread
    /// Revit. Tant que l'index n'est pas prêt, la retombée par nom de fichier
    /// répond null (l'UI retombe sur la couleur d'apparence — le prochain
    /// rafraîchissement/activation en profitera).
    /// </summary>
    private static readonly ConcurrentDictionary<string, string> _fileNameIndex =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>0 = index jamais demandé, 1 = construction lancée (Interlocked).</summary>
    private static int _indexBuildStarted;

    private static volatile bool _indexReady;

    /// <summary>
    /// Vrai quand l'index nom de fichier a fini de se construire. Les caches
    /// (ici et côté bridge) ne mémorisent un ÉCHEC de résolution qu'à partir de
    /// ce moment — avant, l'échec est provisoire.
    /// </summary>
    public static bool IsFileNameIndexReady => _indexReady;

    /// <summary>
    /// Extensions d'image reconnues (alignées sur FindTexturePath côté bridge).
    /// </summary>
    private static readonly string[] _imageExtensions =
        { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };

    /// <summary>
    /// Vrai si le chemin (brut ou résolu) désigne le PLACEHOLDER générique
    /// d'Autodesk (« UnifiedBitmap.png », un carré noir marqué BITMAP) que de
    /// nombreux assets référencent en guise de bouche-trou. Retour terrain DR4 :
    /// le résoudre pollue les aperçus (image noire) et les moyennes de couleur —
    /// il doit être traité comme « pas de texture » (fallback couleur d'apparence).
    /// </summary>
    public static bool IsGenericPlaceholder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = path!.Replace('/', '\\');
        var fileName = normalized.Substring(normalized.LastIndexOf('\\') + 1);
        return string.Equals(fileName, "UnifiedBitmap.png", StringComparison.OrdinalIgnoreCase)
            || normalized.IndexOf(@"Maps\UnifiedBitmap", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Vrai si le nom de fichier trahit une carte technique NON-couleur
    /// (relief/bump, normale, brillance, découpe, motif de relief) — retour
    /// terrain DR4-3 : ces images grises (ex. Simple_Metal_Mtl_Break_pattern.jpg)
    /// polluaient les aperçus quand la marche d'asset les trouvait avant la
    /// texture diffuse. Heuristique par nom, assumée best-effort.
    /// </summary>
    public static bool IsNonColorMap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = path!.Replace('/', '\\');
        var fileName = normalized.Substring(normalized.LastIndexOf('\\') + 1);
        foreach (var marker in new[] { "_pattern", "bump", "_normal", "_gloss", "cutout", "noise" })
        {
            if (fileName.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Résout une valeur brute d'asset vers un fichier image existant, avec cache.
    /// </summary>
    public static string? Resolve(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return null;

        if (_cache.TryGetValue(rawValue!, out var cached)) return cached;

        var resolved = Resolve(rawValue!, _defaultRoots.Value, LookupFileNameIndex);

        // Échec possiblement provisoire (index en construction) : ne pas mémoriser.
        if (resolved != null || _indexReady)
            _cache[rawValue!] = resolved;

        return resolved;
    }

    /// <summary>
    /// Résolution sans cache avec racines injectables (testable).
    /// Chemins multiples séparés par « | » : premier segment qui se résout.
    /// <paramref name="fileNameLookup"/> : retombée par nom de fichier seul
    /// (index machine en production, dictionnaire injecté en test) — null pour
    /// s'en passer, la sonde directe racine\nom reste appliquée.
    /// </summary>
    public static string? Resolve(string rawValue, IReadOnlyList<string> searchRoots,
        Func<string, string?>? fileNameLookup = null)
    {
        try
        {
            foreach (var segment in rawValue.Split('|'))
            {
                var candidate = Normalize(segment);
                if (candidate.Length == 0) continue;

                var resolved = ResolveSingle(candidate, searchRoots, fileNameLookup);
                if (resolved != null) return resolved;
            }
        }
        catch
        {
            // Introuvable / chemin malformé : null, jamais d'exception.
        }
        return null;
    }

    /// <summary>
    /// Normalise un candidat brut (DR4-1) : retrait des espaces, du préfixe de
    /// bibliothèque « lib:?... » — tout ce qui précède et inclut « ? » est
    /// supprimé (observé dans olympe.log : « lib:?Maps\... ») — et conversion
    /// des « / » en « \ ».
    /// </summary>
    private static string Normalize(string segment)
    {
        var s = segment.Trim();
        int question = s.LastIndexOf('?');
        if (question >= 0) s = s.Substring(question + 1);
        return s.Replace('/', '\\').Trim();
    }

    /// <summary>
    /// Résout un chemin unique : tel quel s'il est absolu et existe, sinon sondé
    /// contre les racines connues (chemin relatif complet, puis nom de fichier seul
    /// en dernier recours pour les chemins absolus d'une autre machine).
    /// FIA2-01 (conservé) : les chemins UNC (\\serveur\...) ne sont JAMAIS sondés
    /// tels quels — un File.Exists sur un partage injoignable peut bloquer 1 à 30 s
    /// (timeout SMB) sur le thread Revit. Pour eux, seule la retombée par nom de
    /// fichier s'applique.
    /// </summary>
    private static string? ResolveSingle(string path, IReadOnlyList<string> searchRoots,
        Func<string, string?>? fileNameLookup)
    {
        try
        {
            bool rooted = Path.IsPathRooted(path);
            bool isUnc = path.Length >= 2 && path[0] == '\\' && path[1] == '\\';
            if (rooted && !isUnc && File.Exists(path)) return path;

            // Chemin relatif : sonder chaque racine dans l'ordre.
            if (!rooted)
            {
                foreach (var root in searchRoots)
                {
                    var probe = Path.Combine(root, path);
                    if (File.Exists(probe)) return probe;
                }
            }

            // Dernier recours : le nom de fichier seul (chemin absolu d'une
            // machine d'origine différente, ex. « C:/Users/shenj/... » du log).
            var fileName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(fileName) && fileName != path)
            {
                // Sonde directe racine\nom (couvre les dossiers plats de Revit.ini).
                foreach (var root in searchRoots)
                {
                    var probe = Path.Combine(root, fileName);
                    if (File.Exists(probe)) return probe;
                }

                // Index nom → chemin (les bibliothèques Autodesk sont profondes).
                var indexed = fileNameLookup?.Invoke(fileName);
                if (indexed != null && File.Exists(indexed)) return indexed;
            }
        }
        catch
        {
            // Caractères invalides, chemin trop long... : candidat ignoré.
        }
        return null;
    }

    /// <summary>
    /// Construit les racines de recherche (DR4-1, nouvelle donnée disque) : les
    /// sous-dossiers « assetlibrary_*.fbm » de
    /// &lt;Program Files (x86)&gt;\Common Files\Autodesk Shared\Materials\&lt;version&gt;
    /// ET &lt;Program Files&gt;\...\Materials\&lt;version&gt; — toutes versions, triées
    /// décroissant (la plus récente sondée d'abord) — puis les chemins
    /// additionnels déclarés dans Revit.ini (logique historique conservée).
    /// L'ancien résolveur sondait ...\Materials\Textures\... : mauvaises racines
    /// (diagnostic « 0 texture résolvable » du DR2-3, invalidé par la
    /// vérification disque de DR4).
    /// </summary>
    private static List<string> BuildSearchRoots()
    {
        var roots = new List<string>();
        try
        {
            AddAutodeskSharedRoots(roots,
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
            AddAutodeskSharedRoots(roots,
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            AppendRevitIniRenderPaths(roots);
        }
        catch
        {
            // Best-effort : une liste partielle (voire vide) reste utilisable.
        }
        return roots;
    }

    /// <summary>
    /// Ajoute les sous-dossiers « assetlibrary_*.fbm » de chaque dossier de
    /// version de &lt;base&gt;\Common Files\Autodesk Shared\Materials, versions
    /// triées décroissant (ex. 2023 avant 2022 — la bibliothèque la plus
    /// récente est sondée d'abord).
    /// </summary>
    private static void AddAutodeskSharedRoots(List<string> roots, string? programFilesDir)
    {
        try
        {
            if (string.IsNullOrEmpty(programFilesDir)) return;

            var materials = Path.Combine(programFilesDir!,
                "Common Files", "Autodesk Shared", "Materials");
            if (!Directory.Exists(materials)) return;

            // Retour terrain DR4-3 : les textures DIFFUSE de la bibliothèque
            // (« 1\Mats\Brick_... ») vivent sous Materials\Textures\<n>\Mats,
            // un frère des dossiers de version — sondé en premier (les .fbm ne
            // contiennent surtout que placeholders et cartes de relief).
            AddIfExists(roots, Path.Combine(materials, "Textures"));

            var versionDirs = Directory.GetDirectories(materials)
                .OrderByDescending(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase);

            foreach (var versionDir in versionDirs)
            {
                foreach (var fbm in Directory.GetDirectories(versionDir, "assetlibrary_*.fbm"))
                    AddIfExists(roots, fbm);
            }
        }
        catch
        {
            // Best-effort : dossier inaccessible → ignoré.
        }
    }

    /// <summary>
    /// Lit les chemins additionnels de rendu declares dans Revit.ini
    /// ([Directories] AdditionalRenderAppearancePaths=chemin1|chemin2).
    /// AUCUNE API publique Revit n'expose ce reglage (Options &gt; Rendu &gt;
    /// Chemins supplementaires) : lecture best-effort d'un format non documente,
    /// susceptible de changer entre versions — tout echec est silencieusement ignore.
    /// </summary>
    private static void AppendRevitIniRenderPaths(List<string> roots)
    {
        try
        {
            var revitRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk", "Revit");
            if (!Directory.Exists(revitRoot)) return;

            foreach (var versionDir in Directory.GetDirectories(revitRoot, "Autodesk Revit *"))
            {
                var iniPath = Path.Combine(versionDir, "Revit.ini");
                if (!File.Exists(iniPath)) continue;

                bool inDirectoriesSection = false;
                foreach (var line in File.ReadAllLines(iniPath))
                {
                    var trimmed = line.Trim();
                    // trimmed[0] plutot que StartsWith : net48 n'a pas StartsWith(char)
                    if (trimmed.Length > 0 && trimmed[0] == '[')
                    {
                        inDirectoriesSection = string.Equals(
                            trimmed, "[Directories]", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }
                    if (!inDirectoriesSection) continue;

                    if (trimmed.StartsWith("AdditionalRenderAppearancePaths",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        int eq = trimmed.IndexOf('=');
                        if (eq < 0) continue;
                        foreach (var part in trimmed.Substring(eq + 1).Split('|'))
                        {
                            var dir = part.Trim();
                            if (dir.Length > 0) AddIfExists(roots, dir);
                        }
                    }
                }
            }
        }
        catch
        {
            // Best-effort : Revit.ini absent, verrouille ou illisible → ignore.
        }
    }

    private static void AddIfExists(List<string> roots, string dir)
    {
        try
        {
            if (Directory.Exists(dir) &&
                !roots.Contains(dir, StringComparer.OrdinalIgnoreCase))
            {
                roots.Add(dir);
            }
        }
        catch
        {
            // Best-effort.
        }
    }

    // ---- Index nom de fichier (tâche de fond) ----

    /// <summary>
    /// Retombée par nom de fichier contre l'index machine. Le premier appel
    /// déclenche la construction en tâche de fond (Task.Run) ; tant qu'elle
    /// n'est pas terminée, la réponse est null — jamais d'attente bloquante
    /// sur le thread Revit (spec DR4-1).
    /// </summary>
    private static string? LookupFileNameIndex(string fileName)
    {
        EnsureIndexBuildStarted();
        if (!_indexReady) return null;
        return _fileNameIndex.TryGetValue(fileName, out var path) ? path : null;
    }

    private static void EnsureIndexBuildStarted()
    {
        if (Interlocked.CompareExchange(ref _indexBuildStarted, 1, 0) != 0) return;
        Task.Run(BuildFileNameIndex);
    }

    /// <summary>
    /// Construit l'index nom → chemin en parcourant les racines dans l'ordre
    /// (la plus récente d'abord : TryAdd conserve la première occurrence, donc
    /// la version de bibliothèque la plus récente). Chaque racine est isolée
    /// dans son try/catch : un sous-dossier inaccessible n'abandonne pas les
    /// autres racines.
    /// </summary>
    private static void BuildFileNameIndex()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var root in _defaultRoots.Value)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(
                             root, "*", SearchOption.AllDirectories))
                {
                    if (HasImageExtension(file))
                        _fileNameIndex.TryAdd(Path.GetFileName(file), file);
                }
            }
            catch
            {
                // Best-effort : racine partiellement indexée.
            }
        }
        _indexReady = true;
        try
        {
            Services.LogService.Info(
                $"Index textures: {_fileNameIndex.Count} fichiers en {sw.ElapsedMilliseconds} ms");
        }
        catch
        {
            // Le diagnostic ne doit jamais faire échouer l'index.
        }
    }

    private static bool HasImageExtension(string file)
    {
        foreach (var ext in _imageExtensions)
        {
            if (file.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
