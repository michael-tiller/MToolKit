using System;
using System.Collections.Generic;
using System.Linq;
using MToolKit.ContextMenus.Configuration;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using R3;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MToolKit.ContextMenus.Samples.Tests.Editor
{
  public sealed class MenuGalleryHudTests
  {
    private static readonly string[] Labels = { "Open", "Hold", "Confirm", "Back", "Ring", "Variant", "Theme", "Next style" };
    private static readonly string[] Paths = { "Shared/OpenContext", "Shared/HoldWheel", "Menu/Submit", "Menu/Back",
      "Menu/SwitchRing", "Menu/Secondary", "Shared/Theme", "Shared/NextStyle" };

    [Test]
    public void Supplied_gallery_has_top_right_status_and_eight_bottom_right_label_glyph_rows_for_all_styles()
    {
      string path = SuppliedScene();
      var scene = EditorSceneManager.OpenPreviewScene(path);
      try
      {
        var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MenuGalleryController>(true)).Single();
        var hud = controller.GetComponentInChildren<MenuGalleryCommandHud>(true);
        Assert.That(hud, Is.Not.Null, "The supplied gallery needs one reusable command HUD, shared by all four presentations.");
        Assert.That(controller.Status, Is.Not.Null);
        Assert.That(controller.Status.rectTransform.anchorMin, Is.EqualTo(Vector2.one), "Debug/status belongs at the top right.");
        Assert.That(controller.Status.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
        Assert.That(controller.Status.rectTransform.pivot, Is.EqualTo(Vector2.one));
        Assert.That(controller.Status.horizontalAlignment, Is.EqualTo(HorizontalAlignmentOptions.Right));
        var frame = (RectTransform)hud.transform;
        Assert.That(frame.anchorMin, Is.EqualTo(Vector2.right), "Commands belong at the bottom right.");
        Assert.That(frame.anchorMax, Is.EqualTo(Vector2.right));
        Assert.That(frame.pivot, Is.EqualTo(Vector2.right));
        Assert.That(frame.anchoredPosition.x, Is.LessThanOrEqualTo(0));
        Assert.That(frame.anchoredPosition.y, Is.GreaterThanOrEqualTo(0));
        Assert.That(hud.GetComponent<VerticalLayoutGroup>(), Is.Not.Null, "Use native vertical row layout.");
        Assert.That(hud.Rows.Select(row => row.Label), Is.EqualTo(Labels));
        Assert.That(hud.Rows.Select(row => row.ActionPath), Is.EqualTo(Paths));
        foreach (var row in hud.Rows)
        {
          Assert.That(row.LabelTarget, Is.Not.Null, row.Label);
          Assert.That(row.GlyphTarget, Is.Not.Null, row.Label);
          Assert.That(row.LabelTarget, Is.Not.SameAs(row.GlyphTarget), "Action words and native glyphs need independent columns.");
          Assert.That(row.LabelTarget.horizontalAlignment, Is.EqualTo(HorizontalAlignmentOptions.Right), row.Label);
          Assert.That(row.LabelTarget.transform.parent, Is.SameAs(row.GlyphTarget.transform.parent), row.Label);
          Assert.That(row.LabelTarget.transform.parent.GetComponent<HorizontalLayoutGroup>(), Is.Not.Null, row.Label);
          Assert.That(row.LabelTarget.GetComponent<LayoutElement>().flexibleWidth, Is.GreaterThan(0), row.Label);
          Assert.That(row.GlyphTarget.GetComponent<LayoutElement>().minWidth, Is.GreaterThan(0), row.Label);
        }
        Assert.That(hud.Rows.Single(row => row.ActionPath == "Menu/Back").Button, Is.Not.Null,
          "The single global Back row retains pointer navigation.");
        Assert.That(controller.Hints == null || !controller.Hints.gameObject.activeSelf, Is.True,
          "The previous combined hint paragraph must not remain as duplicate instructions.");
        var showBack = typeof(MenuProfile).GetProperty("ShowBackControl");
        Assert.That(showBack, Is.Not.Null, "The framework supports suppressing only a duplicate local Back control.");
        Assert.That(controller.HostConfig.Profiles.Select(named => named.Name),
          Is.EquivalentTo(new[] { "WheelContextMenu", "NestedWheelContextMenu", "PieContextMenu", "ListContextMenu" }));
        foreach (var named in controller.HostConfig.Profiles)
          Assert.That(showBack.GetValue(named.Profile), Is.False, named.Name + ": global Back must be the only Back instruction.");
      }
      finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void Hud_uses_injected_service_runtime_actions_and_refreshable_glyph_bindings_then_releases_them()
    {
      var root = new GameObject("Owned command HUD", typeof(RectTransform), typeof(MenuGalleryCommandHud));
      var actions = ScriptableObject.CreateInstance<InputActionAsset>();
      using var prompts = new RecordingPrompts();
      try
      {
        var hud = root.GetComponent<MenuGalleryCommandHud>();
        hud.Rows = CreateRows(root.transform);
        AddActions(actions);
        hud.Bind(prompts, actions, () => { });
        Assert.That(prompts.ActiveBindings, Is.EqualTo(8), "Each real glyph target owns a prompt binding.");
        Assert.That(prompts.ResolvedActions, Is.EquivalentTo(Paths.Select(path => actions.FindAction(path, true))),
          "Resolve the actual execution asset's actions; do not fall back to authored or unrelated global action instances.");
        Assert.That(prompts.Styles.All(style => style == InputPromptStyle.Glyph), Is.True);
        Assert.That(hud.Rows.Select(row => row.LabelTarget.text), Is.EqualTo(Labels));
        foreach (var row in hud.Rows)
          Assert.That(row.GlyphTarget.text, Does.StartWith("<sprite"), row.Label + ": glyph-only output comes from the injected service.");
        string before = hud.Rows[0].GlyphTarget.text;
        actions.FindAction(Paths[0], true).ApplyBindingOverride(0, "<Keyboard>/z");
        prompts.Refresh();
        Assert.That(hud.Rows[0].GlyphTarget.text, Is.Not.EqualTo(before), "An existing service refresh must update the displayed glyph.");
        Assert.That(prompts.ObservedPaths[actions.FindAction(Paths[0], true)], Is.EqualTo("<Keyboard>/z"));
        hud.Unbind();
        Assert.That(prompts.ActiveBindings, Is.Zero, "Closing/rebinding the HUD must release all service observers.");
        Assert.That(prompts.Disposed, Is.False, "The HUD does not own an injected prompt service.");
      }
      finally { Object.DestroyImmediate(root); Object.DestroyImmediate(actions); }
    }

    [Test]
    public void Global_Back_pointer_row_invokes_semantic_Back_once_after_rebind_and_stops_after_unbind()
    {
      var root = new GameObject("Owned command HUD Back", typeof(RectTransform), typeof(MenuGalleryCommandHud));
      var actions = ScriptableObject.CreateInstance<InputActionAsset>();
      using var prompts = new RecordingPrompts();
      try
      {
        var hud = root.GetComponent<MenuGalleryCommandHud>();
        hud.Rows = CreateRows(root.transform);
        AddActions(actions);
        int backs = 0;
        Action back = () => backs++;
        hud.Bind(prompts, actions, back);
        hud.Bind(prompts, actions, back);
        Assert.That(prompts.ActiveBindings, Is.EqualTo(8), "Replacing a binding must retire the old eight observers.");
        hud.Rows.Single(row => row.ActionPath == "Menu/Back").Button.onClick.Invoke();
        Assert.That(backs, Is.EqualTo(1), "One pointer confirmation must call the existing semantic Back operation once.");
        hud.Unbind();
        hud.Rows.Single(row => row.ActionPath == "Menu/Back").Button.onClick.Invoke();
        Assert.That(backs, Is.EqualTo(1), "Unbound controls must not reach the previous session.");
        Assert.That(prompts.ActiveBindings, Is.Zero);
      }
      finally { Object.DestroyImmediate(root); Object.DestroyImmediate(actions); }
    }

    private static string SuppliedScene()
    {
      var paths = AssetDatabase.FindAssets("ActionMenuGallery t:SceneAsset").Select(AssetDatabase.GUIDToAssetPath)
        .Where(path => path.EndsWith("/HierarchicalActionMenus/ActionMenuGallery.unity", StringComparison.Ordinal)).ToArray();
      Assert.That(paths, Has.Length.EqualTo(1), "Import the supplied sample exactly once.");
      return paths[0];
    }

    private static MenuGalleryHudRow[] CreateRows(Transform parent)
    {
      var rows = new MenuGalleryHudRow[Labels.Length];
      for (int i = 0; i < rows.Length; i++)
      {
        var row = new GameObject(Labels[i], typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(row.transform, false);
        var glyph = new GameObject("Glyph", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        glyph.transform.SetParent(row.transform, false);
        rows[i] = new MenuGalleryHudRow { Label = Labels[i], ActionPath = Paths[i], LabelTarget = label, GlyphTarget = glyph,
          Button = Paths[i] == "Menu/Back" ? row.AddComponent<Button>() : null };
      }
      return rows;
    }

    private static void AddActions(InputActionAsset asset)
    {
      var shared = asset.AddActionMap("Shared");
      var menu = asset.AddActionMap("Menu");
      foreach (string path in Paths)
      {
        var parts = path.Split('/');
        var map = parts[0] == "Shared" ? shared : menu;
        map.AddAction(parts[1], InputActionType.Button).AddBinding("<Keyboard>/space");
      }
    }

    private sealed class RecordingPrompts : IInputPromptService, IDisposable
    {
      private sealed class Binding : IDisposable
      {
        public TMP_Text Text;
        public Func<string> Render;
        public bool Disposed;
        public void Dispose() { Disposed = true; }
      }
      private readonly List<Binding> bindings = new();
      private readonly Subject<Unit> changed = new();
      private int revision;
      public readonly List<InputAction> ResolvedActions = new();
      public readonly List<InputPromptStyle> Styles = new();
      public readonly Dictionary<InputAction, string> ObservedPaths = new();
      public int ActiveBindings => bindings.Count(binding => !binding.Disposed);
      public bool Disposed { get; private set; }
      public Observable<Unit> Changed => changed;
      public InputPromptContext Context => default;
      public string Resolve(InputAction action, InputPromptStyle style = InputPromptStyle.Auto, InputPromptContext? context = null)
      {
        ResolvedActions.Add(action); Styles.Add(style);
        ObservedPaths[action] = action.bindings[0].effectivePath;
        return "<sprite name=\"service-" + revision + "\">";
      }
      public string ResolveReference(InputActionReference action, InputPromptStyle style = InputPromptStyle.Auto, InputPromptContext? context = null)
        => Resolve(action.action, style, context);
      public string ResolvePath(string path, InputPromptStyle style = InputPromptStyle.Auto, InputPromptContext? context = null)
        => throw new InvalidOperationException("HUD actions must resolve through runtime identities.");
      public string Localize(string key, string fallback, params object[] arguments) => fallback;
      public string Compose(string key, string fallback, params InputPromptArgument[] arguments) => fallback;
      public IDisposable Bind(TMP_Text text, Func<string> render)
      {
        var binding = new Binding { Text = text, Render = render }; bindings.Add(binding);
        text.text = render(); return binding;
      }
      public void Refresh()
      {
        revision++;
        foreach (var binding in bindings.Where(binding => !binding.Disposed)) binding.Text.text = binding.Render();
        changed.OnNext(Unit.Default);
      }
      public void Dispose() { Disposed = true; changed.Dispose(); }
    }
  }
}