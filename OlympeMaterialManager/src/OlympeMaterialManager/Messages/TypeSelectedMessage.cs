using CommunityToolkit.Mvvm.Messaging.Messages;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Messages;

/// <summary>
/// Message Messenger pour la communication LeftPanel -> CenterPanel.
/// Envoye quand l'utilisateur selectionne un type dans le TreeView (D-19, D-20).
/// </summary>
public class TypeSelectedMessage : ValueChangedMessage<SceneTypeDto?>
{
    public TypeSelectedMessage(SceneTypeDto? value) : base(value) { }
}
