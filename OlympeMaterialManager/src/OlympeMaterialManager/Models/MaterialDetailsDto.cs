namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour les details complets d'un materiau (D-16).
/// Retourne par GetMaterialDetails pour alimenter le visualisateur materiau.
/// POCO pur -- aucune dependance Revit API.
/// </summary>
public class MaterialDetailsDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ColorArgb { get; set; }
    public string PatternName { get; set; } = string.Empty;
    public bool HasAppearanceAsset { get; set; }
    public bool TintEnabled { get; set; }
    public int TintColorArgb { get; set; }

    /// <summary>
    /// Chemin resolu de la texture bitmap (B10-TX), pour l'aperçu du
    /// visualisateur en mode Texture. Null = introuvable → fallback couleur.
    /// </summary>
    public string? TexturePath { get; set; }
}
