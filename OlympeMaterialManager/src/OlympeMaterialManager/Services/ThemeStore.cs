using System.Collections.Generic;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Olympe.MaterialManager.Messages;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Services;

/// <summary>
/// Point unique de vérité du jeu de couleurs de l'interface (cycle 4).
/// Même contrat que PreviewModeStore : chargement depuis settings.json au
/// démarrage (parse tolérant : valeur inconnue ou champ absent → Dark),
/// persistance immédiate à chaque changement, diffusion d'un message, INPC
/// pour le binding des vues.
///
/// Application des couleurs — un add-in Revit n'a pas d'Application WPF qui lui
/// appartienne (Application.Current est celle de l'hôte, quand elle existe) : le
/// dictionnaire de couleurs est donc superposé aux Resources de CHAQUE fenêtre
/// Olympe, qui s'enregistre via <see cref="RegisterHost"/>. Comme un dictionnaire
/// fusionné plus tard l'emporte, la palette posée ici gagne sur le ThemeDark que
/// OlympeTheme.xaml fusionne par défaut. Les pinceaux étant référencés en
/// DynamicResource, le remplacement se voit immédiatement, sans recréer les vues.
///
/// À n'utiliser que depuis le thread UI WPF.
/// </summary>
public partial class ThemeStore : ObservableObject
{
    private const string DarkDictionaryUri =
        "pack://application:,,,/OlympeMaterialManager;component/Themes/ThemeDark.xaml";

    private const string LightDictionaryUri =
        "pack://application:,,,/OlympeMaterialManager;component/Themes/ThemeLight.xaml";

    private static readonly Uri _darkUri = new(DarkDictionaryUri);
    private static readonly Uri _lightUri = new(LightDictionaryUri);

    /// <summary>
    /// Dictionnaires de ressources des fenêtres Olympe ouvertes. Les fenêtres se
    /// désenregistrent à leur fermeture : les dialogues sont recréés à chaque
    /// ouverture, la liste ne doit pas grossir indéfiniment.
    /// </summary>
    private static readonly List<ResourceDictionary> _hosts = [];

    /// <summary>
    /// Thème réellement appliqué, mémorisé au niveau statique pour qu'une fenêtre
    /// créée APRÈS une bascule (un dialogue, par exemple) s'ouvre déjà au bon jeu.
    /// </summary>
    private static AppTheme _appliedTheme = AppTheme.Dark;

    private readonly PresetService _presetService;

    /// <summary>
    /// Jeu de couleurs courant. Le setter applique, persiste et diffuse.
    /// </summary>
    [ObservableProperty]
    private AppTheme _currentTheme;

    public ThemeStore(PresetService presetService)
    {
        _presetService = presetService;
        // Affectation directe du champ : le chargement initial ne doit ni
        // re-sauvegarder ni diffuser de message — mais il doit appliquer, sinon
        // la fenêtre principale s'ouvrirait au thème par défaut avant bascule.
        _currentTheme = Parse(_presetService.LoadSettings().Theme);
        ApplyToAllHosts(_currentTheme);
    }

    /// <summary>
    /// Parse tolérant du thème persisté : valeur inconnue, numérique hors plage
    /// ou null → défaut Dark. Jamais d'exception.
    /// </summary>
    public static AppTheme Parse(string? value)
    {
        // Pattern match explicite plutôt que Enum.IsDefined : rejette les
        // numériques hors plage ("999" passe TryParse) et compile sans warning
        // sur les deux cibles (même raisonnement que PreviewModeStore.Parse).
        if (Enum.TryParse<AppTheme>(value, ignoreCase: true, out var theme)
            && theme is AppTheme.Dark or AppTheme.Light)
        {
            return theme;
        }
        return AppTheme.Dark;
    }

    /// <summary>
    /// Enregistre les Resources d'une fenêtre Olympe et lui applique aussitôt le
    /// thème courant. À appeler après InitializeComponent.
    /// </summary>
    public static void RegisterHost(ResourceDictionary? hostResources)
    {
        if (hostResources is null) return;
        if (!_hosts.Contains(hostResources)) _hosts.Add(hostResources);
        ApplyToHost(hostResources, _appliedTheme);
    }

    /// <summary>
    /// Retire une fenêtre fermée du jeu de destinataires.
    /// </summary>
    public static void UnregisterHost(ResourceDictionary? hostResources)
    {
        if (hostResources is null) return;
        _hosts.Remove(hostResources);
    }

    /// <summary>
    /// Application + persistance immédiate + diffusion à chaque bascule.
    /// L'application passe avant la persistance : un échec d'écriture ne doit
    /// pas priver l'utilisateur du thème qu'il vient de demander.
    /// </summary>
    partial void OnCurrentThemeChanged(AppTheme value)
    {
        ApplyToAllHosts(value);

        try
        {
            var settings = _presetService.LoadSettings();
            settings.Theme = value.ToString();
            _presetService.SaveSettings(settings);
        }
        catch (Exception ex)
        {
            LogService.Error("Echec de sauvegarde du theme de l'interface", ex);
        }

        WeakReferenceMessenger.Default.Send(new ThemeChangedMessage(value));
    }

    private static void ApplyToAllHosts(AppTheme theme)
    {
        _appliedTheme = theme;
        foreach (var host in _hosts)
        {
            ApplyToHost(host, theme);
        }
    }

    /// <summary>
    /// Retire la palette précédemment superposée sur cette fenêtre puis pose la
    /// nouvelle. Le retrait se fait par comparaison d'URI source : il ne touche
    /// jamais OlympeTheme.xaml (structure) ni les dictionnaires locaux de la vue.
    /// </summary>
    private static void ApplyToHost(ResourceDictionary host, AppTheme theme)
    {
        var merged = host.MergedDictionaries;

        for (var i = merged.Count - 1; i >= 0; i--)
        {
            var source = merged[i].Source;
            if (source is not null && (source.Equals(_darkUri) || source.Equals(_lightUri)))
            {
                merged.RemoveAt(i);
            }
        }

        merged.Add(new ResourceDictionary
        {
            Source = theme == AppTheme.Light ? _lightUri : _darkUri
        });
    }
}
