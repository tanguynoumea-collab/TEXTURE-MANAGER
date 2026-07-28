using System.Globalization;

namespace Olympe.MaterialManager.Helpers;

/// <summary>
/// Predicat de recherche partage par les panneaux gauche et droit (B5).
/// Comparaison insensible a la casse ET aux accents : « beton » matche « Béton ».
/// CompareInfo.IndexOf avec IgnoreNonSpace — un Contains(OrdinalIgnoreCase)
/// serait une faute en UI francaise (les accents ne matcheraient pas).
/// Methode statique pure, sans etat : testable en xunit sans WPF ni Revit.
/// </summary>
public static class SearchMatcher
{
    /// <summary>
    /// True si <paramref name="source"/> contient <paramref name="term"/>,
    /// sans tenir compte de la casse ni des accents.
    /// Terme null/vide/espaces : tout matche (recherche inactive).
    /// Source null/vide : ne matche que si le terme est vide.
    /// </summary>
    public static bool Matches(string? source, string? term)
    {
        if (string.IsNullOrWhiteSpace(term)) return true;
        if (string.IsNullOrEmpty(source)) return false;

        return CultureInfo.CurrentCulture.CompareInfo.IndexOf(
            source, term, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
    }
}
