using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Olympe.MaterialManager.Messages;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Services;

/// <summary>
/// Point unique de vérité du mode d'aperçu des matériaux (B10).
/// Charge le mode depuis settings.json au démarrage (parse tolérant : valeur
/// inconnue ou champ absent → UniformColor), persiste immédiatement chaque
/// changement via SaveSettings (écriture atomique existante) et diffuse
/// PreviewModeChangedMessage. Les vues se bindent sur CurrentMode (INPC).
/// Le bridge Revit ne connaît jamais le mode : il livre des faits (couleurs,
/// chemins de texture), l'UI décide de la présentation.
/// </summary>
public partial class PreviewModeStore : ObservableObject
{
    private readonly PresetService _presetService;

    /// <summary>
    /// Mode d'aperçu courant. Le setter persiste et diffuse le message.
    /// </summary>
    [ObservableProperty]
    private PreviewMode _currentMode;

    public PreviewModeStore(PresetService presetService)
    {
        _presetService = presetService;
        // Affectation directe du champ : le chargement initial ne doit ni
        // re-sauvegarder ni diffuser de message.
        _currentMode = Parse(_presetService.LoadSettings().MaterialPreviewMode);
    }

    /// <summary>
    /// Parse tolérant du mode persisté : valeur inconnue, numérique hors plage
    /// ou null → défaut UniformColor. Jamais d'exception.
    /// DR2-2 : « Texture » (persisté par les versions antérieures, mode
    /// supprimé) est mappé vers Realistic — son remplaçant fonctionnel.
    /// </summary>
    public static PreviewMode Parse(string? value)
    {
        if (string.Equals(value, "Texture", StringComparison.OrdinalIgnoreCase))
            return PreviewMode.Realistic;

        // Pattern match explicite plutôt que Enum.IsDefined : rejette les
        // numériques hors plage ("999" passe TryParse) et compile sans warning
        // sur les deux cibles (la surcharge générique d'IsDefined n'existe pas en net48).
        if (Enum.TryParse<PreviewMode>(value, ignoreCase: true, out var mode)
            && mode is PreviewMode.UniformColor or PreviewMode.Realistic)
        {
            return mode;
        }
        return PreviewMode.UniformColor;
    }

    /// <summary>
    /// Persistance immédiate + diffusion à chaque changement de mode.
    /// FIA-03 : setter bindé sur le thread UI du process Revit — l'écriture
    /// est protégée, un échec I/O ne remonte jamais à l'hôte.
    /// </summary>
    partial void OnCurrentModeChanged(PreviewMode value)
    {
        try
        {
            var settings = _presetService.LoadSettings();
            settings.MaterialPreviewMode = value.ToString();
            _presetService.SaveSettings(settings);
        }
        catch (Exception ex)
        {
            LogService.Error("Echec de sauvegarde du mode d'aperçu des matériaux", ex);
        }

        WeakReferenceMessenger.Default.Send(new PreviewModeChangedMessage(value));
    }
}
