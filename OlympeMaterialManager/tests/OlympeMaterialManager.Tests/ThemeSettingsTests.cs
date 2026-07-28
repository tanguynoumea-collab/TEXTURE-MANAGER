using System.IO;
using Olympe.MaterialManager.Models;
using Olympe.MaterialManager.Services;
using Xunit;

namespace Olympe.MaterialManager.Tests;

/// <summary>
/// Tests du socle du jeu de couleurs (cycle 4), calqués sur PreviewModeSettingsTests :
/// round-trip du thème via settings.json, tolérance aux valeurs inconnues (jamais de
/// quarantaine pour un simple thème invalide) et compatibilité avec les fichiers
/// existants sans le champ Theme (ajout additif, pas de bump de SchemaVersion).
/// Chaque test travaille dans un répertoire temporaire isolé. Aucun type Revit.
/// </summary>
public class ThemeSettingsTests : IDisposable
{
    private readonly string _dir;
    private readonly PresetService _service;

    static ThemeSettingsTests() => WpfResourceHost.EnsurePackSchemeRegistered();

    public ThemeSettingsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "OlympeTests-" + Guid.NewGuid().ToString("N"));
        _service = new PresetService(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private string SettingsPath => Path.Combine(_dir, "settings.json");

    // ---- Round-trip ----

    [Fact]
    public void Store_ChangementDeTheme_PersisteImmediatement_EtRechargeIdentique()
    {
        var store = new ThemeStore(_service);
        Assert.Equal(AppTheme.Dark, store.CurrentTheme);

        store.CurrentTheme = AppTheme.Light;

        // Persistance immédiate : le fichier contient la valeur STRING
        var raw = File.ReadAllText(SettingsPath);
        Assert.Contains("\"theme\": \"Light\"", raw);

        // Un nouveau store (nouvelle session) recharge le même thème
        var reloaded = new ThemeStore(new PresetService(_dir));
        Assert.Equal(AppTheme.Light, reloaded.CurrentTheme);
    }

    [Fact]
    public void SaveSettings_DefautSerialise_Dark()
    {
        _service.SaveSettings(new AppSettingsDto());
        var raw = File.ReadAllText(SettingsPath);
        Assert.Contains("\"theme\": \"Dark\"", raw);
    }

    [Fact]
    public void Store_RetourAuThemeSombre_SePersisteAussi()
    {
        var store = new ThemeStore(_service);
        store.CurrentTheme = AppTheme.Light;
        store.CurrentTheme = AppTheme.Dark;

        Assert.Contains("\"theme\": \"Dark\"", File.ReadAllText(SettingsPath));
        Assert.Equal(AppTheme.Dark, new ThemeStore(new PresetService(_dir)).CurrentTheme);
    }

    // ---- Valeur inconnue → défaut, sans quarantaine ----

    [Theory]
    [InlineData("Fantaisie")]
    [InlineData("")]
    [InlineData("999")] // numérique hors plage : TryParse accepterait, le pattern match rejette
    public void Store_ValeurInconnue_RetombeSurDark_SansQuarantaine(string value)
    {
        File.WriteAllText(SettingsPath,
            $"{{ \"schemaVersion\": 1, \"presetFiles\": [], \"theme\": \"{value}\" }}");

        var store = new ThemeStore(_service);

        Assert.Equal(AppTheme.Dark, store.CurrentTheme);
        // Le fichier reste en place : un thème inconnu n'est PAS une corruption
        Assert.True(File.Exists(SettingsPath));
        Assert.Empty(Directory.GetFiles(_dir, "settings.json.corrupt-*"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Fantaisie")]
    [InlineData("Sombre")] // libellé français : ce n'est pas la valeur persistée
    public void Parse_ValeurInvalide_RetourneDark(string? value)
    {
        Assert.Equal(AppTheme.Dark, ThemeStore.Parse(value));
    }

    [Theory]
    [InlineData("Dark", AppTheme.Dark)]
    [InlineData("Light", AppTheme.Light)]
    [InlineData("light", AppTheme.Light)] // tolérance de casse
    [InlineData("DARK", AppTheme.Dark)]
    public void Parse_ValeurValide_RetourneLeTheme(string value, AppTheme expected)
    {
        Assert.Equal(expected, ThemeStore.Parse(value));
    }

    // ---- Champ absent (fichier existant) → défaut ----

    [Fact]
    public void Store_FichierSansChampTheme_RetombeSurDark()
    {
        File.WriteAllText(SettingsPath,
            "{ \"schemaVersion\": 1, \"presetFiles\": [\"Preset par defaut\"], \"activePresetName\": \"Preset par defaut\" }");

        var store = new ThemeStore(_service);

        Assert.Equal(AppTheme.Dark, store.CurrentTheme);
    }

    [Fact]
    public void LoadSettings_FichierSansChampTheme_DeserialiseAvecDefaut()
    {
        File.WriteAllText(SettingsPath, "{ \"schemaVersion\": 1, \"presetFiles\": [] }");

        var settings = _service.LoadSettings();

        Assert.Equal("Dark", settings.Theme);
        // Ajout additif : la version de schema ne bouge pas
        Assert.Equal(1, settings.SchemaVersion);
    }

    // ---- Application de la palette sur un hôte (mécanique de la bascule) ----

    /// <summary>
    /// Cœur de la bascule à chaud : un hôte enregistré voit ses pinceaux changer
    /// de valeur sans être recréé. Reproduit ce que fait une fenêtre Olympe —
    /// OlympeTheme.xaml fusionné (qui apporte ThemeDark), puis la palette
    /// superposée par le store. Le rendu réel dans Revit reste à valider à la main.
    /// </summary>
    [Fact]
    public void RegisterHost_PuisBascule_ChangeLaValeurDesPinceauxDeLHote()
    {
        var host = new System.Windows.ResourceDictionary();
        host.MergedDictionaries.Add(new System.Windows.ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/OlympeMaterialManager;component/Themes/OlympeTheme.xaml")
        });

        var store = new ThemeStore(_service);
        ThemeStore.RegisterHost(host);
        try
        {
            var sombre = (System.Windows.Media.SolidColorBrush)host["BackgroundBrush"];
            Assert.Equal(System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x1A), sombre.Color);

            store.CurrentTheme = AppTheme.Light;

            var clair = (System.Windows.Media.SolidColorBrush)host["BackgroundBrush"];
            Assert.Equal(System.Windows.Media.Color.FromRgb(0xEF, 0xEF, 0xEF), clair.Color);

            // Retour arrière : la palette claire est bien retirée, pas empilée
            store.CurrentTheme = AppTheme.Dark;
            var retour = (System.Windows.Media.SolidColorBrush)host["BackgroundBrush"];
            Assert.Equal(System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x1A), retour.Color);

            // Une seule palette superposée en plus d'OlympeTheme, quel que soit
            // le nombre de bascules : la liste ne doit pas croître.
            Assert.Equal(2, host.MergedDictionaries.Count);
        }
        finally
        {
            ThemeStore.UnregisterHost(host);
        }
    }

    /// <summary>
    /// Une fenêtre créée APRÈS une bascule (un dialogue, typiquement) doit
    /// s'ouvrir directement au thème courant, pas au thème par défaut.
    /// </summary>
    [Fact]
    public void RegisterHost_ApresBascule_AppliqueLeThemeCourantAlOuverture()
    {
        var store = new ThemeStore(_service);
        store.CurrentTheme = AppTheme.Light;

        var tardif = new System.Windows.ResourceDictionary();
        tardif.MergedDictionaries.Add(new System.Windows.ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/OlympeMaterialManager;component/Themes/OlympeTheme.xaml")
        });

        ThemeStore.RegisterHost(tardif);
        try
        {
            var brush = (System.Windows.Media.SolidColorBrush)tardif["BackgroundBrush"];
            Assert.Equal(System.Windows.Media.Color.FromRgb(0xEF, 0xEF, 0xEF), brush.Color);
        }
        finally
        {
            ThemeStore.UnregisterHost(tardif);
            store.CurrentTheme = AppTheme.Dark;
        }
    }

    /// <summary>
    /// Le thème et le mode d'aperçu partagent le même fichier : écrire l'un ne
    /// doit pas écraser l'autre (les deux stores relisent avant d'écrire).
    /// </summary>
    [Fact]
    public void Store_BasculeDeTheme_NEcrasePasLeModeApercu()
    {
        var previewStore = new PreviewModeStore(_service);
        previewStore.CurrentMode = PreviewMode.Realistic;

        var themeStore = new ThemeStore(_service);
        themeStore.CurrentTheme = AppTheme.Light;

        var settings = _service.LoadSettings();
        Assert.Equal("Realistic", settings.MaterialPreviewMode);
        Assert.Equal("Light", settings.Theme);
    }
}
