using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MToolKit.Runtime.Input.Prompts;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MToolKit.Editor.Input
{
  public sealed class InputKeycapAtlasOutput
  {
    public string LocaleCode { get; }
    public TMP_SpriteAsset SpriteAsset { get; }
    internal InputKeycapAtlasOutput(string localeCode, TMP_SpriteAsset spriteAsset)
    { LocaleCode = localeCode; SpriteAsset = spriteAsset; }
  }

  public sealed class InputKeycapBakeResult
  {
    public TMP_SpriteAsset PrimaryAsset { get; }
    public IReadOnlyList<InputKeycapAtlasOutput> Atlases { get; }
    public IReadOnlyList<string> Issues { get; }
    internal InputKeycapBakeResult(TMP_SpriteAsset primary, InputKeycapAtlasOutput[] atlases, string[] issues)
    { PrimaryAsset = primary; Atlases = atlases; Issues = issues; }
  }

  /// <summary>Editor-only native UGUI/TMP rendering. Runtime consumes only the saved TMP atlas.</summary>
  public static class InputKeycapBaker
  {
    public static string[] GetLocaleCodes(InputKeycapBakeProfile profile)
    {
      if (profile == null) throw new ArgumentNullException(nameof(profile));
      if (!Enum.IsDefined(typeof(InputKeycapLocaleSource), profile.LocaleSource))
        throw new InvalidOperationException("Select a known locale source.");
      var locales = profile.LocaleSource == InputKeycapLocaleSource.ProjectEnabled
        ? LocalizationEditorSettings.GetLocales().Select(locale => locale.Identifier.Code)
        : profile.Locales ?? Array.Empty<string>();
      return locales.Where(code => !string.IsNullOrWhiteSpace(code)).Select(NormalizeLocale)
        .Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToArray();
    }

    public static string GetSpriteAssetPath(InputKeycapBakeProfile profile, string localeCode)
    {
      if (profile == null) throw new ArgumentNullException(nameof(profile));
      if (string.IsNullOrEmpty(profile.SpriteAssetPath) || !profile.SpriteAssetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Choose a native .asset destination for the keycaps.");
      return string.IsNullOrWhiteSpace(localeCode) ? profile.SpriteAssetPath
        : Path.ChangeExtension(profile.SpriteAssetPath, null) + "_" + NormalizeLocale(localeCode) + ".asset";
    }

    public static InputKeycapBakeResult BakeAll(InputKeycapBakeProfile profile, Func<string, string, string> localize = null)
    {
      if (profile == null || profile.Catalog == null) throw new InvalidOperationException("Choose a keycap profile and runtime catalog.");
      if (profile.Keys == null || profile.Keys.Length == 0) throw new InvalidOperationException("Add at least one keycap row.");
      string[] localeCodes = GetLocaleCodes(profile);
      var issues = new List<string>();
      var groups = new SortedDictionary<string, List<InputGlyphAtlasSource.Glyph>>(StringComparer.Ordinal);
      var textures = new List<Texture2D>();
      var sprites = new List<Sprite>();
      var sources = new List<InputGlyphAtlasSource>();
      InputGlyphCatalog stagingCatalog = null;
      try
      {
        foreach (var key in profile.Keys)
        {
          if (key == null) throw new InvalidOperationException("Remove empty keycap rows.");
          ValidateContentMode(key);
          var content = EffectiveContent(key);
          bool symbol = content == InputKeycapContentMode.Sprite;
          bool localized = !symbol && !string.IsNullOrEmpty(key.LocalizationKey);
          var codes = localized ? localeCodes : new[] { "" };
          if (localized && codes.Length == 0) issues.Add("No locales are configured for " + key.ControlPath + ". Runtime will use a readable text prompt.");
          foreach (string code in codes)
          {
            string identity = key.ControlPath + " [" + (code.Length == 0 ? "neutral" : code) + "] [" + key.Platform + "]";
            string label = localized ? localize?.Invoke(key.LocalizationKey, code) ?? ResolveLocalizedLabel(profile, key.LocalizationKey, code) : key.Label;
            if (localized && string.IsNullOrEmpty(label))
            {
              label = key.Label;
              issues.Add("Missing imported translation for " + identity + ": " + key.LocalizationKey + ". Using readable fallback '" + label + "'.");
            }
            var font = ResolveFont(profile, code);
            if (!symbol)
            {
              if (string.IsNullOrEmpty(label))
              { issues.Add("No readable label for " + identity + ". Runtime will use a text prompt."); continue; }
              if (font == null)
              { issues.Add("No TMP font is configured for " + identity + ". This row was omitted."); continue; }
              if (!font.HasCharacters(label, out uint[] missing, true, false))
              {
                issues.Add("Font/fallback coverage missing for " + identity + ": "
                  + string.Join(", ", missing.Select(value => "U+" + value.ToString("X4"))) + ". This row was omitted; runtime uses text.");
                continue;
              }
            }
            var texture = RenderContent(profile, label, FixedWidth(key), HasSprite(key) ? key.ContentSprite : null,
              key.SpriteSize, font, content == InputKeycapContentMode.SpriteAndLabel);
            textures.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
            sprites.Add(sprite);
            if (!groups.TryGetValue(code, out var rows)) groups.Add(code, rows = new List<InputGlyphAtlasSource.Glyph>());
            rows.Add(new InputGlyphAtlasSource.Glyph {
              ControlPath = key.ControlPath, SpriteName = key.SpriteName + (code.Length == 0 ? "" : "-" + code),
              SourceSprite = sprite, LocaleCode = code, BakedLabel = symbol ? null : label,
              KeycapContent = content, Platform = key.Platform,
              DisplayName = key.Label, LocalizationKey = key.LocalizationKey
            });
          }
        }
        foreach (string code in localeCodes)
          if (profile.Keys.Any(key => key != null && EffectiveContent(key) != InputKeycapContentMode.Sprite
              && !string.IsNullOrEmpty(key.LocalizationKey)) && !groups.ContainsKey(code))
            issues.Add("No renderable localized keycap rows for [" + code + "]; no locale atlas was generated.");

        string owner = ProfileOwner(profile);
        var owned = new HashSet<TMP_SpriteAsset>(profile.PreviousAssets(owner).Where(asset => asset != null));
        foreach (string code in new[] { "" }.Concat(localeCodes))
        {
          var existing = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(GetSpriteAssetPath(profile, code));
          if (existing != null) owned.Add(existing);
        }
        stagingCatalog = ScriptableObject.CreateInstance<InputGlyphCatalog>();
        stagingCatalog.SetEntries(profile.Catalog.Entries.ToArray());
        foreach (var group in groups)
        {
          var source = ScriptableObject.CreateInstance<InputGlyphAtlasSource>();
          sources.Add(source);
          source.Catalog = stagingCatalog;
          source.Family = InputGlyphFamily.KeyboardAndMouse;
          source.SpriteAssetPath = GetSpriteAssetPath(profile, group.Key);
          source.Glyphs = group.Value.ToArray();
          var errors = InputGlyphAtlasAuthoring.Validate(source);
          if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors));
          InputGlyphSpriteAssetBuilder.ValidateDestination(source.SpriteAssetPath);
        }
        var outputs = new List<InputKeycapAtlasOutput>();
        for (int i = 0; i < sources.Count; i++)
        {
          var asset = InputGlyphAtlasAuthoring.Rebuild(sources[i]);
          string code = sources[i].Glyphs[0].LocaleCode;
          outputs.Add(new InputKeycapAtlasOutput(code, asset));
        }
        var current = new HashSet<TMP_SpriteAsset>(outputs.Select(output => output.SpriteAsset));
        var entries = stagingCatalog.Entries.Where(entry => entry != null
          && (entry.Family != InputGlyphFamily.KeyboardAndMouse || !owned.Contains(entry.SpriteAsset) || current.Contains(entry.SpriteAsset))).ToArray();
        Undo.RecordObject(profile.Catalog, "Rebuild localized keyboard atlases");
        profile.Catalog.SetEntries(entries);
        EditorUtility.SetDirty(profile.Catalog);
        if (AssetDatabase.Contains(profile.Catalog)) AssetDatabase.SaveAssetIfDirty(profile.Catalog);
        Undo.RecordObject(profile, "Record keycap atlas outputs");
        profile.RememberAssets(owner, outputs.Select(output => output.SpriteAsset).ToArray());
        EditorUtility.SetDirty(profile);
        if (AssetDatabase.Contains(profile)) AssetDatabase.SaveAssetIfDirty(profile);
        var primary = outputs.FirstOrDefault(output => output.LocaleCode.Length == 0)?.SpriteAsset
          ?? outputs.FirstOrDefault()?.SpriteAsset;
        return new InputKeycapBakeResult(primary, outputs.ToArray(), issues.ToArray());
      }
      finally
      {
        foreach (var source in sources) Object.DestroyImmediate(source);
        if (stagingCatalog != null) Object.DestroyImmediate(stagingCatalog);
        foreach (var sprite in sprites) Object.DestroyImmediate(sprite);
        foreach (var texture in textures) Object.DestroyImmediate(texture);
      }
    }

    public static string LastRenderDiagnostics { get; private set; }
    public static Texture2D Render(InputKeycapBakeProfile profile, string label, int fixedWidth = 0)
      => RenderContent(profile, label, fixedWidth, null, Vector2.zero);

    /// <summary>Previews the exact key row used by Bake, including readable fallback for unassigned artwork.</summary>
    public static Texture2D RenderKey(InputKeycapBakeProfile profile, InputKeycapBakeProfile.Key key, string locale = null)
    {
      if (key == null) throw new InvalidOperationException("Choose a keycap row.");
      ValidateContentMode(key);
      var sprite = HasSprite(key) ? key.ContentSprite : null;
      return RenderContent(profile, ResolveLabel(profile, key, locale), FixedWidth(key), sprite, key.SpriteSize,
        ResolveFont(profile, locale), EffectiveContent(key) == InputKeycapContentMode.SpriteAndLabel);
    }

    private static Texture2D RenderContent(InputKeycapBakeProfile profile, string label, int fixedWidth,
      Sprite contentSprite, Vector2 spriteSize, TMP_FontAsset localeFont = null, bool includeLabel = false)
    {
      bool needsText = contentSprite == null || includeLabel;
      var font = localeFont != null ? localeFont : profile != null ? profile.Font : null;
      if (profile == null || (needsText && font == null)) throw new InvalidOperationException("Choose a TMP font for text keycaps.");
      if (profile.BackgroundPrefab != null && profile.BackgroundPrefab.GetComponent<RectTransform>() == null)
        throw new InvalidOperationException("Background prefab must have a RectTransform.");
      if (profile.BackgroundPrefab != null && (profile.BackgroundPrefab.GetComponentsInChildren<Mask>(true).Length > 0
          || profile.BackgroundPrefab.GetComponentsInChildren<RectMask2D>(true).Length > 0))
        throw new InvalidOperationException("Background prefab clipping masks are unsupported by isolated preview baking. Supply unmasked native UGUI graphics.");
      if (needsText && (string.IsNullOrEmpty(label) || label.IndexOfAny(new[] { '\r', '\n', '\t' }) >= 0))
        throw new InvalidOperationException("Keycap labels must contain one nonempty line of literal text.");
      if (profile.SharedHeight < 8 || profile.SharedHeight > 1024 || profile.MinimumWidth < 8 || profile.MinimumWidth > 4096
        || profile.FontSize < 1 || profile.HorizontalPadding < 0 || profile.VerticalPadding < 0 || profile.BorderWidth < 0
        || profile.SpriteLabelSpacing < 0 || profile.SpriteLabelSpacing > 4096)
        throw new InvalidOperationException("Keycap dimensions, font size and padding are outside supported bounds.");
      if (contentSprite != null && (!ValidSize(spriteSize.x) || !ValidSize(spriteSize.y)))
        throw new InvalidOperationException("Central sprite size must contain finite positive pixel dimensions.");
      if (needsText && !font.HasCharacters(label, out uint[] missing, true, false))
        throw new InvalidOperationException("The selected font/fallbacks lack label glyphs: " + string.Join(", ", missing.Select(value => "U+" + value.ToString("X4"))));
      bool uniform = contentSprite != null ? !needsText : profile.UniformCharacters && new StringInfo(label).LengthInTextElements == 1;
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
        TextMeshProUGUI text = null;
        if (contentSprite != null)
        {
          var icon = new GameObject("Central sprite", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
          icon.transform.SetParent(cap.transform, false);
          var image = icon.GetComponent<Image>();
          ConfigureImage(image, contentSprite, profile.TextColor, InputKeycapImageMode.Simple);
          image.preserveAspect = true;
          var element = icon.GetComponent<LayoutElement>();
          element.minWidth = element.preferredWidth = spriteSize.x;
          element.minHeight = element.preferredHeight = spriteSize.y;
        }
        if (needsText)
        {
        var textHost = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textHost.transform.SetParent(cap.transform, false);
        text = textHost.GetComponent<TextMeshProUGUI>();
        text.font = font;
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
        }
        var layout = cap.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(profile.HorizontalPadding + profile.BorderWidth, profile.HorizontalPadding + profile.BorderWidth,
          profile.VerticalPadding + profile.BorderWidth, profile.VerticalPadding + profile.BorderWidth);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = contentSprite == null;
        layout.spacing = contentSprite != null && needsText ? profile.SpriteLabelSpacing : 0;
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
        if (text != null) text.ForceMeshUpdate();
        var labelSize = text != null ? text.GetPreferredValues(label) : Vector2.zero;
        var preferred = new Vector2(labelSize.x + (contentSprite != null ? spriteSize.x : 0) + layout.spacing,
          Mathf.Max(labelSize.y, contentSprite != null ? spriteSize.y : 0));
        float availableWidth = width - layout.padding.horizontal;
        float availableHeight = profile.SharedHeight - layout.padding.vertical;
        float labelWidth = availableWidth - (contentSprite != null ? spriteSize.x + layout.spacing : 0);
        if (availableWidth <= 0 || availableHeight <= 0 || preferred.x > availableWidth + .5f || preferred.y > availableHeight + .5f
          || (text != null && (text.isTextOverflowing || text.textBounds.size.x > labelWidth + .5f || text.textBounds.size.y > availableHeight + .5f)))
          throw new InvalidOperationException("Central content '" + label + "' does not fit. Increase width/height or reduce content size/padding.");
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
          + " textCull=" + text?.canvasRenderer.cull + " textDepth=" + text?.canvasRenderer.absoluteDepth
          + " textVerts=" + text?.canvasRenderer.GetMesh()?.vertexCount + " textMats=" + text?.canvasRenderer.materialCount
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
      => BakeAll(profile, localize).PrimaryAsset;

    public static string ResolveLabel(InputKeycapBakeProfile profile, InputKeycapBakeProfile.Key key, string locale)
      => string.IsNullOrEmpty(key.LocalizationKey) ? key.Label : ResolveLocalizedLabel(profile, key.LocalizationKey, locale) ?? key.Label;

    /// <summary>Read-only comparison against the currently imported native tables. Rebuild explicitly after reviewing stale rows.</summary>
    public static string[] ValidateBakedContent(InputKeycapBakeProfile profile)
    {
      var issues = new List<string>();
      if (profile == null || profile.Catalog == null) return new[] { "Choose a bake profile and runtime catalog." };
      if (profile.Keys == null || profile.Keys.Length == 0) return new[] { "Add at least one keycap row." };
      string[] localeCodes = GetLocaleCodes(profile);
      foreach (var key in profile.Keys)
      {
        if (key == null) { issues.Add("Remove empty keycap rows."); continue; }
        if (!Enum.IsDefined(typeof(InputKeycapContentMode), key.ContentMode))
        { issues.Add("Select a known content mode: " + key.ControlPath); continue; }
        if (!Enum.IsDefined(typeof(InputGlyphPlatform), key.Platform))
        { issues.Add("Select a known platform: " + key.ControlPath); continue; }
        var content = EffectiveContent(key);
        bool symbol = content == InputKeycapContentMode.Sprite;
        bool localized = !symbol && !string.IsNullOrEmpty(key.LocalizationKey);
        var locales = localized ? localeCodes : new[] { "" };
        if (locales == null || locales.Length == 0) { issues.Add("Configure locales for " + key.ControlPath); continue; }
        foreach (string locale in locales)
        {
          string code = (locale ?? "").Trim().Replace('_', '-').ToLowerInvariant();
          string label = ResolveLabel(profile, key, code);
          string identity = key.ControlPath + " [" + (code.Length == 0 ? "neutral" : code) + "] [" + key.Platform + "]";
          if (localized && ResolveLocalizedLabel(profile, key.LocalizationKey, code) == null)
            issues.Add("Missing imported translation for " + identity + ": " + key.LocalizationKey + ". Using readable fallback '" + label + "'.");
          if (!symbol)
          {
            var font = ResolveFont(profile, code);
            if (string.IsNullOrEmpty(label)) issues.Add("No readable label for " + identity + ".");
            else if (font == null) issues.Add("No TMP font is configured for " + identity + ".");
            else if (!font.HasCharacters(label, out uint[] missing, true, false))
              issues.Add("Font/fallback coverage missing for " + identity + ": "
                + string.Join(", ", missing.Select(value => "U+" + value.ToString("X4"))) + ".");
          }
          var asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(GetSpriteAssetPath(profile, code));
          string name = key.SpriteName + (code.Length == 0 ? "" : "-" + code);
          var entry = profile.Catalog.Entries.FirstOrDefault(candidate => candidate != null
            && candidate.Family == InputGlyphFamily.KeyboardAndMouse
            && string.Equals(candidate.ControlPath, key.ControlPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals((candidate.LocaleCode ?? "").Trim().Replace('_', '-'), code, StringComparison.OrdinalIgnoreCase)
            && candidate.KeycapContent == content && candidate.Platform == key.Platform && candidate.SpriteAsset == asset
            && string.Equals(candidate.SpriteName, name, StringComparison.Ordinal));
          if (entry == null || (!symbol && !string.Equals(entry.BakedLabel, label, StringComparison.Ordinal)))
            issues.Add("Rebake " + identity + ": expected " + (symbol ? "a locale-neutral Sprite entry" : "current label '" + label + "'") + ".");
          else if (asset == null || asset.spriteSheet == null || asset.material == null || asset.GetSpriteIndexFromName(name) < 0)
            issues.Add("Missing native sprite resource for " + identity + ": " + name);
        }
      }
      return issues.ToArray();
    }

    private static string NormalizeLocale(string code)
    {
      string normalized = (code ?? "").Trim().Replace('_', '-').ToLowerInvariant();
      if (!Regex.IsMatch(normalized, "^[a-z0-9]+(?:-[a-z0-9]+)*$"))
        throw new InvalidOperationException("Choose a locale code without paths or punctuation: " + code);
      return normalized;
    }

    private static string ProfileOwner(InputKeycapBakeProfile profile)
    {
      string path = AssetDatabase.GetAssetPath(profile);
      string guid = string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path);
      return string.IsNullOrEmpty(guid) ? "instance:" + profile.GetInstanceID() : guid;
    }

    private static TMP_FontAsset ResolveFont(InputKeycapBakeProfile profile, string locale)
    {
      if (profile == null) return null;
      string code = string.IsNullOrWhiteSpace(locale) ? "" : NormalizeLocale(locale);
      while (code.Length > 0)
      {
        var font = profile.LocaleFontOverrides?.FirstOrDefault(item => item != null && item.Font != null
          && !string.IsNullOrWhiteSpace(item.LocaleCode)
          && string.Equals(NormalizeLocale(item.LocaleCode), code, StringComparison.Ordinal))?.Font;
        if (font != null) return font;
        int separator = code.LastIndexOf('-');
        code = separator < 0 ? "" : code.Substring(0, separator);
      }
      return profile.Font;
    }

    private static bool HasSprite(InputKeycapBakeProfile.Key key)
      => (key.ContentMode == InputKeycapContentMode.Sprite || key.ContentMode == InputKeycapContentMode.SpriteAndLabel)
        && key.ContentSprite != null;

    private static InputKeycapContentMode EffectiveContent(InputKeycapBakeProfile.Key key)
      => HasSprite(key) ? key.ContentMode : InputKeycapContentMode.Label;

    private static bool ValidSize(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0 && value <= 4096;

    private static void ValidateContentMode(InputKeycapBakeProfile.Key key)
    {
      if (!Enum.IsDefined(typeof(InputKeycapContentMode), key.ContentMode))
        throw new InvalidOperationException("Select a known keycap content mode: " + key.ControlPath);
      if (!Enum.IsDefined(typeof(InputGlyphPlatform), key.Platform))
        throw new InvalidOperationException("Select a known glyph platform: " + key.ControlPath);
    }

    private static int FixedWidth(InputKeycapBakeProfile.Key key)
    {
      if (!Enum.IsDefined(typeof(InputKeycapWidthRule), key.WidthRule))
        throw new InvalidOperationException("Select a known width rule: " + key.ControlPath);
      int width = key.WidthRule == InputKeycapWidthRule.Fixed ? key.FixedWidth : 0;
      if (key.WidthRule == InputKeycapWidthRule.Fixed && width <= 0)
        throw new InvalidOperationException("Fixed width must be positive: " + key.ControlPath);
      return width;
    }
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
