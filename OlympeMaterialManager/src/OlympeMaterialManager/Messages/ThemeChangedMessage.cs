using CommunityToolkit.Mvvm.Messaging.Messages;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Messages;

/// <summary>
/// Message Messenger diffusé quand le jeu de couleurs change (cycle 4).
/// Émis par ThemeStore après persistance et application. Les vues n'en ont pas
/// besoin pour se repeindre (DynamicResource s'en charge) : il existe pour les
/// consommateurs qui doivent réagir autrement qu'en couleur (glyphe de
/// destination du bouton de bascule, futurs rendus bitmap).
/// </summary>
public class ThemeChangedMessage : ValueChangedMessage<AppTheme>
{
    public ThemeChangedMessage(AppTheme value) : base(value) { }
}
