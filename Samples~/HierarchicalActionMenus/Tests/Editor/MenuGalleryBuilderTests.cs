using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Hosting;
using MToolKit.ContextMenus.Samples;
using MToolKit.ContextMenus.Widgets;
using MToolKit.Theme.Spacing;
using MToolKit.Theme.Typeset;
using MToolKit.Runtime.Input.Prompts;
using TMPro;
using MToolKit.ContextMenus.Samples.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MToolKit.ContextMenus.Samples.Tests.Editor
{
  public sealed class MenuGalleryBuilderTests
  {
    private string ownedRoot;
    private Scene caller, previous;
    private string entryScenePath;

    [SetUp]
    public void PrepareOwnedAuthoringScene()
    {
      previous = SceneManager.GetActiveScene();
      ownedRoot = "Assets/Temp/automation-scratch/ActionMenuGalleryBuilder-" + Guid.NewGuid().ToString("N");
      Folder(ownedRoot);
      // Test Framework TaskList creates DefaultGameObjects for EditMode, retaining its
      // exact InitTestScene in TestJobData. Match that identity rather than guessing
      // ownership from the untitled scene's contents or saving an arbitrary user scene.
      if (string.IsNullOrEmpty(previous.path))
      {
        var roots = new List<string>();
        foreach (var root in previous.GetRootGameObjects())
        {
          var types = new List<string>();
          foreach (var component in root.GetComponents<Component>()) types.Add(component != null ? component.GetType().FullName : "missing");
          roots.Add(root.name + " [" + string.Join(", ", types) + "]");
        }
        TestContext.WriteLine("Entry scene handle=" + previous.handle + " name=" + previous.name + " path=" + previous.path
          + " dirty=" + previous.isDirty + "; roots=" + string.Join("; ", roots));
        Assert.That(IsTestRunnerEntry(previous), Is.True, "Refusing to save an untitled scene that is not the active Test Framework job's InitTestScene");
        entryScenePath = "Assets/Temp/automation-scratch/MenuGalleryFixtureEntry-" + Guid.NewGuid().ToString("N") + ".unity";
        Assert.That(EditorSceneManager.SaveScene(previous, entryScenePath), Is.True);
      }
      caller = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
      SceneManager.SetActiveScene(caller);
      Assert.That(EditorSceneManager.SaveScene(caller, ownedRoot + "/Caller.unity"), Is.True);
      Assert.That(caller.isDirty, Is.False);
    }

    [TearDown]
    public void RemoveOwnedAuthoringAssets()
    {
      if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
      if (caller.IsValid() && caller.isLoaded) EditorSceneManager.CloseScene(caller, true);
      if (!string.IsNullOrEmpty(ownedRoot)) AssetDatabase.DeleteAsset(ownedRoot);
      ownedRoot = null;
    }

    [OneTimeTearDown]
    public void RemoveSavedTestRunnerEntry()
    {
      if (string.IsNullOrEmpty(entryScenePath)) return;
      var entry = SceneManager.GetSceneByPath(entryScenePath);
      if (entry.IsValid() && entry.isLoaded)
      {
        Assert.That(SceneManager.sceneCount, Is.EqualTo(1), "Unexpected scene ownership change during isolated authoring fixture");
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
      }
      AssetDatabase.DeleteAsset(entryScenePath);
      entryScenePath = null;
    }

    private static bool IsTestRunnerEntry(Scene scene)
    {
      var holderType = typeof(UnityEditor.TestTools.TestRunner.Api.TestRunnerApi).Assembly.GetType(
        "UnityEditor.TestTools.TestRunner.TestRun.TestJobDataHolder", throwOnError: true);
      foreach (var holder in Resources.FindObjectsOfTypeAll(holderType))
      {
        var runs = (IEnumerable)holderType.GetField("TestRuns", BindingFlags.Public | BindingFlags.Instance).GetValue(holder);
        foreach (var run in runs)
        {
          var field = run.GetType().GetField("InitTestScene", BindingFlags.Public | BindingFlags.Instance);
          if (field != null && (Scene)field.GetValue(run) == scene) return true;
        }
      }
      return false;
    }

    [Test]
    public void Existing_generated_data_is_rejected_without_touching_authored_assets()
    {
      string output = ownedRoot + "/Gallery";
      Folder(output + "/Profiles");
      var marker = ScriptableObject.CreateInstance<MenuProfile>();
      marker.ListWidth = 513;
      string markerPath = output + "/Profiles/Existing.asset";
      AssetDatabase.CreateAsset(marker, markerPath);
      AssetDatabase.SaveAssetIfDirty(marker);
      marker.ListWidth = 514; EditorUtility.SetDirty(marker);
      var error = Assert.Throws<InvalidOperationException>(() => MenuGalleryAssetBuilder.Build(output));
      Assert.That(error.Message, Does.Contain("exist").IgnoreCase);
      Assert.That(EditorUtility.IsDirty(marker), Is.True, "Preflight saved a preexisting authored asset");
      Assert.That(AssetDatabase.LoadAssetAtPath<MenuProfile>(markerPath), Is.SameAs(marker));
      Assert.That(marker.ListWidth, Is.EqualTo(514));
      Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(output + "/ActionMenuGallery.unity"), Is.Null);
      Assert.That(caller.isDirty, Is.False);
    }

    [Test]
    public void Generated_gallery_has_glyph_only_settings_and_native_keyboard_mouse_and_controller_assets()
    {
      var promptProperty = typeof(MenuGalleryController).GetProperty("PromptSettings");
      Assert.That(promptProperty, Is.Not.Null, "Standalone prompts need an authored TMP glyph catalog/settings reference.");
      string path = MenuGalleryAssetBuilder.Build(ownedRoot + "/GlyphGallery");
      var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
      try
      {
        var controller = scene.GetRootGameObjects()
          .SelectMany(root => root.GetComponentsInChildren<MenuGalleryController>(true)).Single();
        var settings = promptProperty.GetValue(controller) as InputPromptSettings;
        Assert.That(settings, Is.Not.Null);
        Assert.That(settings.Style, Is.EqualTo(InputPromptStyle.Glyph));
        Assert.That(settings.Catalog, Is.Not.Null);
        var openAction = controller.InputActions.FindAction("Shared/OpenContext", true);
        foreach (var binding in openAction.bindings)
        {
          string bindingPath = binding.effectivePath;
          var family = bindingPath.StartsWith("<Gamepad>/", StringComparison.Ordinal)
            ? InputGlyphFamily.Generic : InputGlyphFamily.KeyboardAndMouse;
          string control = bindingPath.Substring(bindingPath.IndexOf('/') + 1);
          Assert.That(settings.Catalog.Entries.Any(entry => entry.Family == family
            && string.Equals(entry.ControlPath, control, StringComparison.OrdinalIgnoreCase)
            && entry.SpriteAsset != null && !string.IsNullOrEmpty(entry.SpriteName)),
            Is.True, "Missing OpenContext glyph for actual binding " + bindingPath);
        }
        foreach (string control in new[] { "enter", "escape", "tab", "q", "r", "e", "backspace", "pageUp", "pageDown", "f2", "f8", "minus", "equals", "leftButton", "rightButton" })
          Assert.That(settings.Catalog.Entries.Any(entry => entry.Family == InputGlyphFamily.KeyboardAndMouse
            && entry.ControlPath == control && entry.SpriteAsset != null && !string.IsNullOrEmpty(entry.SpriteName)),
            Is.True, "Missing native keyboard/mouse glyph for " + control);
        foreach (string control in new[] { "buttonSouth", "buttonEast", "buttonWest", "buttonNorth", "leftShoulder", "rightShoulder", "leftTrigger", "rightTrigger", "start", "rightStickPress" })
          Assert.That(settings.Catalog.Entries.Any(entry => entry.Family == InputGlyphFamily.Generic
            && entry.ControlPath == control && entry.SpriteAsset != null && !string.IsNullOrEmpty(entry.SpriteName)),
            Is.True, "Missing neutral controller glyph for " + control);
      }
      finally
      {
        EditorSceneManager.CloseScene(scene, true);
        SceneManager.SetActiveScene(caller);
      }
    }

    [Test]
    public void Generating_prefabs_does_not_dirty_the_callers_active_scene()
    {
      string path = MenuGalleryAssetBuilder.Build(ownedRoot + "/Gallery");
      Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null);
      Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(caller));
      Assert.That(caller.isDirty, Is.False, "Temporary prefab GameObjects were created in the caller's scene");
    }

    [Test]
    public void Dirty_unrelated_assets_are_refused_without_changing_memory_or_disk()
    {
      var unrelated = ScriptableObject.CreateInstance<MenuProfile>();
      string path = ownedRoot + "/Unrelated.asset";
      AssetDatabase.CreateAsset(unrelated, path);
      AssetDatabase.SaveAssetIfDirty(unrelated);
      string diskBefore = File.ReadAllText(path);
      unrelated.ListWidth = 627;
      EditorUtility.SetDirty(unrelated);
      Assert.That(EditorUtility.IsDirty(unrelated), Is.True);
      var error = Assert.Throws<InvalidOperationException>(() => MenuGalleryAssetBuilder.Build(ownedRoot + "/Gallery"));
      Assert.That(error.Message, Does.Contain(path), "Refusal should identify the existing edit that Unity would save");
      Assert.That(EditorUtility.IsDirty(unrelated), Is.True,
        "Sample generation cleared another editor's dirty asset state");
      Assert.That(unrelated.ListWidth, Is.EqualTo(627));
      Assert.That(File.ReadAllText(path), Is.EqualTo(diskBefore), "Sample generation wrote unrelated asset content");
      Assert.That(AssetDatabase.IsValidFolder(ownedRoot + "/Gallery"), Is.False, "Preflight created output before refusing the dirty edit");
      Assert.That(Directory.Exists(ownedRoot + "/Gallery"), Is.False);
    }

    [Test]
    public void Fresh_gallery_authors_four_canonical_root_styles_and_one_custom_child()
    {
      string output = ownedRoot + "/CanonicalGallery";
      MenuGalleryAssetBuilder.Build(output);
      var config = AssetDatabase.LoadAssetAtPath<MenuHostConfig>(output + "/Profiles/GalleryHost.asset");
      Assert.That(config, Is.Not.Null);
      string[] expected = { "WheelContextMenu", "PieContextMenu", "ListContextMenu", "NestedWheelContextMenu" };

      Assert.That(config.Profiles.Length, Is.EqualTo(4), "Visual variations of one wheel are configurations, not duplicate style roots.");
      Assert.That(config.Profiles.Select(value => value.Name), Is.EquivalentTo(expected));
      var files = AssetDatabase.FindAssets("t:MenuProfile", new[] { output + "/Profiles" })
        .Select(AssetDatabase.GUIDToAssetPath).Select(Path.GetFileNameWithoutExtension).ToArray();
      Assert.That(files, Is.EquivalentTo(expected.Concat(new[] { "ReplacementCards" })),
        "The custom presentation remains a child demo; no duplicate wheel profile should be generated.");
      foreach (string name in new[] { "ListRow", "RadialLabelIcon", "InteractionButton", "IconOnly", "SecondaryAction", "CenterInformation" })
      {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(output + "/Prefabs/" + name + ".prefab");
        Assert.That(prefab, Is.Not.Null, name);
        var widget = prefab.GetComponent<MenuActionWidget>();
        Assert.That(widget, Is.Not.Null, name);
        Assert.That(widget.SpacingTargets, Is.Not.Null.And.Not.Empty,
          name + " must expose concrete layout targets for authored theme spacing.");
        foreach (var spacing in widget.SpacingTargets)
        {
          Assert.That(spacing, Is.Not.Null, name);
          Assert.That(spacing.LayoutGroup, Is.Not.Null, name + " has an unwired spacing preset.");
          Assert.That(spacing.GetComponent<LayoutGroup>(), Is.SameAs(spacing.LayoutGroup), name);
        }
        foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true))
        {
          var typography = text.GetComponent<TypesetPreset>();
          Assert.That(typography, Is.Not.Null, name + "/" + text.name + " lacks an authorable typeset preset.");
          Assert.That(typography.Text, Is.SameAs(text), name + "/" + text.name + " targets different text.");
        }
      }
      var customCenter = AssetDatabase.LoadAssetAtPath<GameObject>(output + "/Prefabs/CustomCenter.prefab");
      Assert.That(customCenter.GetComponent<GalleryCenterWidget>(), Is.Not.Null);
      Assert.That(customCenter.GetComponentsInChildren<SpacingPreset>(true), Is.Empty,
        "The custom center retains ownership of its private layout.");
      Assert.That(customCenter.GetComponentsInChildren<TypesetPreset>(true), Is.Empty,
        "The custom center deliberately supplies its own typography.");
    }

    [Test]
    public void Fresh_gallery_authors_independent_icons_images_and_optional_widget_targets()
    {
      string output = ownedRoot + "/MediaGallery";
      MenuGalleryAssetBuilder.Build(output);
      var artwork = AssetDatabase.LoadAssetAtPath<Sprite>(output + "/Icons/ActionArtwork.png");
      Assert.That(artwork, Is.Not.Null, "The image example must use an authored reusable artwork sprite.");
      var set = AssetDatabase.LoadAssetAtPath<MToolKit.ContextMenus.Authoring.MenuActionSetSO>(
        output + "/Actions/GalleryActions.asset");
      var actions = set.Create(new MenuContext(new MenuGalleryState()));
      Assert.That(actions.Count, Is.EqualTo(5), "Adding media examples must preserve the action hierarchy.");
      AssertMedia(actions[0], true, true, artwork, "Build");
      AssertMedia(actions[1], true, false, artwork, "Equipment");
      AssertMedia(actions[2], false, true, artwork, "Interactions");
      AssertMedia(actions[3], false, false, artwork, "Preferences");
      AssertMedia(actions[4], true, true, artwork, "Unavailable recipe");
      var revolver = actions[1].Children[0];
      AssertMedia(revolver, true, true, artwork, "Equipment selection");
      Assert.That(revolver.SecondaryActions.Any(value => MediaImage(value) == artwork), Is.True,
        "At least one independently bound auxiliary must demonstrate an image.");
      var provider = AssetDatabase.LoadAssetAtPath<GalleryChildrenProviderSO>(output + "/Actions/StockProvider.asset");
      Assert.That(provider.Icon, Is.Not.Null);
      Assert.That(MediaImage(provider), Is.SameAs(artwork), "The dynamic provider must retain authored image data.");

      foreach (string name in new[] { "ListRow", "RadialLabelIcon", "InteractionButton", "IconOnly", "SecondaryAction", "CenterInformation" })
      {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(output + "/Prefabs/" + name + ".prefab");
        var widget = prefab.GetComponent<MenuActionWidget>();
        var property = typeof(MenuActionWidget).GetProperty("ActionImage");
        Assert.That(property, Is.Not.Null, "The default widget must expose its independent image target.");
        var image = property.GetValue(widget) as Image;
        Assert.That(image, Is.Not.Null, name + " cannot render an image-only action.");
        Assert.That(image, Is.Not.SameAs(widget.Icon), name + " aliases the compact icon and artwork.");
        Assert.That(image.raycastTarget, Is.False, name + " artwork must not take input ownership.");
        if (widget.Icon != null)
        {
          Assert.That(widget.Icon.transform.IsChildOf(image.transform), Is.False,
            name + " would hide its icon when its image is absent.");
          Assert.That(image.transform.IsChildOf(widget.Icon.transform), Is.False,
            name + " would hide its image when its icon is absent.");
        }
      }
    }

    private static Sprite MediaImage(object value)
    {
      var type = value.GetType();
      var property = type.GetProperty("Image");
      if (property != null) return property.GetValue(value) as Sprite;
      var field = type.GetField("Image");
      Assert.That(field, Is.Not.Null, type.Name + " must expose independent Image authoring.");
      return field.GetValue(value) as Sprite;
    }

    private static void AssertMedia(MenuAction action, bool icon, bool image, Sprite artwork, string label)
    {
      Assert.That(action.Icon != null, Is.EqualTo(icon), label + " icon");
      Assert.That(MediaImage(action), Is.SameAs(image ? artwork : null), label + " image");
      if (image && icon) Assert.That(action.Icon, Is.Not.SameAs(artwork), label + " must use independent sprites.");
    }

    private static void Folder(string path)
    {
      if (AssetDatabase.IsValidFolder(path)) return;
      int separator = path.LastIndexOf('/');
      string parent = path.Substring(0, separator);
      Folder(parent);
      AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
    }
  }
}
