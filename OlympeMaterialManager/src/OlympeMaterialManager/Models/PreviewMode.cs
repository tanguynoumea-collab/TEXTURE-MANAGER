namespace Olympe.MaterialManager.Models;

/// <summary>
/// Mode d'aperçu des matériaux dans l'interface (B10).
/// S'applique au carré d'aperçu du visualisateur, aux pastilles des presets
/// et au liseré des cartes du panneau central (B8).
/// Persisté en STRING dans settings.json (jamais l'enum sérialisé — une valeur
/// inconnue enverrait le fichier entier en quarantaine, DON-02).
/// </summary>
public enum PreviewMode
{
    /// <summary>Couleur de surface uniforme du matériau (défaut).</summary>
    UniformColor,

    /// <summary>Texture bitmap de l'asset d'apparence (fallback couleur si introuvable).</summary>
    Texture,

    /// <summary>Rendu réaliste — réservé phase 2, présent pour la stabilité du schéma.</summary>
    Realistic
}
