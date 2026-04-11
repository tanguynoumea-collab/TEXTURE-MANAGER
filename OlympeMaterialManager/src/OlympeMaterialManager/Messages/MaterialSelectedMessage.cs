using CommunityToolkit.Mvvm.Messaging.Messages;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Messages;

/// <summary>
/// Message Messenger pour la selection d'un materiau preset (D-20).
/// Envoye quand l'utilisateur selectionne un materiau dans le TreeView droit.
/// Transporte un PresetMaterialDto nullable (null = deselection).
/// </summary>
public class MaterialSelectedMessage : ValueChangedMessage<PresetMaterialDto?>
{
    public MaterialSelectedMessage(PresetMaterialDto? value) : base(value) { }
}
