using System;
using System.Collections.Generic;
using System.Reflection;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Presentation;
using MToolKit.ContextMenus.Widgets;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using R3;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuGlyphPromptTests
  {
    [Test]
    public void Widget_requests_glyph_only_on_initial_and_live_binding_and_releases_its_subscription()
    {
      using var fixture = new Fixture();
      using var prompts = new RecordingPrompts();
      var widget = fixture.Widget();
      widget.Bind(new MenuWidgetState(null, MenuActionState.Available, promptService: prompts,
        bindingAction: fixture.Confirm));
      Assert.That(prompts.LastStyle, Is.EqualTo(InputPromptStyle.Glyph));
      Assert.That(widget.BindingHint.text, Does.StartWith("glyph:"));
      prompts.Revision++;
      prompts.Notify();
      Assert.That(widget.BindingHint.text, Does.EndWith(":1"));
      Assert.That(prompts.LastStyle, Is.EqualTo(InputPromptStyle.Glyph));
      widget.Unbind();
      string retired = widget.BindingHint.text;
      prompts.Revision++; prompts.Notify();
      Assert.That(widget.BindingHint.text, Is.EqualTo(retired));
    }

    [Test]
    public void Presenter_primary_and_secondary_snapshots_request_glyphs_from_the_owned_runtime_actions()
    {
      using var fixture = new Fixture();
      using var prompts = new RecordingPrompts();
      var centerPrefab = fixture.Root("Glyph state capture");
      centerPrefab.AddComponent<MenuGlyphCaptureWidget>();
      var profile = fixture.Profile(); profile.CenterPrefab = centerPrefab;
      profile.InputBindings.Configure(submit: fixture.Reference(fixture.AuthoredConfirm),
        secondary: new[] { fixture.Reference(fixture.AuthoredSecondary) });
      var secondary = new MenuAction("auxiliary", new MenuLabel("Auxiliary"),
        new MenuCommandBehavior(_ => { }), bindingHint: fixture.Reference(fixture.AuthoredSecondary));
      var action = new MenuAction("primary", new MenuLabel("Primary"), new MenuCommandBehavior(_ => { }),
        secondaryActions: new[] { secondary }, bindingHint: fixture.Reference(fixture.AuthoredConfirm));
      var presenter = fixture.Presenter<RadialMenuPresenter>(profile, new[] { action }, prompts);
      MenuWidgetState state = null;
      foreach (var capture in presenter.GetComponentsInChildren<MenuGlyphCaptureWidget>()) state = capture.State;
      Assert.That(state, Is.Not.Null);
      Assert.That(state.BindingAction, Is.SameAs(fixture.Confirm));
      Assert.That(state.BindingHint, Does.StartWith("glyph:"));
      Assert.That(state.SecondaryStates.Count, Is.EqualTo(1));
      Assert.That(state.SecondaryStates[0].BindingAction, Is.SameAs(fixture.Secondary));
      Assert.That(state.SecondaryStates[0].BindingHint, Does.StartWith("glyph:"));
      Assert.That(prompts.Styles.Count, Is.GreaterThanOrEqualTo(3));
      foreach (var style in prompts.Styles) Assert.That(style, Is.EqualTo(InputPromptStyle.Glyph));
      Assert.That(fixture.AuthoredConfirm.enabled, Is.False);
    }

    [Test]
    public void Widget_renders_a_native_TMP_sprite_instead_of_the_configured_literal_button_name()
    {
      using var fixture = new Fixture();
      var widget = fixture.BindRealWidget();
      fixture.AssertSprite(widget.BindingHint, "keyboard-enter");
      Assert.That(widget.BindingHint.text, Does.Not.Contain("Literal Enter"));
      fixture.Catalog.SetEntries(Array.Empty<InputGlyphCatalog.Entry>());
      Assert.That(widget.BindingHint.text, Does.Contain("Enter"), "Missing artwork keeps the existing readable binding fallback.");
      Assert.That(widget.BindingHint.text, Does.Not.Contain("<sprite"));
    }

    [Test]
    public void Rebinding_updates_the_live_TMP_glyph_without_changing_the_authored_input_asset()
    {
      using var fixture = new Fixture();
      var widget = fixture.BindRealWidget();
      fixture.AssertSprite(widget.BindingHint, "keyboard-enter");
      fixture.Confirm.ApplyBindingOverride(0, "<Keyboard>/tab");
      fixture.AssertSprite(widget.BindingHint, "keyboard-tab");
      Assert.That(widget.BindingHint.text, Does.Not.Contain("Literal Tab"));
      Assert.That(fixture.AuthoredConfirm.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/enter"));
    }

    [Test]
    public void Relevant_keyboard_and_controller_activity_switches_the_live_native_glyph()
    {
      using var fixture = new Fixture();
      fixture.Prompts.SetContext(null);
      fixture.KeyboardState(new KeyboardState(Key.Enter));
      var widget = fixture.BindRealWidget();
      Assert.That(fixture.Prompts.Context.Device, Is.SameAs(fixture.Keyboard));
      fixture.AssertSprite(widget.BindingHint, "keyboard-enter");
      fixture.KeyboardState(new KeyboardState());
      fixture.GamepadState(new GamepadState().WithButton(GamepadButton.South));
      Assert.That(fixture.Prompts.Context.Device, Is.SameAs(fixture.Pad));
      fixture.AssertSprite(widget.BindingHint, "gamepad-south");
      Assert.That(widget.BindingHint.text, Does.Not.Contain("Literal South"));
    }

    [Test]
    public void Footer_back_and_page_operations_display_their_real_rebindable_glyphs()
    {
      using var fixture = new Fixture();
      var profile = fixture.Profile(); profile.ShowCenter = false; profile.MaximumVisibleItems = 3;
      profile.InputBindings.Configure(back: fixture.Reference(fixture.AuthoredBack),
        pageBackward: fixture.Reference(fixture.AuthoredPrevious), pageForward: fixture.Reference(fixture.AuthoredNext));
      var actions = new MenuAction[9];
      for (int i = 0; i < actions.Length; i++) actions[i] = new MenuAction("action" + i, new MenuLabel("Action " + i), new MenuCommandBehavior(_ => { }));
      var presenter = fixture.Presenter<ListMenuPresenter>(profile, actions);
      Assert.That(fixture.FooterText(presenter, "Back"), Does.Contain(fixture.Tag("keyboard-escape")));
      Assert.That(fixture.FooterText(presenter, "Back"), Does.Contain("Close"), "The operation label remains readable beside its input glyph.");
      Assert.That(fixture.FooterText(presenter, "Previous"), Does.Contain(fixture.Tag("keyboard-page-up")));
      Assert.That(fixture.FooterText(presenter, "Next"), Does.Contain(fixture.Tag("keyboard-page-down")));
      fixture.Back.ApplyBindingOverride(0, "<Keyboard>/tab");
      Assert.That(fixture.FooterText(presenter, "Back"), Does.Contain(fixture.Tag("keyboard-tab")));
      Assert.That(fixture.FooterText(presenter, "Next"), Does.Contain(fixture.Tag("keyboard-page-down")));
    }

    private sealed class RecordingPrompts : IInputPromptService, IDisposable
    {
      private readonly Subject<Unit> changed = new();
      public readonly List<InputPromptStyle> Styles = new();
      public int Revision;
      public InputPromptStyle LastStyle;
      public Observable<Unit> Changed => changed;
      public InputPromptContext Context => default;
      public void Notify() => changed.OnNext(Unit.Default);
      public string Resolve(InputAction action, InputPromptStyle style = InputPromptStyle.Auto, InputPromptContext? context = null)
      {
        LastStyle = style; Styles.Add(style);
        return style == InputPromptStyle.Glyph ? "glyph:" + action?.id + ":" + Revision : "Literal button name";
      }
      public string ResolveReference(InputActionReference action, InputPromptStyle style = InputPromptStyle.Auto, InputPromptContext? context = null)
        => Resolve(action?.action, style, context);
      public string ResolvePath(string path, InputPromptStyle style = InputPromptStyle.Auto, InputPromptContext? context = null) => path;
      public string Localize(string key, string fallback, params object[] arguments) => fallback;
      public string Compose(string key, string fallback, params InputPromptArgument[] arguments) => fallback;
      public IDisposable Bind(TMP_Text text, Func<string> render)
      {
        text.richText = true; text.text = render();
        return changed.Subscribe(_ => { if (text != null) text.text = render(); });
      }
      public void Dispose() => changed.Dispose();
    }

    private sealed class Fixture : IDisposable
    {
      private const string PlayerUpdates = "RUN_PLAYER_UPDATES_IN_EDIT_MODE";
      private readonly List<Object> objects = new();
      private readonly List<MenuActionWidget> widgets = new();
      private readonly InputSettings inputSettings = InputSystem.settings;
      private readonly InputSettings.UpdateMode previousMode;
      private readonly bool previousFeature;
      private readonly Keyboard previousKeyboard = UnityEngine.InputSystem.Keyboard.current;
      private readonly Gamepad previousPad = Gamepad.current;
      private readonly InputActionAsset authored, runtime;
      private readonly TMP_SpriteAsset sprites;
      private readonly Canvas canvas;
      private readonly List<IContextMenuPresenter> presenters = new();
      private readonly List<MenuPresentationContext> contexts = new();
      private readonly List<MenuSession> sessions = new();
      public Keyboard Keyboard { get; }
      public Gamepad Pad { get; }
      public InputPromptService Prompts { get; }
      public InputGlyphCatalog Catalog { get; }
      public InputAction AuthoredConfirm { get; }
      public InputAction AuthoredSecondary { get; }
      public InputAction AuthoredBack { get; }
      public InputAction AuthoredPrevious { get; }
      public InputAction AuthoredNext { get; }
      public InputAction Confirm => runtime.FindAction(AuthoredConfirm.id);
      public InputAction Secondary => runtime.FindAction(AuthoredSecondary.id);
      public InputAction Back => runtime.FindAction(AuthoredBack.id);

      public Fixture()
      {
        previousMode = inputSettings.updateMode;
        previousFeature = (bool)typeof(InputSettings).GetMethod("IsFeatureEnabled", BindingFlags.Instance | BindingFlags.NonPublic)
          .Invoke(inputSettings, new object[] { PlayerUpdates });
        try
        {
          inputSettings.SetInternalFeatureFlag(PlayerUpdates, true);
          inputSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
          Keyboard = InputSystem.AddDevice<Keyboard>(); Pad = InputSystem.AddDevice<Gamepad>();
          authored = Own(ScriptableObject.CreateInstance<InputActionAsset>());
          var map = authored.AddActionMap("Menu");
          InputAction Action(string name, string key, string pad)
          {
            var action = map.AddAction(name, InputActionType.Button);
            action.AddBinding("<Keyboard>/" + key, groups: "Keyboard&Mouse");
            action.AddBinding("<Gamepad>/" + pad, groups: "Controller");
            return action;
          }
          AuthoredConfirm = Action("Submit", "enter", "buttonSouth");
          AuthoredSecondary = Action("Secondary", "tab", "buttonWest");
          AuthoredBack = Action("Back", "escape", "buttonEast");
          AuthoredPrevious = Action("Previous", "pageUp", "leftShoulder");
          AuthoredNext = Action("Next", "pageDown", "rightShoulder");
          runtime = Own(Object.Instantiate(authored)); runtime.devices = new InputDevice[] { Keyboard, Pad }; runtime.Enable();
          foreach (var action in runtime) _ = action.controls.Count;
          sprites = SpriteAsset();
          Catalog = Own(ScriptableObject.CreateInstance<InputGlyphCatalog>());
          var entries = new List<InputGlyphCatalog.Entry>();
          void Entry(InputGlyphFamily family, string path, string name, string literal)
            => entries.Add(new InputGlyphCatalog.Entry { Family = family, ControlPath = path, SpriteAsset = sprites, SpriteName = name, DisplayName = literal });
          Entry(InputGlyphFamily.KeyboardAndMouse, "enter", "keyboard-enter", "Literal Enter");
          Entry(InputGlyphFamily.KeyboardAndMouse, "tab", "keyboard-tab", "Literal Tab");
          Entry(InputGlyphFamily.KeyboardAndMouse, "escape", "keyboard-escape", "Literal Escape");
          Entry(InputGlyphFamily.KeyboardAndMouse, "pageUp", "keyboard-page-up", "Literal Page Up");
          Entry(InputGlyphFamily.KeyboardAndMouse, "pageDown", "keyboard-page-down", "Literal Page Down");
          Entry(InputGlyphFamily.Generic, "buttonSouth", "gamepad-south", "Literal South");
          Catalog.SetEntries(entries.ToArray());
          var settings = Own(ScriptableObject.CreateInstance<InputPromptSettings>());
          settings.Configure(Catalog, InputPromptStyle.LocalizedName);
          Prompts = new InputPromptService(settings, actions: runtime);
          Prompts.SetContext(new InputPromptContext(device: Keyboard, bindingGroup: "Keyboard&Mouse"));
          var owner = Root("Glyph menu canvas"); canvas = owner.AddComponent<Canvas>();
          canvas.renderMode = RenderMode.WorldSpace; ((RectTransform)owner.transform).sizeDelta = new Vector2(1200, 900);
        }
        catch { Dispose(); throw; }
      }

      private T Own<T>(T value) where T : Object { objects.Add(value); return value; }
      public GameObject Root(string name, Transform parent = null)
      {
        var root = Own(new GameObject(name, typeof(RectTransform)));
        if (parent != null) root.transform.SetParent(parent, false);
        return root;
      }
      public MenuProfile Profile() => Own(ScriptableObject.CreateInstance<MenuProfile>());
      public InputActionReference Reference(InputAction action) => Own(InputActionReference.Create(action));
      public MenuActionWidget Widget()
      {
        var root = Root("Bound glyph widget", canvas.transform);
        var widget = root.AddComponent<MenuActionWidget>(); widgets.Add(widget); widget.UseSkin = false;
        var hint = Root("Binding glyph", root.transform).AddComponent<TextMeshProUGUI>();
        hint.font = TMP_Settings.defaultFontAsset;
        Assert.That(hint.font, Is.Not.Null, "Native glyph rendering requires the project's installed TMP Essentials font.");
        hint.rectTransform.sizeDelta = new Vector2(300, 80); hint.fontSize = 20;
        widget.BindingHint = hint;
        return widget;
      }
      public MenuActionWidget BindRealWidget()
      {
        var widget = Widget();
        widget.Bind(new MenuWidgetState(null, MenuActionState.Available, promptService: Prompts, bindingAction: Confirm));
        return widget;
      }
      public T Presenter<T>(MenuProfile profile, IReadOnlyList<MenuAction> actions, IInputPromptService prompts = null) where T : Component, IContextMenuPresenter
      {
        var presenter = Root("Glyph presenter", canvas.transform).AddComponent<T>(); presenters.Add(presenter);
        var session = new MenuSession(); sessions.Add(session); session.Open(actions, new MenuContext());
        var context = new MenuPresentationContext(canvas, Vector2.zero, prompts: prompts ?? Prompts,
          labelOverride: label => label.Fallback, resolveInput: action => runtime.FindAction(action.id));
        contexts.Add(context); presenter.Bind(session, profile, context); return presenter;
      }
      public string Tag(string name) => "<sprite=\"" + sprites.name + "\" name=\"" + name + "\">";
      public void AssertSprite(TMP_Text text, string name)
      {
        Assert.That(text.text, Is.EqualTo(Tag(name)));
        Assert.That(text.richText, Is.True);
        text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        // ugui bb329a87fcdc overwrites its local sprite count with an unassigned aggregate.
        // Inspect actual native parsed elements and generated geometry instead.
        int spriteCount = 0;
        TMP_CharacterInfo rendered = default;
        for (int i = 0; i < text.textInfo.characterCount; i++)
        {
          var character = text.textInfo.characterInfo[i];
          if (character.elementType != TMP_TextElementType.Sprite) continue;
          spriteCount++; rendered = character;
        }
        Assert.That(spriteCount, Is.EqualTo(1), "The real TMP parser must produce a sprite, not literal tag text.");
        Assert.That(rendered.textElement, Is.InstanceOf<TMP_SpriteCharacter>());
        Assert.That(rendered.textElement.textAsset, Is.SameAs(sprites));
        Assert.That(((TMP_SpriteCharacter)rendered.textElement).name, Is.EqualTo(name));
        Assert.That(text.textInfo.meshInfo[rendered.materialReferenceIndex].material, Is.SameAs(sprites.material));
        float area = Vector3.Cross(rendered.topRight - rendered.topLeft, rendered.bottomLeft - rendered.topLeft).magnitude;
        Assert.That(area, Is.GreaterThan(0), "The parsed glyph must generate a nondegenerate native sprite quad.");
      }
      public string FooterText(Component presenter, string operation)
      {
        foreach (var button in presenter.GetComponentsInChildren<Button>(true))
        {
          if (button.gameObject.name != operation) continue;
          var values = new List<string>();
          foreach (var text in button.GetComponentsInChildren<TMP_Text>(true)) values.Add(text.text);
          return string.Join(" ", values);
        }
        Assert.Fail("Missing footer operation: " + operation); return null;
      }
      public void KeyboardState(KeyboardState value) { InputSystem.QueueStateEvent(Keyboard, value); InputSystem.Update(); }
      public void GamepadState(GamepadState value) { InputSystem.QueueStateEvent(Pad, value); InputSystem.Update(); }
      private TMP_SpriteAsset SpriteAsset()
      {
        var asset = Own(ScriptableObject.CreateInstance<TMP_SpriteAsset>()); asset.name = "MTK_MenuGlyphTest_" + Guid.NewGuid().ToString("N");
        var serialized = new SerializedObject(asset); serialized.FindProperty("m_Version").stringValue = "1.1.0";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var texture = Own(new Texture2D(2, 2)); texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); texture.Apply();
        asset.spriteSheet = texture;
        var shader = Shader.Find("TextMeshPro/Sprite"); Assert.That(shader, Is.Not.Null);
        asset.material = Own(new Material(shader)); asset.material.mainTexture = texture;
        var sprite = Own(Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero));
        string[] names = { "keyboard-enter", "keyboard-tab", "keyboard-escape", "keyboard-page-up", "keyboard-page-down", "gamepad-south" };
        for (int i = 0; i < names.Length; i++)
        {
          var glyph = new TMP_SpriteGlyph((uint)i, new UnityEngine.TextCore.GlyphMetrics(2, 2, 0, 2, 2),
            new UnityEngine.TextCore.GlyphRect(0, 0, 2, 2), 1, 0, sprite);
          asset.spriteGlyphTable.Add(glyph); asset.spriteCharacterTable.Add(new TMP_SpriteCharacter((uint)(0xe000 + i), glyph) { name = names[i] });
        }
        asset.UpdateLookupTables(); return asset;
      }
      public void Dispose()
      {
        try
        {
          foreach (var presenter in presenters) if (presenter is Object obj && obj != null) presenter.Unbind();
          foreach (var widget in widgets) if (widget != null) widget.Unbind();
          foreach (var context in contexts) context.Dispose();
          foreach (var session in sessions) session.Dispose();
          Prompts?.Dispose(); if (runtime != null) runtime.Disable();
          for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
          objects.Clear();
          if (Keyboard != null && Keyboard.added) InputSystem.RemoveDevice(Keyboard);
          if (Pad != null && Pad.added) InputSystem.RemoveDevice(Pad);
        }
        finally
        {
          inputSettings.SetInternalFeatureFlag(PlayerUpdates, previousFeature); inputSettings.updateMode = previousMode;
          if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
          if (previousPad != null && previousPad.added) previousPad.MakeCurrent();
        }
      }
    }
  }

  public sealed class MenuGlyphCaptureWidget : MonoBehaviour, IMenuWidget
  {
    public MenuWidgetState State { get; private set; }
    public void Bind(MenuWidgetState state) => State = state;
    public void Unbind() => State = null;
  }
}
