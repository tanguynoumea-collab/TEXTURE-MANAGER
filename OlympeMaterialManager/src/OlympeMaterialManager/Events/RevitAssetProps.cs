namespace Olympe.MaterialManager.Events;

/// <summary>
/// Noms des AssetProperty du schema d'apparence generique Revit utilises par le bridge (MAINT-08).
/// Point de verite unique pour que la lecture (GetMaterialDetails) et l'ecriture
/// (EditMaterialTint) ne puissent pas diverger.
/// </summary>
internal static class RevitAssetProps
{
    /// <summary>Toggle d'activation de la teinte (AssetPropertyBoolean).</summary>
    public const string TintToggle = "common_Tint_toggle";

    /// <summary>Couleur de teinte RGBA normalisee 0.0-1.0 (AssetPropertyDoubleArray4d).</summary>
    public const string TintColor = "common_Tint_color";
}
