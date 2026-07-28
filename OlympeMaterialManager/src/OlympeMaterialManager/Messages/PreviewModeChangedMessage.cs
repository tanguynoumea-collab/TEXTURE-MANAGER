using CommunityToolkit.Mvvm.Messaging.Messages;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Messages;

/// <summary>
/// Message Messenger diffusé quand le mode d'aperçu des matériaux change (B10).
/// Émis par PreviewModeStore après persistance ; les consommateurs UI peuvent
/// aussi se binder directement sur PreviewModeStore.CurrentMode (INPC).
/// </summary>
public class PreviewModeChangedMessage : ValueChangedMessage<PreviewMode>
{
    public PreviewModeChangedMessage(PreviewMode value) : base(value) { }
}
