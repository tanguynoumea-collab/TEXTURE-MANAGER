using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Olympe.MaterialManager.Messages;

/// <summary>
/// Message Messenger pour la notification d'edition d'un materiau (D-21).
/// Envoye apres chaque modification reussie pour declencher le rafraichissement
/// de la liste des presets (le nom ou la couleur ont pu changer).
/// Transporte le ElementIdValue (long) du materiau modifie.
/// </summary>
public class MaterialEditedMessage : ValueChangedMessage<long>
{
    public MaterialEditedMessage(long materialIdValue) : base(materialIdValue) { }
}
