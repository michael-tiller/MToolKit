using System;
using R3;
using TMPro;
using UnityEngine.InputSystem;

namespace MToolKit.Runtime.Input.Prompts
{
  public interface IInputActionSource
  {
    InputActionAsset RuntimeActions { get; }
    InputAction ResolveRuntimeAction(InputAction action);
  }

  public interface IInputBindingChangeSource
  {
    Observable<(InputAction action, int bindingIndex, string newPath)> BindingChanges { get; }
  }

  public interface IInputPromptService
  {
    Observable<Unit> Changed { get; }
    InputPromptContext Context { get; }
    string Resolve(InputAction action, InputPromptStyle style = InputPromptStyle.Auto, InputPromptContext? context = null);
    string ResolveReference(InputActionReference action, InputPromptStyle style = InputPromptStyle.Auto, InputPromptContext? context = null);
    string ResolvePath(string path, InputPromptStyle style = InputPromptStyle.Auto, InputPromptContext? context = null);
    string Localize(string key, string fallback, params object[] arguments);
    string Compose(string key, string fallback, params InputPromptArgument[] arguments);
    IDisposable Bind(TMP_Text text, Func<string> render);
  }

  /// <summary>Optional actual-device presentation; action-only service implementations remain compatible.</summary>
  public interface IInputDevicePromptService : IInputPromptService
  {
    string ResolveDevice(InputPromptStyle style = InputPromptStyle.Auto, InputPromptContext? context = null);
  }

  /// <summary>Named input or escaped plain text for Unity Localization Smart Strings.</summary>
  public readonly struct InputPromptArgument
  {
    public InputPromptArgument(string name, InputAction action, InputPromptStyle style = InputPromptStyle.Auto,
      InputPromptContext? context = null)
    { Name = name; Action = action; Style = style; Context = context; Text = null; IsText = false; }
    private InputPromptArgument(string name, string text)
    { Name = name; Text = text; Action = null; Style = InputPromptStyle.Auto; Context = null; IsText = true; }
    public static InputPromptArgument PlainText(string name, string text) => new(name, text);
    public string Name { get; }
    public InputAction Action { get; }
    public InputPromptStyle Style { get; }
    public InputPromptContext? Context { get; }
    public string Text { get; }
    public bool IsText { get; }
  }
}
