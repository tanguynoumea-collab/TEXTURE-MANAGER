using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Olympe.MaterialManager.Messages;

/// <summary>
/// Message Messenger pour rafraichir le panneau central apres Set Mat (D-25).
/// Transporte le TypeIdValue pour re-fetch les couches ou parametres.
/// </summary>
public class RefreshLayersMessage : ValueChangedMessage<long>
{
    public RefreshLayersMessage(long typeIdValue) : base(typeIdValue) { }
}
