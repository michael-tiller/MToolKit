#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MToolKit.Runtime.Utilities;
using UnityEditor;
using UnityEngine;

namespace MToolKit.Theme.Editor
{
  /// <summary>
  /// Lean theme-authoring window: browse / create / rename / duplicate / delete / edit the four
  /// theme element types. Modeled on Dirigible's ContentConfigurator but stripped to the uniform
  /// theme model — every element is a <see cref="SemanticScriptableObject"/> with an Id, so one
  /// generic list + the asset's own (Odin) inspector covers all of them.
  /// </summary>
  public class ThemeConfiguratorWindow : EditorWindow
  {
    private enum Tab { Themes, Swatches, Typesets, Spacings }

    // ponytail: one table drives tabs, counts, list filtering and create — no per-type duplication.
    private static readonly (Tab tab, string label, Type type)[] Tabs =
    {
      (Tab.Themes,   "Themes",   typeof(Theme)),
      (Tab.Swatches, "Swatches", typeof(MToolKit.Theme.Swatch.Swatch)),
      (Tab.Typesets, "Typesets", typeof(MToolKit.Theme.Typeset.Typeset)),
      (Tab.Spacings, "Spacings", typeof(MToolKit.Theme.Spacing.Spacing)),
    };

    private const float ListWidth = 240f;

    private Tab _tab = Tab.Swatches;
    private string _search = "";
    private Vector2 _listScroll, _inspectorScroll;

    private ScriptableObject _selected;  // strong ref so it survives domain reloads
    private UnityEditor.Editor _editor;  // cached inspector for _selected
    private string _renameBuffer = "";

    [MenuItem("Tools/MToolKit/Theme Configurator")]
    public static void ShowWindow()
    {
      var w = GetWindow<ThemeConfiguratorWindow>();
      w.titleContent = new GUIContent("Theme Configurator", EditorGUIUtility.IconContent("d_ScriptableObject Icon").image);
      w.minSize = new Vector2(640, 400);
      w.Show();
    }

    private void OnDisable() => DestroyEditor();

    private Type CurrentType => Tabs.First(t => t.tab == _tab).type;

