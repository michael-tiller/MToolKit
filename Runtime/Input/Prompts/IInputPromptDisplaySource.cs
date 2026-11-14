namespace MToolKit.Runtime.Input.Prompts
{
  /// <summary>Scoped adapter for views that identify runtime actions by name.</summary>
  /// <remarks>The provider preserves its own player/action context; explicit styles override shared defaults.</remarks>
  public interface IInputPromptDisplaySource
  {
    /// <summary>Returns the named action's TMP-compatible presentation in the requested style.</summary>
    string GetDisplayString(string actionName, InputPromptStyle style);
  }

  /// <summary>Optional per-view policy for actions without an eligible binding in the current context.</summary>
  public interface IInputPromptFallbackDisplaySource : IInputPromptDisplaySource
  {
    string GetDisplayString(string actionName, InputPromptStyle style, InputPromptUnboundBehavior unboundBehavior);
  }
}
