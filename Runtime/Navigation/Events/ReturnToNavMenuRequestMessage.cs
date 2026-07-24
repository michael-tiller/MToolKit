// Navigation/Events/ReturnToNavMenuRequestMessage.cs

using MToolKit.Runtime.Navigation.Interfaces;

namespace MToolKit.Runtime.Navigation.Events
{
  /// <summary>
  ///   Request to leave gameplay and return to the navigation menu scene. Handled by
  ///   <see cref="NavigationSystem" />: loads the configured MenuSceneReference in Single
  ///   mode, which unloads the game scene so a subsequent game load starts from a clean
  ///   slate (idempotent loading of new profile data). Publishers are responsible for any
  ///   pre-departure work (e.g. auto-save) BEFORE publishing.
  /// </summary>
  public readonly struct ReturnToNavMenuRequestMessage : INavigationMessage { }
}
