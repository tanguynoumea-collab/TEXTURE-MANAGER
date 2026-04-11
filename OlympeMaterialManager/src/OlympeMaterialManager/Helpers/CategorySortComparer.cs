using System.Collections;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Helpers;

/// <summary>
/// Comparateur de tri pour le TreeView : Murs en premier, Sols en deuxieme,
/// puis ordre alphabetique. Au sein d'une meme categorie, tri par TypeName (D-05).
/// </summary>
public class CategorySortComparer : IComparer
{
    private static readonly Dictionary<string, int> _priorityMap = new()
    {
        { "Murs", 0 },
        { "Sols", 1 },
    };

    public int Compare(object? x, object? y)
    {
        var catX = GetSortKey(x);
        var catY = GetSortKey(y);

        int prioX = _priorityMap.TryGetValue(catX.category, out var px) ? px : 100;
        int prioY = _priorityMap.TryGetValue(catY.category, out var py) ? py : 100;

        if (prioX != prioY) return prioX.CompareTo(prioY);

        int catCompare = string.Compare(catX.category, catY.category, StringComparison.OrdinalIgnoreCase);
        if (catCompare != 0) return catCompare;

        return string.Compare(catX.typeName, catY.typeName, StringComparison.OrdinalIgnoreCase);
    }

    private static (string category, string typeName) GetSortKey(object? item)
    {
        if (item is SceneTypeDto dto)
            return (dto.CategoryName, dto.TypeName);
        return (item?.ToString() ?? "", "");
    }
}
