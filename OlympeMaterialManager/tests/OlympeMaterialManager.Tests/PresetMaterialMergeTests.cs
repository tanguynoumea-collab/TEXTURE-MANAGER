using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Models;
using Xunit;

namespace Olympe.MaterialManager.Tests;

/// <summary>
/// Tests du dedoublonnage de la pipette materiau (B2) :
/// ids invalides (« Par catégorie ») ignores, doublons par id OU par nom
/// contre le groupe cible ignores, dedoublonnage entre candidats,
/// ordre des candidats retenus preserve.
/// Methode statique pure : aucun type WPF ni Revit.
/// </summary>
public class PresetMaterialMergeTests
{
    private static PresetMaterialDto Mat(long id, string name) => new()
    {
        MaterialElementIdValue = id,
        MaterialName = name
    };

    [Fact]
    public void SelectNewMaterials_RetourneTousLesCandidats_QuandGroupeVide()
    {
        var candidates = new[] { Mat(10, "Béton banché C25"), Mat(20, "Acier") };

        var result = PresetMaterialMerge.SelectNewMaterials(candidates, []);

        Assert.Equal(2, result.Count);
        Assert.Equal("Béton banché C25", result[0].MaterialName);
        Assert.Equal("Acier", result[1].MaterialName);
    }

    [Fact]
    public void SelectNewMaterials_IgnoreLesIdsInvalides()
    {
        // « Par catégorie » : MaterialId = InvalidElementId (-1)
        var candidates = new[] { Mat(-1, "< Par catégorie >"), Mat(10, "Béton") };

        var result = PresetMaterialMerge.SelectNewMaterials(candidates, []);

        Assert.Single(result);
        Assert.Equal(10, result[0].MaterialElementIdValue);
    }

    [Fact]
    public void SelectNewMaterials_IgnoreLesDoublonsParId()
    {
        var existing = new[] { Mat(10, "Béton (ancien nom)") };
        var candidates = new[] { Mat(10, "Béton"), Mat(20, "Acier") };

        var result = PresetMaterialMerge.SelectNewMaterials(candidates, existing);

        Assert.Single(result);
        Assert.Equal(20, result[0].MaterialElementIdValue);
    }

    [Fact]
    public void SelectNewMaterials_IgnoreLesDoublonsParNom()
    {
        // Meme nom deja present sous un autre id (autre document) : doublon
        var existing = new[] { Mat(99, "Béton") };
        var candidates = new[] { Mat(10, "Béton"), Mat(20, "Acier") };

        var result = PresetMaterialMerge.SelectNewMaterials(candidates, existing);

        Assert.Single(result);
        Assert.Equal("Acier", result[0].MaterialName);
    }

    [Fact]
    public void SelectNewMaterials_DedoublonneLesCandidatsEntreEux()
    {
        // Meme materiau sur plusieurs couches du meme type
        var candidates = new[] { Mat(10, "Béton"), Mat(10, "Béton"), Mat(20, "Acier") };

        var result = PresetMaterialMerge.SelectNewMaterials(candidates, []);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SelectNewMaterials_ComparaisonDeNomExacte_AccentsEtCasseDistinguent()
    {
        // Coherent avec ResolveMaterial : comparaison exacte, « Beton » != « Béton »
        var existing = new[] { Mat(99, "Béton") };
        var candidates = new[] { Mat(10, "Beton") };

        var result = PresetMaterialMerge.SelectNewMaterials(candidates, existing);

        Assert.Single(result);
    }

    [Fact]
    public void SelectNewMaterials_RetourneVide_QuandTousDoublonsOuInvalides()
    {
        var existing = new[] { Mat(10, "Béton") };
        var candidates = new[] { Mat(10, "Béton"), Mat(-1, "< Par catégorie >") };

        var result = PresetMaterialMerge.SelectNewMaterials(candidates, existing);

        Assert.Empty(result);
    }
}
