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
    private Texture2D preview;
    private int previewKey, previewLocale;

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
        EditorGUILayout.HelpBox("Single characters share the standard key width. FitLabel uses native ContentSizeFitter; Fixed supports wider Enter, Backspace, Shift and Space rules. Localized labels are baked per configured locale. Fonts, padding and fixed widths must fit without clipping.", MessageType.Info);
        if (keycaps.Keys != null && keycaps.Keys.Length > 0)
        {
          previewKey = Mathf.Clamp(previewKey, 0, keycaps.Keys.Length - 1);
          previewKey = EditorGUILayout.Popup("Preview key", previewKey, keycaps.Keys.Select(key => key?.ControlPath ?? "Empty row").ToArray());
          var locales = keycaps.Locales != null && keycaps.Locales.Length > 0 ? keycaps.Locales : new[] { "" };
          previewLocale = Mathf.Clamp(previewLocale, 0, locales.Length - 1);
          previewLocale = EditorGUILayout.Popup("Preview locale", previewLocale, locales);
          if (GUILayout.Button("Render native keycap preview")) Execute(() => {
            var key = keycaps.Keys[previewKey];
            var next = InputKeycapBaker.Render(keycaps, InputKeycapBaker.ResolveLabel(keycaps, key, locales[previewLocale]),
              key.WidthRule == InputKeycapWidthRule.Fixed ? key.FixedWidth : 0);
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
      if (GUILayout.Button("Validate and rebuild native atlas", GUILayout.Height(30))) Execute(() => {
        TMP_SpriteAsset result = profile is InputGlyphAtlasSource atlas ? InputGlyphAtlasAuthoring.Rebuild(atlas)
          : InputKeycapBaker.Bake((InputKeycapBakeProfile)profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        EditorGUIUtility.PingObject(result);
        message = "Saved native atlas, material, named glyphs and catalog mappings. Other device-family atlases are unchanged.";
      });
      if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, failed ? MessageType.Error : MessageType.Info);
      EditorGUILayout.EndScrollView();
    }

    private void Execute(Action action)
    {
      try { failed = false; action(); }
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