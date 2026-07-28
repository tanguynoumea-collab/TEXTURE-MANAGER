using Olympe.MaterialManager.Services;

namespace Olympe.MaterialManager.Tests;

/// <summary>
/// Rend les URI « pack:// » utilisables hors process WPF hôte.
/// Sans cela, <c>ResourceDictionary.Source</c> lève « The URI prefix is not
/// recognized » : le schéma pack n'est enregistré que lorsque quelque chose a
/// touché System.IO.Packaging. Appelé depuis le constructeur statique de chaque
/// classe de tests qui charge un dictionnaire — l'ordre d'exécution des classes
/// xunit n'est pas garanti, aucune ne peut compter sur une autre.
/// </summary>
internal static class WpfResourceHost
{
    private static readonly object _lock = new();
    private static bool _ready;

    public static void EnsurePackSchemeRegistered()
    {
        lock (_lock)
        {
            if (_ready) return;
            _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
            System.Windows.Application.ResourceAssembly ??= typeof(PresetService).Assembly;
            _ready = true;
        }
    }
}
