using System.Collections.ObjectModel;
using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Models;
using Xunit;

namespace Olympe.MaterialManager.Tests;

/// <summary>
/// Tests de la logique pure de validation B1 (materiaux absents a l'activation
/// d'un preset) : construction de la liste a valider depuis les groupes, purge
/// des introuvables sur les groupes sources. Aucun type WPF ni Revit.
/// </summary>
public class PresetMaterialValidationTests
{
    private static PresetMaterialDto Mat(long id, string name) => new()
    {
        MaterialElementIdValue = id,
        MaterialName = name
    };

    private static MaterialRefDto Ref(long id, string name) => new()
    {
        ElementIdValue = id,
        MaterialName = name
    };

    private static PresetGroupDto Group(string name, params PresetMaterialDto[] materials) => new()
    {
        GroupName = name,
        Materials = new ObservableCollection<PresetMaterialDto>(materials)
    };

    [Fact]
    public void BuildMaterialRefs_CollecteTousLesMateriaux_DeTousLesGroupes()
    {
        var groups = new[]
        {
            Group("Bétons", Mat(10, "Béton banché C25"), Mat(20, "Béton lissé")),
            Group("Métaux", Mat(30, "Acier"))
        };

        var refs = PresetMaterialValidation.BuildMaterialRefs(groups);

        Assert.Equal(3, refs.Count);
        Assert.Equal(10, refs[0].ElementIdValue);
        Assert.Equal("Béton banché C25", refs[0].MaterialName);
        Assert.Equal("Acier", refs[2].MaterialName);
    }

    [Fact]
    public void BuildMaterialRefs_DedoublonneParPaireIdNom()
    {
        // Meme materiau present dans deux groupes : valide une seule fois
        var groups = new[]
        {
            Group("Bétons", Mat(10, "Béton")),
            Group("Favoris", Mat(10, "Béton"), Mat(20, "Acier"))
        };

        var refs = PresetMaterialValidation.BuildMaterialRefs(groups);

        Assert.Equal(2, refs.Count);
        Assert.Equal("Béton", refs[0].MaterialName);
        Assert.Equal("Acier", refs[1].MaterialName);
    }

    [Fact]
    public void BuildMaterialRefs_RetourneVide_SansGroupes()
    {
        var refs = PresetMaterialValidation.BuildMaterialRefs([]);

        Assert.Empty(refs);
    }

    [Fact]
    public void RemoveMaterials_RetireLesBonsMateriaux_EtRetourneLeCompte()
    {
        var betons = Group("Bétons", Mat(10, "Béton banché C25"), Mat(20, "Béton lissé"));
        var metaux = Group("Métaux", Mat(30, "Acier"));
        var groups = new[] { betons, metaux };

        int removed = PresetMaterialValidation.RemoveMaterials(
            groups, new[] { Ref(20, "Béton lissé"), Ref(30, "Acier") });

        Assert.Equal(2, removed);
        Assert.Single(betons.Materials);
        Assert.Equal("Béton banché C25", betons.Materials[0].MaterialName);
        Assert.Empty(metaux.Materials);
    }

    [Fact]
    public void RemoveMaterials_ConserveLesGroupesVides()
    {
        var betons = Group("Bétons", Mat(10, "Béton"));
        var groups = new List<PresetGroupDto> { betons };

        PresetMaterialValidation.RemoveMaterials(groups, new[] { Ref(10, "Béton") });

        // Le groupe vide n'est PAS retire de la liste : structure du preset intacte
        Assert.Single(groups);
        Assert.Same(betons, groups[0]);
        Assert.Empty(betons.Materials);
    }

    [Fact]
    public void RemoveMaterials_NeRetirePas_QuandLaPaireNeCorrespondPas()
    {
        // Meme id mais nom different : la paire (id, nom) ne matche pas -> conserve
        var betons = Group("Bétons", Mat(10, "Béton renommé"));

        int removed = PresetMaterialValidation.RemoveMaterials(
            new[] { betons }, new[] { Ref(10, "Béton") });

        Assert.Equal(0, removed);
        Assert.Single(betons.Materials);
    }

    [Fact]
    public void RemoveMaterials_ListeVide_CollectionIntacte()
    {
        // « Conserver » : aucune purge demandee, la collection reste intacte
        var betons = Group("Bétons", Mat(10, "Béton"), Mat(20, "Acier"));

        int removed = PresetMaterialValidation.RemoveMaterials(new[] { betons }, []);

        Assert.Equal(0, removed);
        Assert.Equal(2, betons.Materials.Count);
    }

    [Fact]
    public void RemoveMaterials_RetireLaPaireDansTousLesGroupes()
    {
        // Le meme materiau introuvable present dans deux groupes : purge partout
        var betons = Group("Bétons", Mat(10, "Béton"));
        var favoris = Group("Favoris", Mat(10, "Béton"), Mat(20, "Acier"));

        int removed = PresetMaterialValidation.RemoveMaterials(
            new[] { betons, favoris }, new[] { Ref(10, "Béton") });

        Assert.Equal(2, removed);
        Assert.Empty(betons.Materials);
        Assert.Single(favoris.Materials);
        Assert.Equal("Acier", favoris.Materials[0].MaterialName);
    }
}
