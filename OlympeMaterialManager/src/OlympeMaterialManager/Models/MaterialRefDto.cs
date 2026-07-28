namespace Olympe.MaterialManager.Models;

/// <summary>
/// Reference legere (id, nom) d'un materiau de preset pour la validation B1.
/// La paire suit la meme regle que ResolveMaterial (DON-04) : l'id n'est qu'un
/// cache, le nom est la verite portable entre documents.
/// </summary>
public class MaterialRefDto
{
    public long ElementIdValue { get; set; } = -1;
    public string MaterialName { get; set; } = string.Empty;
}
