using System;
using System.Linq;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Widgets;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MToolKit.ContextMenus.Samples.Tests.Editor
{
  public sealed class MenuGallerySecondaryLayoutTests
  {
    [Test]
    public void Supplied_center_secondary_labels_glyphs_and_optional_media_fit_without_wrapping_or_shrinking()
    {
      string[] profiles = AssetDatabase.FindAssets("NestedWheelContextMenu t:MenuProfile")
        .Select(AssetDatabase.GUIDToAssetPath)
        .Where(path => path.EndsWith("/HierarchicalActionMenus/Profiles/NestedWheelContextMenu.asset", StringComparison.Ordinal))
        .ToArray();
      Assert.That(profiles, Has.Length.EqualTo(1), "Import the supplied HierarchicalActionMenus sample exactly once.");
      string sampleRoot = profiles[0].Substring(0, profiles[0].Length - "/Profiles/NestedWheelContextMenu.asset".Length);
      var profile = AssetDatabase.LoadAssetAtPath<MenuProfile>(profiles[0]);
      var centerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sampleRoot + "/Prefabs/CenterInformation.prefab");
      var secondaryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sampleRoot + "/Prefabs/SecondaryAction.prefab");
      var settings = AssetDatabase.LoadAssetAtPath<InputPromptSettings>(sampleRoot + "/Input/Glyphs/MenuPromptSettings.asset");
      var artwork = AssetDatabase.LoadAssetAtPath<Sprite>(sampleRoot + "/Icons/ActionArtwork.png");
      Assert.That(profile, Is.Not.Null);
      Assert.That(centerPrefab, Is.SameAs(profile.CenterPrefab));
      Assert.That(secondaryPrefab, Is.SameAs(profile.SecondaryPrefab));
      Assert.That(settings, Is.Not.Null);
      Assert.That(settings.Catalog, Is.Not.Null, "Exercise the supplied native TMP glyph catalog, not literal shortcut text.");
      Assert.That(artwork, Is.Not.Null, "The sample's generic artwork is required for the optional-media layout cases.");
      var theme = new MenuThemeContext(profile.Skin, profile.SharedThemeOverrides, profile.ThemeOverrides);
      var available = MenuActionState.Available;
      Scene preview = EditorSceneManager.NewPreviewScene();
      GameObject canvasObject = null;
      try
      {
        // Instantiate only saved sample assets directly into an owned preview scene. No caller scene or asset is saved.
        canvasObject = (GameObject)PrefabUtility.InstantiatePrefab(profile.PresenterPrefab, preview);
        canvasObject.name = "Owned secondary layout test canvas";
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(centerPrefab, preview);
        instance.transform.SetParent(canvasObject.transform, false);
        var center = instance.GetComponent<MenuCenterWidget>();
        Assert.That(center, Is.Not.Null);
        Assert.That(center.SecondaryPrefab, Is.SameAs(secondaryPrefab));
        Assert.That(center.SecondaryContainer.rect.width, Is.EqualTo(200).Within(.01f), "Preserve the supplied center composition width.");

        using (var prompts = new InputPromptService(settings, localizer: new EnglishNames()))
        {
          var keyboard = new InputPromptContext(familyOverride: InputGlyphFamily.KeyboardAndMouse);
          string[] hints = {
            prompts.ResolvePath("<Keyboard>/q", InputPromptStyle.Glyph, keyboard),
            prompts.ResolvePath("<Keyboard>/r", InputPromptStyle.Glyph, keyboard)
          };
          Assert.That(hints.All(hint => hint.StartsWith("<sprite=", StringComparison.Ordinal)), Is.True,
            "Both auxiliary bindings must resolve to real catalog glyph markup.");
          // Optional media must collapse naturally. Both short labels remain readable for every supported media combination.
          for (int media = 0; media < 4; media++)
          {
            Sprite icon = (media & 1) != 0 ? artwork : null;
            Sprite image = (media & 2) != 0 ? artwork : null;
            var variant = new MenuAction("layout.variant", new MenuLabel("Variant"), icon: icon, image: image);
            var inspect = new MenuAction("layout.inspect", new MenuLabel("Inspect"), icon: icon, image: image);
            var selected = new MenuAction("layout.selected", new MenuLabel("Revolver"));
            center.Bind(new MenuWidgetState(selected, available, selected: true,
              secondaryStates: new[] {
                new MenuWidgetState(variant, available, bindingHint: hints[0], theme: theme, activate: () => { }),
                new MenuWidgetState(inspect, available, bindingHint: hints[1], theme: theme, activate: () => { })
              }, theme: theme));
            RebuildOwnedLayouts(center);
            var controls = center.SecondaryContainer.GetComponentsInChildren<MenuActionWidget>()
              .Where(widget => widget.gameObject.activeInHierarchy).ToArray();
            Assert.That(controls, Has.Length.EqualTo(2), "Bind both Variant and Inspect in the real supplied center.");
            var firstRow = BoundsIn(center.SecondaryContainer, (RectTransform)controls[0].transform);
            var secondRow = BoundsIn(center.SecondaryContainer, (RectTransform)controls[1].transform);
            var rowLayout = center.SecondaryContainer.GetComponent<LayoutGroup>();
            float availableWidth = center.SecondaryContainer.rect.width - rowLayout.padding.horizontal;
            Assert.That(firstRow.width, Is.GreaterThanOrEqualTo(availableWidth - .25f), "Each secondary action gets its own full-width line.");
            Assert.That(secondRow.width, Is.GreaterThanOrEqualTo(availableWidth - .25f));
            Assert.That(firstRow.center.x, Is.EqualTo(secondRow.center.x).Within(.25f));
            Assert.That(firstRow.yMin, Is.GreaterThanOrEqualTo(secondRow.yMax - .25f), "Secondary actions stack vertically in authored order.");
            foreach (var widget in controls)
              AssertReadableControl(widget, profile, center, media);
            AssertDisjoint(BoundsIn(center.transform, (RectTransform)controls[0].transform),
              BoundsIn(center.transform, (RectTransform)controls[1].transform), "Auxiliary hit targets overlap.");
            center.Unbind();
          }
        }
      }
      finally
      {
        if (canvasObject != null) UnityEngine.Object.DestroyImmediate(canvasObject);
        if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
      }
    }

    private static void RebuildOwnedLayouts(MenuCenterWidget center)
    {
      var groups = center.GetComponentsInChildren<LayoutGroup>(true)
        .Where(group => group.isActiveAndEnabled).OrderBy(group => Depth(group.transform)).ToArray();
      // Rebuild actual layout-controller roots: a manually positioned center root itself has no ILayoutController.
      for (int pass = 0; pass < 3; pass++)
      {
        foreach (var group in groups)
          LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);
        Canvas.ForceUpdateCanvases();
      }
      foreach (var text in center.GetComponentsInChildren<TMP_Text>())
        if (text.isActiveAndEnabled) text.ForceMeshUpdate(false, true);
    }

    private static void AssertReadableControl(MenuActionWidget widget, MenuProfile profile, MenuCenterWidget center, int media)
    {
      string context = widget.Label.text + ", media=" + media;
      var hit = ((RectTransform)widget.transform).rect;
      Assert.That(hit.width, Is.GreaterThanOrEqualTo(profile.MinimumTargetSize), context + ": usable hit width");
      Assert.That(hit.height, Is.GreaterThanOrEqualTo(profile.MinimumTargetSize), context + ": usable hit height");
      Assert.That(widget.Label.fontSize, Is.EqualTo(18).Within(.01f), context + ": do not shrink the authored18pt label");
      Assert.That(widget.Label.textInfo.lineCount, Is.EqualTo(1), context + ": short labels must stay on one line");
      Assert.That(widget.Label.isTextTruncated, Is.False, context + ": do not ellipsize a short action name");
      Assert.That(widget.Label.textInfo.characterCount, Is.EqualTo(widget.Label.text.Length), context + ": complete native label mesh");
      Assert.That(widget.BindingHint, Is.Not.Null, context);
      Assert.That(PositiveSpriteQuads(widget.BindingHint), Is.GreaterThan(0), context + ": real binding glyph must remain visible");
      Rect label = BoundsIn(widget.transform, widget.Label.rectTransform);
      Rect glyph = BoundsIn(widget.transform, widget.BindingHint.rectTransform);
      AssertInside(label, hit, context + ": label lies inside hit target");
      AssertInside(glyph, hit, context + ": binding glyph target lies inside hit target");
      AssertDisjoint(label, glyph, context + ": label/glyph rectangles overlap");
      Rect? previousMedia = null;
      foreach (var target in new[] { widget.Icon, widget.ActionImage })
      {
        if (target == null || !target.enabled || !target.gameObject.activeInHierarchy) continue;
        Rect bounds = BoundsIn(widget.transform, target.rectTransform);
        AssertInside(bounds, hit, context + ": media lies inside hit target");
        AssertDisjoint(bounds, label, context + ": media/label rectangles overlap");
        AssertDisjoint(bounds, glyph, context + ": media/glyph rectangles overlap");
        if (previousMedia.HasValue) AssertDisjoint(previousMedia.Value, bounds, context + ": independent icon/image rectangles overlap");
        previousMedia = bounds;
      }
      var corners = new Vector3[4];
      ((RectTransform)widget.transform).GetWorldCorners(corners);
      foreach (var corner in corners)
      {
        Vector3 point = center.transform.InverseTransformPoint(corner);
        Assert.That(new Vector2(point.x, point.y).magnitude, Is.LessThanOrEqualTo(profile.InnerRadius + .5f),
          context + ": auxiliary hit target must remain inside the radial center clearance");
      }
    }

    private static int PositiveSpriteQuads(TMP_Text text)
    {
      text.ForceMeshUpdate(false, true);
      int count = 0;
      for (int i = 0; i < text.textInfo.characterCount; i++)
      {
        var character = text.textInfo.characterInfo[i];
        if (!character.isVisible || character.elementType != TMP_TextElementType.Sprite) continue;
        int material = character.materialReferenceIndex, vertex = character.vertexIndex;
        if (material < 0 || material >= text.textInfo.meshInfo.Length) continue;
        var vertices = text.textInfo.meshInfo[material].vertices;
        if (vertices != null && vertex >= 0 && vertex + 3 < vertices.Length
          && Vector3.Cross(vertices[vertex + 1] - vertices[vertex], vertices[vertex + 3] - vertices[vertex]).sqrMagnitude > .00000001f) count++;
      }
      return count;
    }

    private static Rect BoundsIn(Transform coordinateFrame, RectTransform target)
    {
      var corners = new Vector3[4];
      target.GetWorldCorners(corners);
      Vector2 minimum = coordinateFrame.InverseTransformPoint(corners[0]);
      Vector2 maximum = minimum;
      foreach (var corner in corners)
      {
        Vector2 point = coordinateFrame.InverseTransformPoint(corner);
        minimum = Vector2.Min(minimum, point);
        maximum = Vector2.Max(maximum, point);
      }
      return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
    }

    private static void AssertInside(Rect content, Rect hit, string message)
    {
      const float tolerance = .25f;
      Assert.That(content.width, Is.GreaterThanOrEqualTo(0), message);
      Assert.That(content.height, Is.GreaterThanOrEqualTo(0), message);
      Assert.That(content.xMin, Is.GreaterThanOrEqualTo(hit.xMin - tolerance), message);
      Assert.That(content.xMax, Is.LessThanOrEqualTo(hit.xMax + tolerance), message);
      Assert.That(content.yMin, Is.GreaterThanOrEqualTo(hit.yMin - tolerance), message);
      Assert.That(content.yMax, Is.LessThanOrEqualTo(hit.yMax + tolerance), message);
    }

    private static void AssertDisjoint(Rect first, Rect second, string message)
    {
      float x = Mathf.Min(first.xMax, second.xMax) - Mathf.Max(first.xMin, second.xMin);
      float y = Mathf.Min(first.yMax, second.yMax) - Mathf.Max(first.yMin, second.yMin);
      Assert.That(x <= .25f || y <= .25f, Is.True, message);
    }

    private static int Depth(Transform transform)
    {
      int depth = 0;
      for (var parent = transform.parent; parent != null; parent = parent.parent) depth++;
      return depth;
    }

    private sealed class EnglishNames : IInputPromptLocalizer, IInputPromptLocaleProvider
    {
      public string LocaleCode => "en";
      public string GetName(string key, string fallback) => fallback;
    }
  }
}
