using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Olympe.MaterialManager.Helpers;

/// <summary>
/// Résolution des chemins de texture lus dans les assets d'apparence Revit (B10-TX).
/// Les valeurs brutes peuvent contenir plusieurs chemins séparés par « | »
/// (premier existant retenu) et des chemins relatifs à la bibliothèque de
/// matériaux Autodesk. Conçu comme une fonction qui a le DROIT d'échouer :
/// introuvable → null, JAMAIS d'exception (le fallback couleur est le chemin nominal).
/// Pur .NET (aucun type Revit) pour rester testable en xunit.
/// </summary>
public static class TexturePathResolver
{
    /// <summary>
    /// Cache par session : valeur brute → chemin résolu (ou null si introuvable).
    /// Évite de re-sonder le disque à chaque rafraîchissement de couches/pastilles.
    /// </summary>
    private static readonly ConcurrentDictionary<string, string?> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Racines de recherche calculées une seule fois par session.
    /// </summary>
    private static readonly Lazy<IReadOnlyList<string>> _defaultRoots = new(BuildSearchRoots);

    /// <summary>
    /// Résout une valeur brute d'asset vers un fichier image existant, avec cache.
    /// </summary>
    public static string? Resolve(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return null;
        return _cache.GetOrAdd(rawValue!, raw => Resolve(raw, _defaultRoots.Value));
    }

    /// <summary>
    /// Résolution sans cache avec racines injectables (testable).
    /// Chemins multiples séparés par « | » : premier segment qui se résout.
    /// </summary>
    public static string? Resolve(string rawValue, IReadOnlyList<string> searchRoots)
    {
        try
        {
            foreach (var segment in rawValue.Split('|'))
            {
                var candidate = segment.Trim();
                if (candidate.Length == 0) continue;

                var resolved = ResolveSingle(candidate, searchRoots);
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
    /// Résout un chemin unique : tel quel s'il est absolu et existe, sinon sondé
    /// contre les racines connues (chemin relatif complet, puis nom de fichier seul
    /// en dernier recours pour les chemins absolus d'une autre machine).
    /// </summary>
    private static string? ResolveSingle(string path, IReadOnlyList<string> searchRoots)
    {
        try
        {
            bool rooted = Path.IsPathRooted(path);
            if (rooted && File.Exists(path)) return path;

            // Chemin relatif : sonder chaque racine dans l'ordre.
            if (!rooted)
            {
                foreach (var root in searchRoots)
                {
                    var probe = Path.Combine(root, path);
                    if (File.Exists(probe)) return probe;
                }
            }

            // Dernier recours : le nom de fichier seul contre les racines
            // (chemin absolu d'une machine d'origine différente).
            var fileName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(fileName) && fileName != path)
            {
                foreach (var root in searchRoots)
                {
                    var probe = Path.Combine(root, fileName);
                    if (File.Exists(probe)) return probe;
                }
            }
        }
        catch
        {
            // Caractères invalides, chemin trop long... : candidat ignoré.
        }
        return null;
    }

    /// <summary>
    /// Construit les racines de recherche, dans l'ordre contractuel :
    /// bibliothèque Autodesk partagée (x86 puis 64 bits, avec les sous-dossiers
    /// 1\Mats, 2\Mats, 3\Mats où la bibliothèque range ses bitmaps), puis les
    /// chemins additionnels déclarés dans Revit.ini.
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
    /// Ajoute &lt;base&gt;\Common Files\Autodesk Shared\Materials\Textures et ses
    /// sous-dossiers 1\Mats, 2\Mats, 3\Mats s'ils existent sur la machine.
    /// </summary>
    private static void AddAutodeskSharedRoots(List<string> roots, string? programFilesDir)
    {
        if (string.IsNullOrEmpty(programFilesDir)) return;

        var textures = Path.Combine(programFilesDir!,
            "Common Files", "Autodesk Shared", "Materials", "Textures");
        AddIfExists(roots, textures);

        foreach (var sub in new[] { "1", "2", "3" })
            AddIfExists(roots, Path.Combine(textures, sub, "Mats"));
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
}