    private void OnGUI()
    {
      DrawToolbar();
      EditorGUILayout.BeginHorizontal();
      DrawList();
      DrawInspector();
      EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
      EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
      foreach (var (tab, label, type) in Tabs)
      {
        int count = AssetDatabase.FindAssets($"t:{type.Name}").Length;
        var style = _tab == tab ? EditorStyles.toolbarButton : EditorStyles.miniButton;
        if (GUILayout.Toggle(_tab == tab, $"{label} ({count})", style, GUILayout.Width(110)) && _tab != tab)
        {
          _tab = tab;
          Select(null);
        }
      }
      GUILayout.FlexibleSpace();
      GUILayout.Label("Search:", GUILayout.Width(50));
      _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Width(160));
      if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(40)))
        CreateNew();
      EditorGUILayout.EndHorizontal();
    }

    private void DrawList()
    {
      EditorGUILayout.BeginVertical(GUILayout.Width(ListWidth));
      _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

      var assets = GetAssets(CurrentType);
      if (assets.Count == 0)
        EditorGUILayout.HelpBox($"No {_tab}. Click New.", MessageType.Info);

      foreach (var a in assets)
      {
        var rect = EditorGUILayout.GetControlRect(false, 22);
        if (a == _selected)
          EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.9f, 0.5f));

        var iconRect = new Rect(rect.x + 2, rect.y + 2, 18, 18);
        var preview = AssetPreview.GetAssetPreview(a);  // free Swatch color chip via SwatchEditor.RenderStaticPreview
        if (preview != null) GUI.DrawTexture(iconRect, preview, ScaleMode.ScaleToFit);

        var labelRect = new Rect(rect.x + 24, rect.y + 2, rect.width - 26, 18);
        GUI.Label(labelRect, Label(a));

        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        {
          if (Event.current.clickCount == 2) { Selection.activeObject = a; EditorGUIUtility.PingObject(a); }
          else Select(a);
          Event.current.Use();
          Repaint();
        }
      }

      EditorGUILayout.EndScrollView();
      EditorGUILayout.EndVertical();
    }

    private void DrawInspector()
    {
      EditorGUILayout.BeginVertical();
      EnsureEditorFresh();

      if (_selected == null)
      {
        EditorGUILayout.HelpBox("Select an element to edit, or click New.", MessageType.None);
        EditorGUILayout.EndVertical();
        return;
      }

      // Rename row + Id readout.
      EditorGUILayout.BeginHorizontal();
      _renameBuffer = EditorGUILayout.TextField("Asset Name", _renameBuffer);
      using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_renameBuffer) || _renameBuffer == _selected.name))
      {
        if (GUILayout.Button("Rename", GUILayout.Width(70)))
          Rename(_selected, _renameBuffer.Trim());
      }
      EditorGUILayout.EndHorizontal();
      EditorGUILayout.LabelField("Resolved Id", (_selected as SemanticScriptableObject)?.Id ?? "(n/a)");
      EditorGUILayout.Space(4);

      _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);
      _editor?.OnInspectorGUI();
      EditorGUILayout.EndScrollView();

      EditorGUILayout.Space(6);
      EditorGUILayout.BeginHorizontal();
      // A Theme IS its folder, so duplicating one deep-clones the whole folder (relinked); the other
      // element types are single assets.
      if (GUILayout.Button(_tab == Tab.Themes ? "Duplicate Folder" : "Duplicate")) Duplicate(_selected);
      if (GUILayout.Button("Delete")) Delete(_selected);
      if (GUILayout.Button("Ping")) { Selection.activeObject = _selected; EditorGUIUtility.PingObject(_selected); }
      EditorGUILayout.EndHorizontal();

      EditorGUILayout.EndVertical();
    }

    // ── data ────────────────────────────────────────────────────────────────

    private List<ScriptableObject> GetAssets(Type type)
    {
      return AssetDatabase.FindAssets($"t:{type.Name}")
        .Select(g => AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(g)))
        .Where(a => a != null && type.IsInstanceOfType(a))  // IsInstanceOfType guards t: name collisions
        .Where(a => string.IsNullOrEmpty(_search) || Label(a).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
        .OrderBy(a => a.name, StringComparer.OrdinalIgnoreCase)
        .ToList();
    }

    private static string Label(ScriptableObject a)
    {
      string id = (a as SemanticScriptableObject)?.Id;
      return string.IsNullOrEmpty(id) || id == a.name ? a.name : $"{a.name}  ({id})";
    }

    // ── actions ─────────────────────────────────────────────────────────────

    private void Select(ScriptableObject asset)
    {
      _selected = asset;
      _renameBuffer = asset != null ? asset.name : "";
      DestroyEditor();
      if (asset != null) _editor = UnityEditor.Editor.CreateEditor(asset);
    }

    private void CreateNew()
    {
      Type type = CurrentType;
      string dir = _selected != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(_selected)) : "Assets";
      string path = EditorUtility.SaveFilePanelInProject(
        $"New {type.Name}", $"New{type.Name}", "asset", "Create a new theme element", dir);
      if (string.IsNullOrEmpty(path)) return;

      var asset = CreateInstance(type) as ScriptableObject;
      AssetDatabase.CreateAsset(asset, path);
      ThemeRegistryTools.Register(asset);  // auto-add to the nearest matching registry
      AssetDatabase.SaveAssets();
      Select(asset);
    }

    private void Duplicate(ScriptableObject asset)
    {
      if (_tab == Tab.Themes)
      {
        DuplicateThemeFolder(asset);
        return;
      }

      string src = AssetDatabase.GetAssetPath(asset);
      string dst = AssetDatabase.GenerateUniqueAssetPath(
        $"{Path.GetDirectoryName(src)}/{Path.GetFileNameWithoutExtension(src)}_copy.asset".Replace("\\", "/"));
      if (!AssetDatabase.CopyAsset(src, dst)) return;

      var copy = AssetDatabase.LoadAssetAtPath<ScriptableObject>(dst);
      // Blank the copy's SemId so its Id falls back to the unique "_copy" filename instead of
      // colliding with the source's Id. Set via SerializedProperty because the setter is protected.
      var so = new SerializedObject(copy);
      var semId = so.FindProperty("semId");
      if (semId != null) { semId.stringValue = ""; so.ApplyModifiedPropertiesWithoutUndo(); }

      ThemeRegistryTools.Register(copy, asset);  // add next to the source in its registry
      AssetDatabase.SaveAssets();
      Select(copy);
    }

    /// Deep-clone the selected theme's whole folder (relinked) via the reusable tool, then select the clone.
    private void DuplicateThemeFolder(ScriptableObject theme)
    {
      string themePath = AssetDatabase.GetAssetPath(theme);
      string folder = Path.GetDirectoryName(themePath).Replace("\\", "/");
      string dst = ThemeFolderTools.CloneThemeFolder(folder);
      if (string.IsNullOrEmpty(dst)) return;

      var folderObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dst);
      if (folderObj != null) EditorGUIUtility.PingObject(folderObj);
      Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>($"{dst}/{Path.GetFileName(themePath)}"));
    }

    private void Rename(ScriptableObject asset, string newName)
    {
      string err = AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(asset), newName);
      if (!string.IsNullOrEmpty(err)) { EditorUtility.DisplayDialog("Rename failed", err, "OK"); return; }
      AssetDatabase.SaveAssets();
      _renameBuffer = asset.name;
      GUI.FocusControl(null);
    }

    private void Delete(ScriptableObject asset)
    {
      if (!EditorUtility.DisplayDialog("Delete", $"Delete '{asset.name}'? This cannot be undone.", "Delete", "Cancel"))
        return;
      string path = AssetDatabase.GetAssetPath(asset);
      Select(null);
      AssetDatabase.DeleteAsset(path);
    }

    // ── editor lifecycle ──────────────────────────────────────────────────────

    /// Recreate the cached inspector if a domain reload invalidated it (target becomes Unity-null).
    private void EnsureEditorFresh()
    {
      if (_selected == null) return;
      if (_editor != null && _editor.target != null) return;
      DestroyEditor();
      _editor = UnityEditor.Editor.CreateEditor(_selected);
    }

    private void DestroyEditor()
    {
      if (_editor != null) { DestroyImmediate(_editor); _editor = null; }
    }
  }
}
#endif
