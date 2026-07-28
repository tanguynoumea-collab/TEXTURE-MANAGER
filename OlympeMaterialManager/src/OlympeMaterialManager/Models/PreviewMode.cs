namespace Olympe.MaterialManager.Models;

/// <summary>
/// Mode d'aperçu des matériaux dans l'interface (B10, refondu DR2-2).
/// S'applique au carré d'aperçu du visualisateur, aux pastilles des presets
/// et au liseré des cartes du panneau central (B8).
/// Persisté en STRING dans settings.json (jamais l'enum sérialisé — une valeur
/// inconnue enverrait le fichier entier en quarantaine, DON-02).
/// L'ancien mode « Texture » (bitmap) a été supprimé après diagnostic terrain
/// (zéro texture résolvable) ; sa valeur persistée est mappée vers Realistic
/// par PreviewModeStore.Parse.
/// </summary>
public enum PreviewMode
{
    /// <summary>Couleur de surface uniforme du matériau (défaut).</summary>
    UniformColor,

    /// <summary>Couleur d'apparence (diffuse/albedo de l'asset d'apparence) —
    /// ce que la vue 3D Réaliste de Revit affiche pour un matériau sans texture.
    /// Fallback couleur graphique si absente.</summary>
    Realistic
}
