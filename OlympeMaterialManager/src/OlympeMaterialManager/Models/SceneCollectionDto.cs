using System.Collections.ObjectModel;

namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO racine pour la serialisation JSON des scenes actives.
/// </summary>
public class SceneCollectionDto
{
    public ObservableCollection<SceneDto> Scenes { get; set; } = new();
}
