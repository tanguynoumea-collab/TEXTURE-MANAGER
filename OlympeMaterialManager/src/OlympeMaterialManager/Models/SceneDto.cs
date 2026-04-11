using System.Collections.ObjectModel;

namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour une scene (sous-ensemble de types Revit).
/// POCO pur -- aucune dependance Revit API (D-01, D-02).
/// </summary>
public class SceneDto
{
    public string Name { get; set; } = string.Empty;
    public ObservableCollection<SceneTypeDto> Types { get; set; } = new();

    public override string ToString() => Name;
}
