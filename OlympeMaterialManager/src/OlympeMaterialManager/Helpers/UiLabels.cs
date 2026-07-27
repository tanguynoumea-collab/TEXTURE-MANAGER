namespace Olympe.MaterialManager.Helpers;

/// <summary>
/// Libelles UI speciaux affiches a la place d'un nom de materiau ou de motif (MAINT-08).
/// Centralises pour que les differents handlers du bridge ne puissent pas diverger.
/// </summary>
public static class UiLabels
{
    /// <summary>Materiau non assigne sur une couche : herite de la categorie.</summary>
    public const string ByCategory = "< Par catégorie >";

    /// <summary>Element reference par un id qui ne resout plus vers un nom.</summary>
    public const string Inconnu = "< Inconnu >";

    /// <summary>Aucun materiau ou motif assigne.</summary>
    public const string Aucun = "< Aucun >";
}
