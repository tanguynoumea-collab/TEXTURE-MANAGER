using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Helpers;

/// <summary>
/// Logique pure de la validation des materiaux d'un preset a l'activation (B1) :
/// construction de la liste a valider par le bridge, purge des introuvables.
/// Methodes statiques testables sans Revit ni WPF.
/// </summary>
public static class PresetMaterialValidation
{
    /// <summary>
    /// Construit la liste (id, nom) de TOUS les materiaux du preset, dedoublonnee
    /// par paire (id, nom) : le meme materiau present dans plusieurs groupes n'est
    /// valide qu'une fois. L'ordre de parcours des groupes est preserve.
    /// </summary>
    public static List<MaterialRefDto> BuildMaterialRefs(IEnumerable<PresetGroupDto> groups)
    {
        var seen = new HashSet<(long, string)>();
        var result = new List<MaterialRefDto>();

        foreach (var group in groups)
        {
            foreach (var mat in group.Materials)
            {
                if (!seen.Add((mat.MaterialElementIdValue, mat.MaterialName)))
                    continue;

                result.Add(new MaterialRefDto
                {
                    ElementIdValue = mat.MaterialElementIdValue,
                    MaterialName = mat.MaterialName
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Retire des groupes tous les materiaux dont la paire (id, nom) figure dans
    /// la liste des introuvables (comparaison exacte, coherente avec
    /// ResolveMaterial). Les groupes devenus vides sont CONSERVES — la structure
    /// du preset reste intacte. Retourne le nombre de materiaux retires.
    /// A appeler UNIQUEMENT sur les groupes SOURCES (jamais les clones de la
    /// projection de recherche B5-D).
    /// </summary>
    public static int RemoveMaterials(
        IEnumerable<PresetGroupDto> groups,
        IEnumerable<MaterialRefDto> missing)
    {
        var missingSet = new HashSet<(long, string)>();
        foreach (var r in missing)
            missingSet.Add((r.ElementIdValue, r.MaterialName));

        if (missingSet.Count == 0)
            return 0;

        int removed = 0;
        foreach (var group in groups)
        {
            for (int i = group.Materials.Count - 1; i >= 0; i--)
            {
                var mat = group.Materials[i];
                if (missingSet.Contains((mat.MaterialElementIdValue, mat.MaterialName)))
                {
                    group.Materials.RemoveAt(i);
                    removed++;
                }
            }
        }

        return removed;
    }

    /// <summary>
    /// Met a jour EN PLACE les couleurs des materiaux de preset trouves dans le
    /// document (DR3-1) : pour chaque materiau dont la paire (id, nom) figure
    /// dans les couleurs rafraichies, ColorArgb et AppearanceColorArgb sont
    /// alignes sur les valeurs actuelles du document (y compris apparence → null,
    /// verite fraiche). PresetMaterialDto notifie (INPC) : les pastilles suivent
    /// sans re-binding. A appeler UNIQUEMENT sur les groupes SOURCES — les clones
    /// de la projection de recherche B5-D referencent les memes instances.
    /// Retourne le nombre de materiaux effectivement modifies (0 = rien a
    /// persister, l'appelant ne declenche pas d'AutoSave).
    /// </summary>
    public static int ApplyRefreshedColors(
        IEnumerable<PresetGroupDto> groups,
        IEnumerable<RefreshedMaterialColorsDto> refreshed)
    {
        var byKey = new Dictionary<(long, string), RefreshedMaterialColorsDto>();
        foreach (var r in refreshed)
            byKey[(r.ElementIdValue, r.MaterialName)] = r;

        if (byKey.Count == 0)
            return 0;

        int changed = 0;
        foreach (var group in groups)
        {
            foreach (var mat in group.Materials)
            {
                if (!byKey.TryGetValue((mat.MaterialElementIdValue, mat.MaterialName), out var fresh))
                    continue;

                bool materialChanged = false;
                if (mat.ColorArgb != fresh.ColorArgb)
                {
                    mat.ColorArgb = fresh.ColorArgb;
                    materialChanged = true;
                }
                if (mat.AppearanceColorArgb != fresh.AppearanceColorArgb)
                {
                    mat.AppearanceColorArgb = fresh.AppearanceColorArgb;
                    materialChanged = true;
                }

                if (materialChanged)
                    changed++;
            }
        }

        return changed;
    }
}
