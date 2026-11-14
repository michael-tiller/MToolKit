using System;
using System.Linq;
using MToolKit.Runtime.Input.Prompts;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace MToolKit.Editor.Input
{
  public sealed class InputGlyphAtlasWindow : EditorWindow
  {
    [SerializeField] private ScriptableObject profile;
    [SerializeField] private TMP_SpriteAsset importAtlas;
    private Vector2 scroll;
    private string message;
    private bool failed;
    private bool warning;
    private Texture2D preview;
    private int previewKey, previewLocale;
    private bool showAllKeys;

    [MenuItem("Tools/MToolKit/Input Glyphs/Atlas Wizard")]
    public static void Open() => GetWindow<InputGlyphAtlasWindow>("Input Glyph Atlas");

    private void OnEnable()
    {
      if (Selection.activeObject is InputGlyphAtlasSource || Selection.activeObject is InputKeycapBakeProfile)
        profile = Selection.activeObject as ScriptableObject;
    }

    private void OnDisable() { if (preview != null) DestroyImmediate(preview); }

    private void OnGUI()
    {
      EditorGUILayout.LabelField("Native TMP input glyph authoring", EditorStyles.boldLabel);
      EditorGUILayout.HelpBox("Choose a saved source recipe. Replace source Sprites or bake styled keyboard labels, then rebuild native TMP resources. Existing named asset references are retained.", MessageType.Info);
      profile = EditorGUILayout.ObjectField("Source profile", profile, typeof(ScriptableObject), false) as ScriptableObject;
      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("New sprite atlas source")) Create<InputGlyphAtlasSource>();
      if (GUILayout.Button("New keyboard cap profile")) Create<InputKeycapBakeProfile>();
      EditorGUILayout.EndHorizontal();
      if (profile == null) return;
      if (!(profile is InputGlyphAtlasSource) && !(profile is InputKeycapBakeProfile))
      { EditorGUILayout.HelpBox("Select an InputGlyphAtlasSource or InputKeycapBakeProfile.", MessageType.Error); return; }
      scroll = EditorGUILayout.BeginScrollView(scroll);
      var serialized = new SerializedObject(profile);
      serialized.Update();
      var iterator = serialized.GetIterator();
      bool enter = true;
      while (iterator.NextVisible(enter))
      {
        enter = false;
        if (iterator.name == "m_Script") continue;
        if (profile is InputKeycapBakeProfile selected
          && (iterator.name == nameof(InputKeycapBakeProfile.Keys)
            || (selected.LocaleSource == InputKeycapLocaleSource.ProjectEnabled && iterator.name == nameof(InputKeycapBakeProfile.Locales)))) continue;
        EditorGUILayout.PropertyField(iterator, true);
      }
      serialized.ApplyModifiedProperties();
      var catalog = profile is InputGlyphAtlasSource source ? source.Catalog : ((InputKeycapBakeProfile)profile).Catalog;
      if (catalog != null)
      {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime keyboard presentation", EditorStyles.boldLabel);
        var config = new SerializedObject(catalog);
        config.Update();
        EditorGUILayout.PropertyField(config.FindProperty("keyboardGlyphMode"));
        config.ApplyModifiedProperties();
      }
      if (profile is InputGlyphAtlasSource atlasSource)
      {
        importAtlas = EditorGUILayout.ObjectField("Atlas to import", importAtlas, typeof(TMP_SpriteAsset), false) as TMP_SpriteAsset;
        if (GUILayout.Button("Load current catalog mappings")) Execute(() => {
          Undo.RecordObject(atlasSource, "Import input glyph mappings");
          InputGlyphAtlasAuthoring.ImportFromCatalog(atlasSource, importAtlas);
          message = "Loaded semantic mappings and native Sprite sources. Replace artwork or add rows before rebuilding.";
        });
        foreach (string error in InputGlyphAtlasAuthoring.Validate(atlasSource)) EditorGUILayout.HelpBox(error, MessageType.Warning);
      }
      else if (profile is InputKeycapBakeProfile keycaps)
      {
        EditorGUILayout.HelpBox("Import translations with Unity Localization/Google Sheets first, then select the native collection and locale source here. Neutral characters and symbols use the base atlas; localized labels always use a separate sibling atlas per language. Validate saved caps, explicitly rebuild, and preview. Existing collection/table references survive moves; select a replacement collection after imports that create new assets.", MessageType.Info);
        string[] resolvedLocales;
        try { resolvedLocales = InputKeycapBaker.GetLocaleCodes(keycaps); }
        catch (Exception error) { resolvedLocales = Array.Empty<string>(); EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
        EditorGUILayout.LabelField("Resolved bake locales", resolvedLocales.Length == 0 ? "None" : string.Join(", ", resolvedLocales));
        if (keycaps.LocaleSource == InputKeycapLocaleSource.ProjectEnabled)
          EditorGUILayout.HelpBox("Using all enabled Unity Localization project locales. The saved manual locale list is ignored. Missing translations and font coverage are reported; locale font overrides use native TMP fallbacks.", MessageType.Info);
        EditorGUILayout.HelpBox("Single characters and symbols share the standard key width. Sprite centers neutral artwork; SpriteAndLabel puts the icon and localized name inside one cap with configurable spacing. Unassigned artwork keeps a readable label. FitLabel uses native ContentSizeFitter for the whole row; Fixed supports wider special keys. Content must fit without clipping.", MessageType.Info);
        if (GUILayout.Button("Validate saved keycaps against imported tables")) Execute(() => {
          var issues = InputKeycapBaker.ValidateBakedContent(keycaps);
          warning = issues.Length > 0;
          message = issues.Length == 0 ? "Saved keycap entries match the imported labels and expected content modes. Rebuild after changing artwork or styling."
            : string.Join("\n", issues);
        });
        if (GUILayout.Button("Add missing standard keys")) Execute(() => {
          Undo.RecordObject(keycaps, "Add missing standard input keys");
          message = keycaps.EnsureStandardKeys() ? "Added missing standard keys. Authored rows are preserved; rebuild when ready."
            : "All standard keys are already configured.";
        });
        serialized.Update();
        var keys = serialized.FindProperty(nameof(InputKeycapBakeProfile.Keys));
        if (keys.arraySize > 0)
        {
          previewKey = Mathf.Clamp(previewKey, 0, keys.arraySize - 1);
          previewKey = EditorGUILayout.Popup("Key to edit", previewKey,
            keycaps.Keys.Select(key => key == null ? "Empty row" : key.ControlPath + " [" + key.Platform + "]").ToArray());
          var selectedKey = keys.GetArrayElementAtIndex(previewKey);
          var field = selectedKey.Copy();
          var end = selectedKey.GetEndProperty();
          bool child = true;
          while (field.NextVisible(child) && !SerializedProperty.EqualContents(field, end))
          {
            child = false;
            EditorGUILayout.PropertyField(field, true);
          }
        }
        else EditorGUILayout.HelpBox("Add standard keys or use the full key list to create custom rows.", MessageType.Info);
        bool expand = EditorGUILayout.Foldout(showAllKeys, "All key rows (add or remove)", true);
        if (expand && !showAllKeys) keys.isExpanded = true;
        showAllKeys = expand;
        if (showAllKeys) EditorGUILayout.PropertyField(keys, true);
        serialized.ApplyModifiedProperties();
        if (keycaps.Keys != null && keycaps.Keys.Length > 0)
        {
          previewKey = Mathf.Clamp(previewKey, 0, keycaps.Keys.Length - 1);
          var locales = resolvedLocales.Length > 0 ? resolvedLocales : new[] { "" };
          previewLocale = Mathf.Clamp(previewLocale, 0, locales.Length - 1);
          previewLocale = EditorGUILayout.Popup("Preview locale", previewLocale, locales);
          if (GUILayout.Button("Render native keycap preview")) Execute(() => {
            var key = keycaps.Keys[previewKey];
            var next = InputKeycapBaker.RenderKey(keycaps, key, locales[previewLocale]);
            if (preview != null) DestroyImmediate(preview);
            preview = next;
            message = "Native UGUI/TMP preview rendered. Rebuild to save the atlas and catalog mappings.";
          });
          if (preview != null)
          {
            var rect = GUILayoutUtility.GetRect(Math.Min(position.width - 30, preview.width), preview.height);
            GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit, true);
          }
        }
      }
      if (GUILayout.Button("Validate and rebuild native atlases", GUILayout.Height(30))) Execute(() => {
        TMP_SpriteAsset result;
        if (profile is InputGlyphAtlasSource atlas)
        {
          result = InputGlyphAtlasAuthoring.Rebuild(atlas);
          message = "Saved native atlas, named glyphs and catalog mappings.";
        }
        else
        {
          var baked = InputKeycapBaker.BakeAll((InputKeycapBakeProfile)profile);
          result = baked.PrimaryAsset;
          warning = baked.Issues.Count > 0;
          message = "Saved " + baked.Atlases.Count + " separate native atlas output(s)."
            + (warning ? "\n" + string.Join("\n", baked.Issues) : " All requested labels have native font coverage.");
        }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        if (result != null) EditorGUIUtility.PingObject(result);
      });
      if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, failed ? MessageType.Error : warning ? MessageType.Warning : MessageType.Info);
      EditorGUILayout.EndScrollView();
    }

    private void Execute(Action action)
    {
      try { failed = warning = false; action(); }
      catch (Exception error) { failed = true; message = error.Message; }
    }

    private void Create<T>() where T : ScriptableObject
    {
      InputGlyphSpriteAssetBuilder.EnsureFolder("Assets/Editor/InputGlyphs");
      string path = EditorUtility.SaveFilePanelInProject("Create glyph source profile", typeof(T).Name, "asset", "Choose an editor-only source recipe location.", "Assets/Editor/InputGlyphs");
      if (string.IsNullOrEmpty(path)) return;
      var created = CreateInstance<T>();
      AssetDatabase.CreateAsset(created, path);
      profile = created;
      Selection.activeObject = created;
      message = null;
    }
  }
}
