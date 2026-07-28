namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour une couche de CompoundStructure.
/// Fonction en francais, epaisseur en mm, materiau resolu (D-11).
/// POCO pur -- aucune dependance Revit API.
/// </summary>
public class LayerDto
{
    public int LayerIndex { get; set; }
    public string Function { get; set; } = string.Empty;
    public double Width { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public long MaterialElementIdValue { get; set; } = -1;

    /// <summary>
    /// Couleur de surface ARGB du materiau (B8). Null = « Par catégorie » ou
    /// materiau non resolu → liseré transparent, jamais un gris menteur.
    /// </summary>
    public int? ColorArgb { get; set; }

    /// <summary>
    /// Couleur d'apparence ARGB du materiau (DR2-1) : diffuse/albedo de l'asset
    /// d'apparence, pour le mode Réaliste. Null = pas d'asset ou asset sans
    /// couleur → fallback couleur graphique cote UI.
    /// </summary>
    public int? AppearanceColorArgb { get; set; }

    /// <summary>
    /// Chemin resolu de la texture bitmap du materiau (DR4-1, mode Réaliste
    /// opportuniste). Null = pas d'asset, pas de bitmap ou introuvable →
    /// fallback couleur d'apparence puis couleur graphique cote UI.
    /// </summary>
    public string? TexturePath { get; set; }
}
