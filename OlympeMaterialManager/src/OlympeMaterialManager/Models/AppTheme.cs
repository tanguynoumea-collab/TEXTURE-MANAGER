namespace Olympe.MaterialManager.Models;

/// <summary>
/// Jeu de couleurs de l'interface (cycle 4). Chaque valeur correspond à un
/// dictionnaire de couleurs commutable à chaud dans Themes/ :
/// ThemeDark.xaml (« Graphite &amp; Sauge ») et ThemeLight.xaml (« Craie &amp; Sauge »).
/// Persisté en STRING dans settings.json (jamais l'enum sérialisé — une valeur
/// inconnue enverrait le fichier entier en quarantaine, DON-02) : voir
/// <see cref="Services.ThemeStore.Parse"/> pour le parse tolérant.
/// </summary>
public enum AppTheme
{
    /// <summary>Jeu sombre « Graphite &amp; Sauge » (défaut).</summary>
    Dark,

    /// <summary>Jeu clair « Craie &amp; Sauge », miroir de profondeur du sombre.</summary>
    Light
}
