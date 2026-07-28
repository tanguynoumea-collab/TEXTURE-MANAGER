using Olympe.MaterialManager.Helpers;
using Xunit;

namespace Olympe.MaterialManager.Tests;

/// <summary>
/// Tests du predicat de recherche partage des panneaux (B5) :
/// insensibilite a la casse ET aux accents (« beton » matche « Béton »),
/// terme vide/null = recherche inactive, source vide/null = pas de match.
/// Methode statique pure : aucun type WPF ni Revit.
/// </summary>
public class SearchMatcherTests
{
    [Theory]
    [InlineData("Béton banché C25", "beton")]      // accents : e matche é
    [InlineData("Béton banché C25", "BÉTON")]      // casse avec accents
    [InlineData("Mur de base", "MUR")]             // casse simple
    [InlineData("beton", "Béton")]                 // accents dans le terme
    [InlineData("Cloison métallique", "metallique")]
    [InlineData("Mur de base : Béton 20", "béton 20")] // sous-chaine au milieu
    public void Matches_EstInsensibleCasseEtAccents(string source, string term)
    {
        Assert.True(SearchMatcher.Matches(source, term));
    }

    [Theory]
    [InlineData("Béton banché C25", "bois")]  // aucun match
    [InlineData("Béton", "betonn")]           // terme plus long
    [InlineData("", "beton")]                 // source vide
    [InlineData(null, "beton")]               // source null
    public void Matches_RetourneFalse_QuandAucunMatch(string? source, string term)
    {
        Assert.False(SearchMatcher.Matches(source, term));
    }

    [Theory]
    [InlineData("Béton", "")]      // terme vide : recherche inactive
    [InlineData("Béton", null)]    // terme null
    [InlineData("Béton", "   ")]   // terme espaces
    [InlineData(null, null)]       // tout null : recherche inactive
    [InlineData("", "")]           // tout vide
    public void Matches_RetourneTrue_QuandTermeVide(string? source, string? term)
    {
        Assert.True(SearchMatcher.Matches(source, term));
    }
}
