using System.Collections.Generic;
using System.Linq;
using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Models;
using Xunit;

namespace Olympe.MaterialManager.Tests;

/// <summary>
/// Tests de la decision de portee d'un drop (DR5-2), convention des explorateurs :
/// deposer sur un element DE la selection agit sur toute la selection, deposer
/// hors selection agit sur ce seul element. Verifie aussi qu'on ne melange jamais
/// couches et parametres, et que l'appartenance repose sur l'identite de reference
/// (les DTO n'ont pas d'egalite de valeur).
/// Methode statique pure : aucun type WPF ni Revit.
/// </summary>
public class DropTargetResolverTests
{
    private static LayerDto Couche(int index) => new() { LayerIndex = index };

    private static MaterialParamDto Parametre(string nom)
        => new() { ParameterDefinitionName = nom };

    /// <summary>
    /// Imite ListBox.SelectedItems : une liste non generique, potentiellement
    /// heterogene, transmise telle quelle au resolveur.
    /// </summary>
    private static List<object> Selection(params object[] items) => items.ToList();

    [Fact]
    public void CibleDansLaSelection_RenvoieTouteLaSelection()
    {
        var a = Couche(0);
        var b = Couche(1);
        var c = Couche(2);

        var cibles = DropTargetResolver.ResolveDropTargets(b, Selection(a, b, c));

        Assert.Equal(new[] { a, b, c }, cibles);
    }

    [Fact]
    public void CibleHorsSelection_RenvoieLaSeuleCible()
    {
        var a = Couche(0);
        var b = Couche(1);
        var horsSelection = Couche(9);

        var cibles = DropTargetResolver.ResolveDropTargets(horsSelection, Selection(a, b));

        Assert.Equal(new[] { horsSelection }, cibles);
    }

    [Fact]
    public void SelectionNulle_RenvoieLaSeuleCible()
    {
        var cible = Couche(3);

        var cibles = DropTargetResolver.ResolveDropTargets(cible, null);

        Assert.Equal(new[] { cible }, cibles);
    }

    [Fact]
    public void SelectionVide_RenvoieLaSeuleCible()
    {
        var cible = Couche(3);

        var cibles = DropTargetResolver.ResolveDropTargets(cible, Selection());

        Assert.Equal(new[] { cible }, cibles);
    }

    [Fact]
    public void SelectionUnique_EgaleALaCible_RenvoieCetteCible()
    {
        var cible = Couche(4);

        var cibles = DropTargetResolver.ResolveDropTargets(cible, Selection(cible));

        Assert.Equal(new[] { cible }, cibles);
    }

    [Fact]
    public void SelectionDeParametres_CibleCouche_NeMelangePas()
    {
        var couche = Couche(0);
        var selectionParametres = Selection(Parametre("Matériau"), Parametre("Matériau 2"));

        var cibles = DropTargetResolver.ResolveDropTargets(couche, selectionParametres);

        Assert.Equal(new[] { couche }, cibles);
    }

    [Fact]
    public void SelectionDeCouches_CibleParametre_NeMelangePas()
    {
        var param = Parametre("Matériau");
        var selectionCouches = Selection(Couche(0), Couche(1));

        var cibles = DropTargetResolver.ResolveDropTargets(param, selectionCouches);

        Assert.Equal(new[] { param }, cibles);
    }

    [Fact]
    public void SelectionHeterogene_NeGardeQueLesElementsDuTypeDeLaCible()
    {
        var a = Parametre("Matériau");
        var b = Parametre("Matériau 2");
        var selection = Selection(a, Couche(0), b, Couche(1));

        var cibles = DropTargetResolver.ResolveDropTargets(a, selection);

        Assert.Equal(new[] { a, b }, cibles);
    }

    [Fact]
    public void JumeauDeValeurNonSelectionne_ResteMonoCible()
    {
        // Deux couches aux champs identiques mais distinctes : l'appartenance
        // se juge par reference, sans quoi le drop deborderait sur la selection.
        var selectionnee = Couche(7);
        var jumelle = Couche(7);

        var cibles = DropTargetResolver.ResolveDropTargets(jumelle, Selection(selectionnee));

        Assert.Equal(new[] { jumelle }, cibles);
        Assert.DoesNotContain(selectionnee, (IEnumerable<LayerDto>)cibles);
    }

    [Fact]
    public void ParametresSelectionnes_PreserventLOrdreDeLaSelection()
    {
        var a = Parametre("A");
        var b = Parametre("B");
        var c = Parametre("C");

        var cibles = DropTargetResolver.ResolveDropTargets(c, Selection(a, b, c));

        Assert.Equal(new[] { a, b, c }, cibles);
    }
}
