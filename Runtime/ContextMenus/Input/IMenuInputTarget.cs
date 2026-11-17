using UnityEngine;

namespace MToolKit.ContextMenus.Input
{
  /// <summary>Optional pointer-origin operations; legacy and custom targets retain the original semantic contract.</summary>
  public interface IMenuPointerConfirmationTarget
  {
    void ConfirmPointer();
    void EndPointerHold();
  }

  /// <summary>Semantic menu operations. The target coordinates the session and its current presenter.</summary>
  public interface IMenuInputTarget
  {
    void Navigate(Vector2 direction, bool analog);
    void Point(Vector2 screenPosition);
    void Confirm();
    void Back();
    void Cancel();
    void InvokeSecondary(int index);
    void Page(int direction);
    void SwitchRing();
    void BeginHold();
    void EndHold();
  }
}
