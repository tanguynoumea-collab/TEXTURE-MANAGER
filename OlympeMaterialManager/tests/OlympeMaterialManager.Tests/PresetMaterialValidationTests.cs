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

    // --- ApplyRefreshedColors (DR3-1) ---

    private static PresetMaterialDto MatColors(long id, string name, int colorArgb, int? appearanceArgb)
    {
        var mat = Mat(id, name);
        mat.ColorArgb = colorArgb;
        mat.AppearanceColorArgb = appearanceArgb;
        return mat;
    }

    private static RefreshedMaterialColorsDto Fresh(long id, string name, int colorArgb, int? appearanceArgb) => new()
    {
        ElementIdValue = id,
        MaterialName = name,
        ColorArgb = colorArgb,
        AppearanceColorArgb = appearanceArgb
    };

    [Fact]
    public void ApplyRefreshedColors_MetAJourEnPlace_EtRetourneLeCompte()
    {
        // Preset d'avant DR2 : AppearanceColorArgb absent du JSON (null)
        var beton = MatColors(10, "Béton", colorArgb: 100, appearanceArgb: null);
        var acier = MatColors(20, "Acier", colorArgb: 200, appearanceArgb: 250);
        var groups = new[] { Group("Bétons", beton), Group("Métaux", acier) };

        int changed = PresetMaterialValidation.ApplyRefreshedColors(groups, new[]
        {
            Fresh(10, "Béton", colorArgb: 100, appearanceArgb: 300), // apparence enrichie
            Fresh(20, "Acier", colorArgb: 999, appearanceArgb: 250)  // couleur graphique changee
        });

        Assert.Equal(2, changed);
        Assert.Equal(100, beton.ColorArgb);
        Assert.Equal(300, beton.AppearanceColorArgb);
        Assert.Equal(999, acier.ColorArgb);
        Assert.Equal(250, acier.AppearanceColorArgb);
    }

    [Fact]
    public void ApplyRefreshedColors_RetourneZero_QuandRienNeChange()
    {
        // Idempotence : re-validation sans changement -> pas d'AutoSave cote VM
        var beton = MatColors(10, "Béton", colorArgb: 100, appearanceArgb: 300);
        var groups = new[] { Group("Bétons", beton) };

        int changed = PresetMaterialValidation.ApplyRefreshedColors(
            groups, new[] { Fresh(10, "Béton", colorArgb: 100, appearanceArgb: 300) });

        Assert.Equal(0, changed);
    }

    [Fact]
    public void ApplyRefreshedColors_IgnoreLesPairesNonCorrespondantes()
    {
        // Meme id mais nom different : la paire (id, nom) ne matche pas -> intact
        var beton = MatColors(10, "Béton renommé", colorArgb: 100, appearanceArgb: null);
        var groups = new[] { Group("Bétons", beton) };

        int changed = PresetMaterialValidation.ApplyRefreshedColors(
            groups, new[] { Fresh(10, "Béton", colorArgb: 999, appearanceArgb: 300) });

        Assert.Equal(0, changed);
        Assert.Equal(100, beton.ColorArgb);
        Assert.Null(beton.AppearanceColorArgb);
    }

    [Fact]
    public void ApplyRefreshedColors_MetAJourLaPaireDansTousLesGroupes()
    {
        // Le meme materiau present dans deux groupes : rafraichi partout
        var beton1 = MatColors(10, "Béton", colorArgb: 100, appearanceArgb: null);
        var beton2 = MatColors(10, "Béton", colorArgb: 100, appearanceArgb: null);
        var groups = new[] { Group("Bétons", beton1), Group("Favoris", beton2) };

        int changed = PresetMaterialValidation.ApplyRefreshedColors(
            groups, new[] { Fresh(10, "Béton", colorArgb: 100, appearanceArgb: 300) });

        Assert.Equal(2, changed);
        Assert.Equal(300, beton1.AppearanceColorArgb);
        Assert.Equal(300, beton2.AppearanceColorArgb);
    }

    [Fact]
    public void ApplyRefreshedColors_ApparenceDevenueNull_EstAppliquee()
    {
        // L'asset d'apparence a ete retire dans le document : null est la verite
        // fraiche, le fallback couleur graphique redevient le chemin nominal
        var beton = MatColors(10, "Béton", colorArgb: 100, appearanceArgb: 300);
        var groups = new[] { Group("Bétons", beton) };

        int changed = PresetMaterialValidation.ApplyRefreshedColors(
            groups, new[] { Fresh(10, "Béton", colorArgb: 100, appearanceArgb: null) });

        Assert.Equal(1, changed);
        Assert.Null(beton.AppearanceColorArgb);
    }

    [Fact]
    public void ApplyRefreshedColors_ListeVide_CollectionIntacte()
    {
        var beton = MatColors(10, "Béton", colorArgb: 100, appearanceArgb: null);
        var groups = new[] { Group("Bétons", beton) };

        int changed = PresetMaterialValidation.ApplyRefreshedColors(groups, []);

        Assert.Equal(0, changed);
        Assert.Equal(100, beton.ColorArgb);
        Assert.Null(beton.AppearanceColorArgb);
    }

    // --- CanPromptMissingMaterials (DR6-1) ---

    [Fact]
    public void CanPromptMissingMaterials_VraiQuandFenetreVisibleEtPasDePick()
    {
        Assert.True(PresetMaterialValidation.CanPromptMissingMaterials(
            missingCount: 2, isPicking: false, isMainWindowVisible: true));
    }

    [Theory]
    [InlineData(true, true)]    // pick pipette en cours
    [InlineData(false, false)]  // fenetre cachee (pas d'owner visible)
    [InlineData(true, false)]   // les deux
    public void CanPromptMissingMaterials_FauxQuandLeContexteInterditLeDialogue(
        bool isPicking, bool isVisible)
    {
        Assert.False(PresetMaterialValidation.CanPromptMissingMaterials(
            missingCount: 2, isPicking, isVisible));
    }

    [Fact]
    public void CanPromptMissingMaterials_FauxSansIntrouvable()
    {
        Assert.False(PresetMaterialValidation.CanPromptMissingMaterials(
            missingCount: 0, isPicking: false, isMainWindowVisible: true));
    }

    [Fact]
    public void CanPromptMissingMaterials_NeGouvernePasLeRafraichissement()
    {
        // DR6-1 : le rafraichissement des couleurs/textures s'applique meme quand
        // le dialogue est supprime (fenetre cachee au tout premier chargement) —
        // les deux effets du callback de validation sont independants.
        var beton = MatColors(10, "Béton", colorArgb: 100, appearanceArgb: null);
        var groups = new[] { Group("Bétons", beton) };

        bool prompt = PresetMaterialValidation.CanPromptMissingMaterials(
            missingCount: 1, isPicking: false, isMainWindowVisible: false);
        int changed = PresetMaterialValidation.ApplyRefreshedColors(
            groups, new[] { Fresh(10, "Béton", colorArgb: 100, appearanceArgb: 0x505050) });

        Assert.False(prompt);
        Assert.Equal(1, changed);
        Assert.Equal(0x505050, beton.AppearanceColorArgb);
    }
}
