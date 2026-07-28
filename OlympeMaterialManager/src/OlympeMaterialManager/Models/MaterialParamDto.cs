namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour un parametre de type Material sur une famille chargee.
/// Utilise quand le type n'a pas de CompoundStructure (D-14).
/// POCO pur -- aucune dependance Revit API.
/// </summary>
public class MaterialParamDto
{
    public string ParameterName { get; set; } = string.Empty;
    public string CurrentMaterialName { get; set; } = string.Empty;
    public long CurrentMaterialIdValue { get; set; } = -1;
    public string ParameterDefinitionName { get; set; } = string.Empty;

    /// <summary>
    /// Couleur de surface ARGB du materiau (B8). Null = « Aucun »/« Par catégorie »
    /// ou materiau non resolu → liseré transparent, jamais un gris menteur.
    /// </summary>
    public int? ColorArgb { get; set; }

    /// <summary>
    /// Chemin resolu de la texture bitmap (B10-TX). Null = pas de texture
    /// trouvee → fallback couleur cote UI.
    /// </summary>
    public string? TexturePath { get; set; }
}
