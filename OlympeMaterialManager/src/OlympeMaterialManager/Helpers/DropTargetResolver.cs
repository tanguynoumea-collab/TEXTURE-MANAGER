using System.Collections;
using System.Linq;

namespace Olympe.MaterialManager.Helpers;

/// <summary>
/// Decision pure « sur quoi porte un drop ? » (DR5-2), selon la convention des
/// explorateurs de fichiers : deposer sur un element DE la selection agit sur
/// toute la selection ; deposer en dehors agit sur ce seul element et laisse la
/// selection intacte.
/// Aucune dependance WPF ni Revit — logique testable isolement.
/// </summary>
public static class DropTargetResolver
{
    /// <summary>
    /// Resout les cibles effectives d'un drop.
    /// </summary>
    /// <param name="cible">Carte ayant recu le drop.</param>
    /// <param name="selection">
    /// Selection courante du panneau central (<c>ListBox.SelectedItems</c>),
    /// potentiellement null ou heterogene.
    /// </param>
    /// <returns>
    /// La selection entiere si <paramref name="cible"/> en fait partie, sinon la
    /// seule <paramref name="cible"/>. Le filtrage par type garantit qu'on ne
    /// melange jamais couches et parametres : une selection d'un autre type que
    /// la cible est ignoree et le drop reste mono-cible.
    /// L'appartenance est jugee par identite de reference, les DTO n'ayant pas
    /// d'egalite de valeur — deux couches distinctes peuvent avoir les memes champs.
    /// </returns>
    public static IReadOnlyList<T> ResolveDropTargets<T>(T cible, IList? selection)
        where T : class
    {
        var homogenes = selection?.OfType<T>().ToList();

        if (homogenes == null || !homogenes.Any(x => ReferenceEquals(x, cible)))
            return new[] { cible };

        return homogenes;
    }
}
