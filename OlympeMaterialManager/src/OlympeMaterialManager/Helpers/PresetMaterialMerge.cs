using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Helpers;

/// <summary>
/// Logique pure de dedoublonnage des materiaux recuperes par la pipette (B2).
/// Extraite en methode statique pour etre testable sans Revit ni WPF.
/// </summary>
public static class PresetMaterialMerge
{
    /// <summary>
    /// Filtre les candidats a l'ajout dans un groupe preset :
    /// - ignore les ids invalides (&lt; 0 : « Par catégorie », « Aucun ») ;
    /// - ignore les doublons par ElementIdValue OU par nom deja present dans le
    ///   groupe cible (comparaison exacte, coherente avec ResolveMaterial) ;
    /// - dedoublonne aussi les candidats entre eux (meme materiau sur
    ///   plusieurs couches d'un meme type).
    /// L'ordre des candidats retenus est preserve.
    /// </summary>
    public static List<PresetMaterialDto> SelectNewMaterials(
        IEnumerable<PresetMaterialDto> candidates,
        IEnumerable<PresetMaterialDto> existingMaterials)
    {
        var seenIds = new HashSet<long>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var existing in existingMaterials)
        {
            if (existing.MaterialElementIdValue >= 0)
                seenIds.Add(existing.MaterialElementIdValue);
            if (!string.IsNullOrEmpty(existing.MaterialName))
                seenNames.Add(existing.MaterialName);
        }

        var result = new List<PresetMaterialDto>();
        foreach (var candidate in candidates)
        {
            // « Par catégorie » / materiau non resolu : id invalide, jamais ajoute
            if (candidate.MaterialElementIdValue < 0) continue;

            if (!seenIds.Add(candidate.MaterialElementIdValue)) continue;

            if (!string.IsNullOrEmpty(candidate.MaterialName) &&
                !seenNames.Add(candidate.MaterialName))
            {
                continue;
            }

            result.Add(candidate);
        }

        return result;
    }
}
