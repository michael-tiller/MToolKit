using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MToolKit.Runtime.Input.Prompts;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MToolKit.Editor.Input
{
  /// <summary>Editor-only native UGUI/TMP rendering. Runtime consumes only the saved TMP atlas.</summary>
  public static class InputKeycapBaker
  {
    public static string LastRenderDiagnostics { get; private set; }
    public static Texture2D Render(InputKeycapBakeProfile profile, string label, int fixedWidth = 0)
    {
      if (profile == null || profile.Font == null) throw new InvalidOperationException("Choose a TMP font for the keycaps.");
      if (profile.BackgroundPrefab != null && profile.BackgroundPrefab.GetComponent<RectTransform>() == null)
        throw new InvalidOperationException("Background prefab must have a RectTransform.");
      if (profile.BackgroundPrefab != null && (profile.BackgroundPrefab.GetComponentsInChildren<Mask>(true).Length > 0
          || profile.BackgroundPrefab.GetComponentsInChildren<RectMask2D>(true).Length > 0))
        throw new InvalidOperationException("Background prefab clipping masks are unsupported by isolated preview baking. Supply unmasked native UGUI graphics.");
      if (string.IsNullOrEmpty(label) || label.IndexOfAny(new[] { '\r', '\n', '\t' }) >= 0)
        throw new InvalidOperationException("Keycap labels must contain one nonempty line of literal text.");
      if (profile.SharedHeight < 8 || profile.SharedHeight > 1024 || profile.MinimumWidth < 8 || profile.MinimumWidth > 4096
        || profile.FontSize < 1 || profile.HorizontalPadding < 0 || profile.VerticalPadding < 0 || profile.BorderWidth < 0)
        throw new InvalidOperationException("Keycap dimensions, font size and padding are outside supported bounds.");
      if (!profile.Font.HasCharacters(label, out uint[] missing, true, false))
        throw new InvalidOperationException("The selected font/fallbacks lack label glyphs: " + string.Join(", ", missing.Select(value => "U+" + value.ToString("X4"))));
      bool uniform = profile.UniformCharacters && new StringInfo(label).LengthInTextElements == 1;
      int requestedWidth = fixedWidth > 0 ? fixedWidth : uniform ? Math.Max(profile.MinimumWidth, profile.SharedHeight) : 0;
      if (requestedWidth > 4096 || (requestedWidth > 0 && requestedWidth < profile.MinimumWidth))
        throw new InvalidOperationException("Fixed keycap width must fit the configured minimum and atlas size.");

      var previousActive = RenderTexture.active;
      PreviewRenderUtility preview = null;
      GameObject root = null;
      Texture2D result = null;
      RenderTexture exact = null;
      var previewMeshes = new List<Mesh>();
      bool begun = false;
      try
      {
        preview = new PreviewRenderUtility();
        preview.camera.orthographic = true;
        preview.camera.clearFlags = CameraClearFlags.SolidColor;
        preview.camera.backgroundColor = Color.clear;
        preview.camera.nearClipPlane = .1f;
        preview.camera.farClipPlane = 100f;
        preview.camera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
        root = new GameObject("Keycap bake canvas", typeof(RectTransform), typeof(Canvas));
        root.hideFlags = HideFlags.HideAndDontSave;
        preview.AddSingleGO(root);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = preview.camera;
        canvas.planeDistance = 1;
        canvas.pixelPerfect = true;
        var cap = new GameObject("Keycap", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        cap.transform.SetParent(root.transform, false);
        var rect = cap.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(Math.Max(profile.MinimumWidth, requestedWidth), profile.SharedHeight);
        ConfigureImage(cap.GetComponent<Image>(), profile.BorderSprite, profile.BorderColor, profile.BorderImageMode);
        var background = new GameObject("Background", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        background.transform.SetParent(cap.transform, false);
        background.GetComponent<LayoutElement>().ignoreLayout = true;
        var fillRect = background.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(profile.BorderWidth, profile.BorderWidth);
        fillRect.offsetMax = -fillRect.offsetMin;
        ConfigureImage(background.GetComponent<Image>(), profile.BackgroundSprite, profile.BackgroundColor, profile.BackgroundImageMode);
        var textHost = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textHost.transform.SetParent(cap.transform, false);
        var text = textHost.GetComponent<TextMeshProUGUI>();
        text.font = profile.Font;
        text.fontSize = profile.FontSize;
        text.color = profile.TextColor;
        text.richText = false;
        text.parseCtrlCharacters = false;
        text.enableAutoSizing = false;
#if UNITY_6000_0_OR_NEWER
        text.textWrappingMode = TextWrappingModes.NoWrap;
#else
        text.enableWordWrapping = false;
#endif
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = label;
        var layout = cap.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(profile.HorizontalPadding + profile.BorderWidth, profile.HorizontalPadding + profile.BorderWidth,
          profile.VerticalPadding + profile.BorderWidth, profile.VerticalPadding + profile.BorderWidth);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleCenter;
        cap.GetComponent<LayoutElement>().minWidth = profile.MinimumWidth;
        var fitter = cap.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = requestedWidth > 0 ? ContentSizeFitter.FitMode.Unconstrained : ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        int width = requestedWidth > 0 ? requestedWidth : Mathf.CeilToInt(rect.rect.width);
        if (width < profile.MinimumWidth || width > 4096) throw new InvalidOperationException("The resolved keycap width exceeds the configured atlas limits.");
        fitter.enabled = false;
        rect.sizeDelta = new Vector2(width, profile.SharedHeight);
        if (profile.BackgroundPrefab != null)
        {
          // Suppress OnEnable until authored/procedural components see the final cap dimensions.
          root.SetActive(false);
          cap.GetComponent<Image>().enabled = false;
          background.GetComponent<Image>().enabled = false;
          var styled = Object.Instantiate(profile.BackgroundPrefab, cap.transform, false);
          styled.name = "Background prefab";
          styled.transform.SetSiblingIndex(0);
          var styledRect = styled.GetComponent<RectTransform>();
          styledRect.anchorMin = Vector2.zero; styledRect.anchorMax = Vector2.one;
          styledRect.offsetMin = styledRect.offsetMax = Vector2.zero;
          (styled.GetComponent<LayoutElement>() ?? styled.AddComponent<LayoutElement>()).ignoreLayout = true;
          root.SetActive(true);
        }
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(width, profile.SharedHeight);
        preview.camera.orthographicSize = profile.SharedHeight * .5f;
        preview.camera.aspect = width / (float)profile.SharedHeight;
        preview.camera.cullingMask = ~0;
        preview.BeginPreview(new Rect(0, 0, width, profile.SharedHeight), GUIStyle.none);
        begun = true;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        text.ForceMeshUpdate();
        var preferred = text.GetPreferredValues(label);
        float availableWidth = width - layout.padding.horizontal;
        float availableHeight = profile.SharedHeight - layout.padding.vertical;
        if (availableWidth <= 0 || availableHeight <= 0 || preferred.x > availableWidth + .5f || preferred.y > availableHeight + .5f
          || text.isTextOverflowing || text.textBounds.size.x > availableWidth + .5f || text.textBounds.size.y > availableHeight + .5f)
          throw new InvalidOperationException("Label '" + label + "' does not fit. Increase width/height or reduce font size/padding.");
        var borderRenderer = cap.GetComponent<Image>().canvasRenderer;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        LastRenderDiagnostics = "mode=" + canvas.renderMode + " rootScene=" + root.scene.name + " cameraScene=" + preview.camera.scene.name
          + " rootPos=" + root.transform.position + " rootScale=" + root.transform.lossyScale + " canvasRect=" + root.GetComponent<RectTransform>().rect
          + " capCorners=" + string.Join(";", corners.Select(corner => corner.ToString("F2")))
          + " cameraType=" + preview.camera.cameraType + " cameraEnabled=" + preview.camera.enabled + " cameraActive=" + preview.camera.isActiveAndEnabled
          + " cameraPos=" + preview.camera.transform.position + " cameraForward=" + preview.camera.transform.forward
          + " ortho=" + preview.camera.orthographicSize + " aspect=" + preview.camera.aspect + " mask=" + preview.camera.cullingMask
          + " borderCull=" + borderRenderer.cull + " borderDepth=" + borderRenderer.absoluteDepth
          + " borderVerts=" + borderRenderer.GetMesh()?.vertexCount + " borderMats=" + borderRenderer.materialCount
          + " borderShader=" + (borderRenderer.materialCount > 0 ? borderRenderer.GetMaterial(0)?.shader.name : "none")
          + " textCull=" + text.canvasRenderer.cull + " textDepth=" + text.canvasRenderer.absoluteDepth
          + " textVerts=" + text.canvasRenderer.GetMesh()?.vertexCount + " textMats=" + text.canvasRenderer.materialCount
          + " pixelsPerPoint=" + EditorGUIUtility.pixelsPerPoint;
        SubmitNativeGraphics(preview, root, previewMeshes);
        preview.Render(false, false);
        var rendered = preview.EndPreview() as RenderTexture;
        begun = false;
        if (rendered == null) throw new InvalidOperationException("Native UGUI preview produced no render texture.");
        // Preview rectangles are GUI points and may produce a larger RT on high-DPI displays.
        // Copy the whole native render into exact pixel dimensions rather than cropping it.
        exact = RenderTexture.GetTemporary(width, profile.SharedHeight, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(rendered, exact);
        RenderTexture.active = exact;
        result = new Texture2D(width, profile.SharedHeight, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, width, profile.SharedHeight), 0, 0);
        result.Apply(false, false);
        return result;
      }
      catch { if (result != null) Object.DestroyImmediate(result); throw; }
      finally
      {
        try { if (begun) preview?.EndPreview(); }
        finally
        {
          RenderTexture.active = previousActive;
          if (exact != null) RenderTexture.ReleaseTemporary(exact);
          try { preview?.Cleanup(); }
          finally
          {
            foreach (var mesh in previewMeshes) if (mesh != null) Object.DestroyImmediate(mesh);
            if (root != null) Object.DestroyImmediate(root);
          }
        }
      }
    }

    public static TMP_SpriteAsset Bake(InputKeycapBakeProfile profile, Func<string, string, string> localize = null)
    {
      if (profile == null || profile.Keys == null || profile.Keys.Length == 0) throw new InvalidOperationException("Add at least one keycap label.");
      var source = ScriptableObject.CreateInstance<InputGlyphAtlasSource>();
      var textures = new List<Texture2D>();
      var sprites = new List<Sprite>();
      try
      {
        source.Catalog = profile.Catalog;
        source.Family = InputGlyphFamily.KeyboardAndMouse;
        source.SpriteAssetPath = profile.SpriteAssetPath;
        var rows = new List<InputGlyphAtlasSource.Glyph>();
        foreach (var key in profile.Keys)
        {
          if (key == null) throw new InvalidOperationException("Remove empty keycap rows.");
          bool localized = !string.IsNullOrEmpty(key.LocalizationKey);
          var locales = localized ? profile.Locales : new[] { "" };
          if (locales == null || locales.Length == 0) throw new InvalidOperationException("Configure locales for localized labels.");
          foreach (string locale in locales)
          {
            string code = (locale ?? "").Replace('_', '-').ToLowerInvariant();
            string label = localized ? localize?.Invoke(key.LocalizationKey, code) ?? ResolveLocalizedLabel(profile, key.LocalizationKey, code) : key.Label;
            if (string.IsNullOrEmpty(label)) label = key.Label;
            int fixedWidth = key.WidthRule == InputKeycapWidthRule.Fixed ? key.FixedWidth : 0;
            if (key.WidthRule == InputKeycapWidthRule.Fixed && fixedWidth <= 0) throw new InvalidOperationException("Fixed width must be positive: " + key.ControlPath);
            var texture = Render(profile, label, fixedWidth);
            textures.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
            sprites.Add(sprite);
            rows.Add(new InputGlyphAtlasSource.Glyph { ControlPath = key.ControlPath,
              SpriteName = key.SpriteName + (string.IsNullOrEmpty(code) ? "" : "-" + code), SourceSprite = sprite,
              LocaleCode = code, BakedLabel = label, DisplayName = key.Label, LocalizationKey = key.LocalizationKey });
          }
        }
        source.Glyphs = rows.ToArray();
        return InputGlyphAtlasAuthoring.Rebuild(source);
      }
      finally
      {
        foreach (var sprite in sprites) Object.DestroyImmediate(sprite);
        foreach (var texture in textures) Object.DestroyImmediate(texture);
        Object.DestroyImmediate(source);
      }
    }

    public static string ResolveLabel(InputKeycapBakeProfile profile, InputKeycapBakeProfile.Key key, string locale)
      => string.IsNullOrEmpty(key.LocalizationKey) ? key.Label : ResolveLocalizedLabel(profile, key.LocalizationKey, locale) ?? key.Label;
    private static string ResolveLocalizedLabel(InputKeycapBakeProfile profile, string key, string locale)
    {
      if (profile.LocalizationCollection == null) return null;
      string code = locale;
      while (!string.IsNullOrEmpty(code))
      {
        var table = profile.LocalizationCollection.StringTables.FirstOrDefault(candidate => string.Equals(candidate.LocaleIdentifier.Code, code, StringComparison.OrdinalIgnoreCase));
        string value = table?.GetEntry(key)?.Value;
        if (!string.IsNullOrEmpty(value)) return value;
        int split = code.LastIndexOf('-');
        code = split < 0 ? "" : code.Substring(0, split);
      }
      return null;
    }

    private static void SubmitNativeGraphics(PreviewRenderUtility preview, GameObject root, ICollection<Mesh> ownedMeshes)
    {
      // Preview cameras do not register Canvas render depth. Submit UGUI's already-generated
      // geometry through Unity's preview renderer; layout, slicing and text remain native UGUI/TMP.
      int order = 0;
      foreach (var graphic in root.GetComponentsInChildren<Graphic>(false))
      {
        if (!graphic.isActiveAndEnabled || graphic.canvasRenderer.cull) continue;
        var sourceMesh = graphic.canvasRenderer.GetMesh();
        var material = graphic.materialForRendering;
        if (sourceMesh == null || sourceMesh.vertexCount == 0 || material == null) continue;
        // CanvasRenderer's borrowed mesh retains native UI rendering state. A native clone
        // is required for Unity's ordinary preview draw path; retain every UV/color channel.
        var mesh = Object.Instantiate(sourceMesh);
        mesh.hideFlags = HideFlags.HideAndDontSave;
        ownedMeshes.Add(mesh);
        var properties = new MaterialPropertyBlock();
        // TMP_Text inherits Graphic's white mainTexture; its actual glyph atlas is on
        // the native font material. Image and TMP_SubMeshUI expose their own textures.
        properties.SetTexture("_MainTex", graphic is TMP_Text ? material.mainTexture : graphic.mainTexture);
        var color = graphic.canvasRenderer.GetColor();
        if (material.HasProperty("_Color")) color *= material.GetColor("_Color");
        properties.SetColor("_Color", color);
        var matrix = graphic.rectTransform.localToWorldMatrix;
        // Equal-plane UI uses hierarchy order. Small camera-facing depth steps retain that
        // order with Unity's normal transparent sorting, including native TMP fallback graphics.
        matrix.m23 -= order++ * .001f;
        for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
          preview.DrawMesh(mesh, matrix, material, submesh, properties);
      }
    }
    private static void ConfigureImage(Image image, Sprite sprite, Color color, InputKeycapImageMode mode)
    {
      image.sprite = sprite;
      image.color = color;
      image.raycastTarget = false;
      image.type = mode == InputKeycapImageMode.Sliced && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
    }
  }
}
