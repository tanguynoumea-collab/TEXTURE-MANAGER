using System.Collections.ObjectModel;

namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour une scene (sous-ensemble de types Revit).
/// POCO pur -- aucune dependance Revit API (D-01, D-02).
/// </summary>
public class SceneDto
{
    /// <summary>
    /// Version du schema de persistance (DON-03). Les fichiers v0 (sans le champ)
    /// deserialisent avec le defaut 1 — acceptable, pas de logique de migration pour l'instant.
    /// A incrementer a la prochaine rupture de format.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    public string Name { get; set; } = string.Empty;
    public ObservableCollection<SceneTypeDto> Types { get; set; } = new();

    public override string ToString() => Name;
}
