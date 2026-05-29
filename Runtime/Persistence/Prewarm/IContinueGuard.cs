using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Navigation.Interfaces;

namespace MToolKit.Runtime.Persistence.Prewarm
{
  /// <summary>
  ///   Optional gate invoked by the main menu's Continue / LoadGame flow before the scene swap.
  ///   Implementations inspect the target save via <see cref="ISavePrewarmChecker"/>, surface any
  ///   compatibility issues to the user via a modal, and return the user's decision. See ADR-0012.
  ///
  ///   <para>
  ///   The interface lives in MToolKit so the menu view (in TemplateGame asmdef) can [Inject] it
  ///   without taking a dependency on game-specific UI types — the heavy lifting (modal layout,
  ///   localized strings, scroll-list rendering) lives in the game asmdef where Dirigible.UI is
  ///   visible. When no implementation is registered, the menu treats the guard as absent and
  ///   proceeds with the load — back-compat for headless/test/template scenes.
  ///   </para>
  ///
  ///   <para>
  ///   The caller passes its own already-resolved <see cref="IModalService"/> and
  ///   <see cref="INavigationService"/> in. We can't resolve them inside the guard's factory
  ///   because the guard is registered in the global scope but those services live in
  ///   NavigationInstaller's child scope (cross-scope plumbing tracked in TECHNICAL_DEBT.md);
  ///   passing them in lets the gate work from any scope that can already inject them.
  ///   </para>
  /// </summary>
  public interface IContinueGuard
  {
    /// <summary>
    ///   Inspect <paramref name="profileName"/> for compatibility issues and, if any are found,
    ///   show the user a modal asking whether to proceed. Returns <c>true</c> to allow the load to
    ///   continue, <c>false</c> to cancel (caller should return without triggering the scene swap).
    ///   When no issues are found, returns <c>true</c> immediately without showing any modal.
    /// </summary>
    UniTask<bool> CanContinueAsync(string profileName, IModalService modalService, INavigationService navigationService);
  }
}
