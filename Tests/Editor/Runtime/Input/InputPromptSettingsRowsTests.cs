using System;
using System.Collections.Generic;
using System.Reflection;
using MToolKit.Runtime.Components;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.Runtime.Input
{
  public sealed class InputPromptSettingsRowsTests
  {
    private GameObject root;
    private InputActionAsset asset;
    private InputAction action;
    private RowPrompts prompts;
    private InputActionReference reference;

    [SetUp]
    public void SetUp()
    {
      root = new GameObject("Input prompt settings rows");
      root.SetActive(false);
      asset = ScriptableObject.CreateInstance<InputActionAsset>();
      action = asset.AddActionMap("Player").AddAction("Pause");
      action.AddBinding("<Keyboard>/enter");
      action.AddBinding("<Keyboard>/space");
      action.AddBinding("<Gamepad>/buttonNorth");
      reference = InputActionReference.Create(action);
      prompts = new RowPrompts();
      prompts.Localizer.Value = "Entrée";
    }

    [TearDown]
    public void TearDown()
    {
      foreach (var row in root.GetComponents<MonoBehaviour>())
        Lifecycle(row, "OnDisable");
      Object.DestroyImmediate(root);
      prompts.Dispose();
      Object.DestroyImmediate(reference);
      Object.DestroyImmediate(asset);
    }

    [Test]
    public void ActionRow_LocalizesExplicitSlotsAndRefreshesWithoutUiReconstruction()
    {
      var row = root.AddComponent<KeybindListElement>();
      Configure(row, "TextKeybind", "TextButton", "TextButtonAlt", "TextButtonGamepad");
      ConfigureIcons(row, "IconButton", "IconButtonAlt", "IconButtonGamepad");
      row.Initialize(reference, inputPrompts: prompts);
      Enable(row);
      Assert.That(row.TextButton.text, Is.EqualTo("Entrée"));
      Assert.That(prompts.SelectedBindings, Does.Contain(0));
      Assert.That(prompts.SelectedBindings, Does.Contain(1));
      Assert.That(prompts.SelectedBindings, Does.Contain(2));
      Assert.That(action.enabled, Is.False);
      var sameText = row.TextButton;
      prompts.Localizer.Value = "Enter";
      prompts.Notify();
      Assert.That(row.TextButton, Is.SameAs(sameText));
      Assert.That(row.TextButton.text, Is.EqualTo("Enter"));
      Disable(row);
      prompts.Localizer.Value = "Entrée";
      prompts.Notify();
      Assert.That(row.TextButton.text, Is.EqualTo("Enter"), "Disabled rows release their prompt subscription.");
      Enable(row);
      Assert.That(row.TextButton.text, Is.EqualTo("Entrée"));
    }

    [Test]
    public void GroupedRow_LocalizesSelectedBindingAndReleasesSubscriptions()
    {
      var row = root.AddComponent<BindingComponentElement>();
      Configure(row, "TextComponentName", "TextPrimary", "TextSecondary", "TextGamepad");
      ConfigureIcons(row, "IconPrimary", "IconSecondary", "IconGamepad");
      var component = new CompositeBindingHelper.BindingComponent { Action = action, Name = "Pause", DisplayName = "Pause" };
      for (int index = 0; index < 3; index++)
        component.Slots.Add(new CompositeBindingHelper.BindingSlot
        {
          BindingIndex = index, Binding = action.bindings[index], IsGamepad = index == 2
        });
      row.Initialize(component, inputPrompts: prompts);
      Enable(row);
      Assert.That(row.TextPrimary.text, Is.EqualTo("Entrée"));
      Assert.That(row.TextGamepad.text, Is.EqualTo("North"));
      prompts.Localizer.Value = "Enter";
      prompts.Notify();
      Assert.That(row.TextPrimary.text, Is.EqualTo("Enter"));
      Disable(row);
      prompts.Localizer.Value = "Entrée";
      prompts.Notify();
      Assert.That(row.TextPrimary.text, Is.EqualTo("Enter"));
    }

    [Test]
    public void ExplicitLegacyDisplayOptions_RetainNativeDeviceAndOverrideSemantics()
    {
      var row = root.AddComponent<KeybindListElement>();
      Configure(row, "TextKeybind", "TextButton", "TextButtonAlt", "TextButtonGamepad");
      ConfigureIcons(row, "IconButton", "IconButtonAlt", "IconButtonGamepad");
      const InputBinding.DisplayStringOptions options = InputBinding.DisplayStringOptions.DontOmitDevice
        | InputBinding.DisplayStringOptions.IgnoreBindingOverrides | InputBinding.DisplayStringOptions.DontUseShortDisplayNames;
      Set(row, "DisplayStringOptions", options);
      action.ApplyBindingOverride(0, "<Keyboard>/f");
      row.Initialize(reference, inputPrompts: prompts);
      Assert.That(row.TextButton.text, Is.EqualTo(action.GetBindingDisplayString(0, options)));
    }

    [Test]
    public void BindingRows_PreserveTheServicesExplicitPlatformOverride()
    {
      prompts.Platform = InputGlyphPlatform.MacOS;
      var row = root.AddComponent<KeybindListElement>();
      Configure(row, "TextKeybind", "TextButton", "TextButtonAlt", "TextButtonGamepad");
      ConfigureIcons(row, "IconButton", "IconButtonAlt", "IconButtonGamepad");
      row.Initialize(reference, inputPrompts: prompts);
      Enable(row);
      Assert.That(prompts.SelectedPlatforms, Is.Not.Empty);
      Assert.That(prompts.SelectedPlatforms, Is.All.EqualTo(InputGlyphPlatform.MacOS),
        "The shared binding-row adapter must retain the caller's OS preview context.");
    }

    private void Configure(Component row, params string[] properties)
    {
      foreach (string property in properties)
      {
        var child = new GameObject(property, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        child.transform.SetParent(root.transform, false);
        Set(row, property, child.GetComponent<TextMeshProUGUI>());
      }
    }

    private void ConfigureIcons(Component row, params string[] properties)
    {
      foreach (string property in properties)
      {
        var child = new GameObject(property, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(root.transform, false);
        Set(row, property, child.GetComponent<Image>());
      }
    }

    private static void Set(object target, string name, object value)
      => target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance).SetValue(target, value);

    private void Enable(Component row)
    {
      root.SetActive(true);
      // Ordinary MonoBehaviours do not consistently receive runtime lifecycle events in
      // synchronous EditMode tests. Exercise the actual methods, as the audio UI tests do.
      // Normalize a possible automatic callback before simulating this enable exactly once.
      Lifecycle(row, "OnDisable");
      Lifecycle(row, "OnEnable");
    }

    private void Disable(Component row)
    {
      root.SetActive(false);
      Lifecycle(row, "OnDisable");
    }

    private static void Lifecycle(Component row, string method)
      => row.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(row, null);

    private sealed class RowPrompts : IInputPromptService, IDisposable
    {
      private readonly Subject<Unit> changed = new();
      public readonly KeyName Localizer = new();
      public readonly List<int> SelectedBindings = new();
      public readonly List<InputGlyphPlatform?> SelectedPlatforms = new();
      public InputGlyphPlatform? Platform;
      public Observable<Unit> Changed => changed;
      // Row tests exercise localization/slot/lifecycle projection, independently of
      // whichever real controller happens to be connected to the test host.
      public InputPromptContext Context => new InputPromptContext(familyOverride: InputGlyphFamily.Generic).WithPlatform(Platform);
      public void Notify() => changed.OnNext(Unit.Default);
      public string Resolve(InputAction action, InputPromptStyle style = InputPromptStyle.Auto, InputPromptContext? context = null)
      {
        if (context?.BindingIndex != null) SelectedBindings.Add(context.Value.BindingIndex.Value);
        SelectedPlatforms.Add(context?.PlatformOverride);
        return new InputPromptResolver(localizer: Localizer).Resolve(action, style, context ?? Context);
      }
      public string ResolveReference(InputActionReference input, InputPromptStyle style = InputPromptStyle.Auto,
        InputPromptContext? context = null) => Resolve(input?.action, style, context);
      public string ResolvePath(string path, InputPromptStyle style = InputPromptStyle.Auto, InputPromptContext? context = null)
        => new InputPromptResolver(localizer: Localizer).ResolvePath(path, style, context ?? Context);
      public string Localize(string key, string fallback, params object[] arguments) => fallback;
      public string Compose(string key, string fallback, params InputPromptArgument[] arguments) => fallback;
      public IDisposable Bind(TMP_Text text, Func<string> render) => Disposable.Empty;
      public void Dispose() => changed.Dispose();
    }

    private sealed class KeyName : IInputPromptLocalizer
    {
      public string Value;
      public string GetName(string key, string fallback) => key == "input.key.enter" ? Value : fallback;
    }
  }
}
