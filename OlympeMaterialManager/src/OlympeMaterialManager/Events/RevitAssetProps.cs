using Autodesk.Revit.DB.Visual;

namespace Olympe.MaterialManager.Events;

/// <summary>
/// Noms des AssetProperty du schema d'apparence generique Revit utilises par le bridge (MAINT-08).
/// Point de verite unique pour que la lecture (GetMaterialDetails) et l'ecriture
/// (EditMaterialTint) ne puissent pas diverger.
/// ADK2-01 : les valeurs proviennent des constantes typees de l'API
/// Autodesk.Revit.DB.Visual (existence prouvee par sonde metadata sur les
/// assemblies 2023.1.80 / 2024.3.30 / 2025.0.2 / 2026.4.0) plutot que de
/// litteraux magiques. Les proprietes common_Tint_* n'ont pas de classe
/// porteuse neutre : elles sont exposees a l'identique par chaque classe de
/// schema (Generic, Ceramic, Concrete...) — Generic sert de source.
/// </summary>
internal static class RevitAssetProps
{
    /// <summary>Toggle d'activation de la teinte (AssetPropertyBoolean).</summary>
    public static readonly string TintToggle = Generic.CommonTintToggle;

    /// <summary>Couleur de teinte RGBA normalisee 0.0-1.0 (AssetPropertyDoubleArray4d).</summary>
    public static readonly string TintColor = Generic.CommonTintColor;

    /// <summary>Couleur diffuse du schema generique, RGBA normalisee 0.0-1.0
    /// (AssetPropertyDoubleArray4d, DR2-1 : couleur d'apparence du mode Réaliste).</summary>
    public static readonly string GenericDiffuse = Generic.GenericDiffuse;

    /// <summary>Chemin du bitmap d'un asset UnifiedBitmap (AssetPropertyString, B10-TX).</summary>
    public static readonly string UnifiedBitmapPath = UnifiedBitmap.UnifiedbitmapBitmap;
}
