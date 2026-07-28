using System.Collections;
using System.Windows;
using System.Windows.Media;
using Xunit;

namespace Olympe.MaterialManager.Tests;

/// <summary>
/// Contrat des deux jeux de couleurs commutables (cycle 4) : ThemeDark.xaml et
/// ThemeLight.xaml doivent exposer EXACTEMENT les mêmes x:Key. Une clé présente
/// d'un seul côté produirait, après bascule, un pinceau introuvable — c'est-à-dire
/// un élément d'interface invisible ou resté à la couleur de l'autre thème, sans
/// erreur au build (DynamicResource ne casse pas la compilation).
/// Ces tests chargent les dictionnaires réels depuis l'assembly.
/// </summary>
public class ThemeDictionaryTests
{
    private const string DarkUri =
        "pack://application:,,,/OlympeMaterialManager;component/Themes/ThemeDark.xaml";
    private const string LightUri =
        "pack://application:,,,/OlympeMaterialManager;component/Themes/ThemeLight.xaml";

    static ThemeDictionaryTests()
    {
        // Hors process WPF hôte, le schéma « pack:// » n'est pas enregistré tant
        // que rien n'a touché System.IO.Packaging : sans cela, ResourceDictionary
        // .Source lève « The URI prefix is not recognized ».
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
        System.Windows.Application.ResourceAssembly ??= typeof(Services.PresetService).Assembly;
    }

    private static ResourceDictionary Load(string uri) =>
        new() { Source = new Uri(uri) };

    private static SortedSet<string> KeysOf(ResourceDictionary dict)
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in dict)
        {
            if (entry.Key is string key) keys.Add(key);
        }
        return keys;
    }

    [Fact]
    public void LesDeuxThemes_ExposentExactementLesMemesCles()
    {
        var dark = KeysOf(Load(DarkUri));
        var light = KeysOf(Load(LightUri));

        Assert.NotEmpty(dark);
        Assert.Equal(string.Join(",", dark), string.Join(",", light));
    }

    [Theory]
    [InlineData(DarkUri)]
    [InlineData(LightUri)]
    public void ChaqueCleBrush_ResoutUnSolidColorBrush(string uri)
    {
        var dict = Load(uri);
        var brushKeys = KeysOf(dict).Where(k => k.EndsWith("Brush", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(brushKeys);
        foreach (var key in brushKeys)
        {
            Assert.IsType<SolidColorBrush>(dict[key]);
        }
    }

    [Theory]
    [InlineData(DarkUri)]
    [InlineData(LightUri)]
    public void ChaqueCleColor_APourPendantUnBrushDeMemePrefixe(string uri)
    {
        var dict = Load(uri);
        var keys = KeysOf(dict);

        foreach (var colorKey in keys.Where(k => k.EndsWith("Color", StringComparison.Ordinal)))
        {
            var brushKey = colorKey[..^"Color".Length] + "Brush";
            Assert.True(keys.Contains(brushKey), $"Le token {colorKey} n'a pas de pinceau {brushKey}.");
        }
    }
}
