namespace Olympe.MaterialManager.Models;

/// <summary>
/// Couleurs actuelles d'un materiau de preset TROUVE dans le document lors de
/// la validation B1 (DR3-1). La paire (ElementIdValue, MaterialName) reprend
/// la reference DU PRESET (pas l'id resolu) : c'est la cle de mise a jour en
/// place cote ViewModel. AppearanceColorArgb null = pas d'asset d'apparence ou
/// asset sans couleur (fallback couleur graphique cote UI).
/// </summary>
public class RefreshedMaterialColorsDto
{
    public long ElementIdValue { get; set; } = -1;
    public string MaterialName { get; set; } = string.Empty;
    public int ColorArgb { get; set; }
    public int? AppearanceColorArgb { get; set; }

    /// <summary>
    /// Chemin resolu de la texture bitmap (DR4-1/DR3-1 : le rafraichissement a
    /// l'activation transporte aussi le TexturePath frais). Null = pas de
    /// bitmap ou introuvable.
    /// </summary>
    public string? TexturePath { get; set; }
}
