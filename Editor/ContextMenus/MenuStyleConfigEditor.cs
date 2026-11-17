using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MToolKit.Editor.ContextMenus
{
  [CustomEditor(typeof(MenuStyleConfig))]
  public sealed class MenuStyleConfigEditor : UnityEditor.Editor
  {
    private MenuStyleBuildPlan plan;
    private string message;
    private bool failed, commonExpanded = true, commonDetails;
    private UnityEditor.Editor presentationInspector;
    private string expandedSource;
    private int previewIndex;
    private readonly MenuThemeOverridesGUI themeOverrides = new MenuThemeOverridesGUI();
    private readonly HashSet<string> composition = new HashSet<string>();
    private static readonly HashSet<string> BaseFields = new HashSet<string> {
      "Id", "AssetName", "Output", "PresenterPrefab", "ItemPrefab", "CenterPrefab", "SecondaryPrefab",
      "ThemeOverrides", "ThemeSet", "Skin", "OverrideShowCenter", "ShowCenter", "OverrideInputBindings", "InputBindings", "MaximumVisibleItems", "WidgetSize", "Navigation"
    };

    private void OnDisable()
    {
      themeOverrides.Dispose();
      if (presentationInspector != null) DestroyImmediate(presentationInspector);
    }

    public override void OnInspectorGUI()
    {
      var config = (MenuStyleConfig)target;
      serializedObject.Update();
      string before = EditorJsonUtility.ToJson(config);
      string commonBefore = config.Common != null ? EditorJsonUtility.ToJson(config.Common) : null;
      string pendingPreview = null, pendingRebuild = null;
      bool themeChanged = false;
      EditorGUILayout.HelpBox("Theme tokens resolve per-style state/default, shared state/default, then fallback skin/native defaults. " +
        "Rebuild applies references and layout to runtime profiles. Existing GUIDs and custom prefab contents are retained.", MessageType.Info);
      EditorGUILayout.PropertyField(serializedObject.FindProperty("Common"));
      EditorGUILayout.PropertyField(serializedObject.FindProperty("OutputFolder"));
      serializedObject.ApplyModifiedProperties();
      themeChanged |= DrawCommon(config);
      var styles = serializedObject.FindProperty("Styles");
      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Styles", EditorStyles.boldLabel);
      for (int i = 0; i < styles.arraySize; i++)
      {
        var property = styles.GetArrayElementAtIndex(i);
        var recipe = property.managedReferenceValue as MenuStyleRecipe;
        if (recipe == null)
        {
          EditorGUILayout.HelpBox("Missing recipe. Remove this entry or assign a supported recipe.", MessageType.Warning);
          if (GUILayout.Button("Remove missing style")) { Remove(config, i); GUIUtility.ExitGUI(); }
          continue;
        }
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        property.isExpanded = EditorGUILayout.Foldout(property.isExpanded,
          recipe.DisplayName + " — " + recipe.AssetName, true);
        if (property.isExpanded)
        {
          Draw(property, "AssetName", "Output");
          if (recipe.PresentationThemeType != null) themeChanged |= DrawPresentationSource(property, recipe, config);
          bool hasSource = property.FindPropertyRelative("ThemeSet")?.objectReferenceValue != null;
          bool open = composition.Contains(recipe.Id);
          bool next = EditorGUILayout.Foldout(open, hasSource ? "Advanced inline fallback (inactive)" : "Advanced inline fallback (active)", true);
          if (next) composition.Add(recipe.Id); else composition.Remove(recipe.Id);
          if (next)
          {
            EditorGUI.indentLevel++;
            if (hasSource) EditorGUILayout.HelpBox("The assigned typed source owns visual layout/composition. These retained fields apply only after clearing Theme set.", MessageType.None);
            else EditorGUILayout.HelpBox("These inline values remain active until a typed Theme set is assigned. Empty prefabs inherit Common, then retain an existing output reference.", MessageType.None);
            themeChanged |= themeOverrides.Draw(property.FindPropertyRelative("ThemeOverrides"), "style:" + recipe.Id,
              "Legacy per-style state layer", recipe.AssetName + "_ThemeOverrides", config.OutputFolder);
            Draw(property, "PresenterPrefab", "ItemPrefab", "CenterPrefab", "SecondaryPrefab");
            DrawFallbackSkin(property.FindPropertyRelative("Skin"));
            Draw(property, "OverrideShowCenter");
            if (property.FindPropertyRelative("OverrideShowCenter").boolValue) Draw(property, "ShowCenter");
            Draw(property, "MaximumVisibleItems", "WidgetSize");
            var iterator = property.Copy();
            int depth = property.depth;
            bool enter = true;
            while (iterator.NextVisible(enter) && iterator.depth > depth)
            {
              enter = false;
              if (iterator.depth == depth + 1 && !BaseFields.Contains(iterator.name)) EditorGUILayout.PropertyField(iterator, true);
            }
            EditorGUI.indentLevel--;
          }
          Draw(property, "Navigation", "OverrideInputBindings");
          if (property.FindPropertyRelative("OverrideInputBindings").boolValue) Draw(property, "InputBindings");
          EditorGUILayout.BeginHorizontal();
          if (GUILayout.Button("Geometry")) previewIndex = i;
          if (GUILayout.Button("Preview changes")) pendingPreview = recipe.Id;
          using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("Rebuild this style")) pendingRebuild = recipe.Id;
          if (GUILayout.Button("Remove", GUILayout.Width(64))) { Remove(config, i); GUIUtility.ExitGUI(); }
          EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
      }
      serializedObject.ApplyModifiedProperties();
      if (themeChanged || before != EditorJsonUtility.ToJson(config) ||
          commonBefore != (config.Common != null ? EditorJsonUtility.ToJson(config.Common) : null))
      { plan = null; message = null; }
      if (pendingPreview != null) Execute(() => Refresh(config, pendingPreview));
      if (pendingRebuild != null) Execute(() => Rebuild(config, pendingRebuild));
      DrawAddButtons(config);
      EditorGUILayout.Space();
      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("Preview all changes")) Execute(() => Refresh(config, null));
      using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        if (GUILayout.Button("Rebuild all styles")) Execute(() => Rebuild(config, null));
      EditorGUILayout.EndHorizontal();
      DrawPlan();
      if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, failed ? MessageType.Error : MessageType.Info);
      if (config.Styles != null && config.Styles.Length > 0)
      {
        previewIndex = Mathf.Clamp(previewIndex, 0, config.Styles.Length - 1);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Geometry preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Layout sketch with up to 12 sample actions. This is not a live rendering of custom prefab graphics, actual actions, fonts, animations or viewport fitting.", MessageType.None);
        var options = config.Styles.Select(value => value == null ? "Missing style" : value.DisplayName + " — " + value.AssetName).ToArray();
        previewIndex = EditorGUILayout.Popup("Preview style", previewIndex, options);
        var area = GUILayoutUtility.GetRect(220, 320, GUILayout.ExpandWidth(true));
        MenuStyleLayoutPreview.Draw(area, config.Common, config.Styles[previewIndex]);
      }
    }

    private bool DrawCommon(MenuStyleConfig config)
    {
      if (config.Common == null) return false;
      commonExpanded = EditorGUILayout.Foldout(commonExpanded, "Shared common settings", true);
      if (!commonExpanded) return false;
      bool changed = false;
      using (new EditorGUI.IndentLevelScope())
      using (var common = new SerializedObject(config.Common))
      {
        common.Update();
        changed |= themeOverrides.Draw(common.FindProperty("ThemeOverrides"), "common:" + config.Common.GetInstanceID(),
          "MTK theme overrides (shared)", config.Common.name + "_ThemeOverrides", config.OutputFolder);
        commonDetails = EditorGUILayout.Foldout(commonDetails, "Input, accessibility and host composition", true);
        if (commonDetails)
        {
          DrawFallbackSkin(common.FindProperty("Skin"));
          Draw(common, "ItemPrefab", "CenterPrefab", "SecondaryPrefab", "FallbackProfile", "ShowCenter", "Backdrop");
          EditorGUILayout.LabelField("Input", EditorStyles.boldLabel);
          Draw(common, "InputBindings", "AnalogDeadZone", "AnalogHysteresis", "AnalogSelectionResetDelay");
          EditorGUILayout.LabelField("Accessibility and placement", EditorStyles.boldLabel);
          Draw(common, "Anchor", "MinimumTargetSize", "ReducedMotion", "Transition");
        }
        changed |= common.ApplyModifiedProperties();
      }
      return changed;
    }

    private bool DrawPresentationSource(SerializedProperty property, MenuStyleRecipe recipe, MenuStyleConfig config)
    {
      var slot = property.FindPropertyRelative("ThemeSet");
      EditorGUILayout.BeginHorizontal();
      EditorGUI.BeginChangeCheck();
      EditorGUILayout.PropertyField(slot, new GUIContent("Theme set"));
      bool changed = EditorGUI.EndChangeCheck();
      if (GUILayout.Button("Create", GUILayout.Width(52)))
      {
        string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(config))?.Replace('\\', '/');
        string path = EditorUtility.SaveFilePanelInProject("Create " + recipe.DisplayName + " theme source", recipe.AssetName + "_Theme",
          "asset", "Choose a new reusable Editor authoring source. Runtime profiles are updated only on Rebuild.", folder);
        if (!string.IsNullOrEmpty(path))
          Execute(() => { slot.objectReferenceValue = MenuStyleAuthoring.CreatePresentationThemeSetAsset(recipe.PresentationThemeType, path); changed = true; });
      }
      var source = slot.objectReferenceValue as MenuPresentationThemeSet;
      using (new EditorGUI.DisabledScope(source == null))
        if (GUILayout.Button(expandedSource == recipe.Id ? "Hide" : "Details", GUILayout.Width(58)))
          expandedSource = expandedSource == recipe.Id ? null : recipe.Id;
      EditorGUILayout.EndHorizontal();
      if (source == null)
        EditorGUILayout.HelpBox("Assign or create the correctly typed theme for this presentation. Existing inline settings remain active until then.", MessageType.None);
      if (source != null && expandedSource == recipe.Id)
      {
        using (new EditorGUI.IndentLevelScope())
        {
          UnityEditor.Editor.CreateCachedEditor(source, typeof(MenuPresentationThemeSetEditor), ref presentationInspector);
          EditorGUI.BeginChangeCheck(); presentationInspector.OnInspectorGUI(); changed |= EditorGUI.EndChangeCheck();
        }
      }
      return changed;
    }

    private static void DrawFallbackSkin(SerializedProperty property)
    {
      if (property != null)
        EditorGUILayout.PropertyField(property, new GUIContent("Fallback skin", "Used only after the sparse style and shared MTK theme override layers."));
    }
    private static void Draw(SerializedProperty parent, params string[] fields)
    {
      foreach (string field in fields)
      {
        var property = parent.FindPropertyRelative(field);
        if (property != null) EditorGUILayout.PropertyField(property, true);
      }
    }
    private static void Draw(SerializedObject target, params string[] fields)
    {
      foreach (string field in fields)
      {
        var property = target.FindProperty(field);
        if (property != null) EditorGUILayout.PropertyField(property, true);
      }
    }

    private void DrawAddButtons(MenuStyleConfig config)
    {
      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("+ ListContextMenu")) Add(config, new ListMenuStyleRecipe { AssetName = "ListContextMenu" });
      if (GUILayout.Button("+ WheelContextMenu")) Add(config, new SingleRingMenuStyleRecipe { AssetName = "WheelContextMenu" });
      if (GUILayout.Button("+ NestedWheelContextMenu")) Add(config, new ConcentricMenuStyleRecipe { AssetName = "NestedWheelContextMenu" });
      EditorGUILayout.EndHorizontal();
      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("+ PieContextMenu")) Add(config, new ReplacementMenuStyleRecipe { AssetName = "PieContextMenu" });
      if (GUILayout.Button("+ Custom")) Add(config, new CustomMenuStyleRecipe { AssetName = "CustomMenu" });
      EditorGUILayout.EndHorizontal();
    }

    private void Add(MenuStyleConfig config, MenuStyleRecipe recipe)
    {
      Undo.RecordObject(config, "Add menu style");
      var existing = config.Styles ?? Array.Empty<MenuStyleRecipe>();
      string name = recipe.AssetName;
      int suffix = 2;
      while (existing.Any(value => value != null && value.AssetName == recipe.AssetName)) recipe.AssetName = name + suffix++;
      config.Styles = existing.Concat(new[] { recipe }).ToArray();
      EditorUtility.SetDirty(config);
      serializedObject.Update();
      plan = null;
    }
    private void Remove(MenuStyleConfig config, int index)
    {
      serializedObject.ApplyModifiedProperties();
      Undo.RecordObject(config, "Remove menu style recipe");
      config.Styles = config.Styles.Where((value, at) => at != index).ToArray();
      EditorUtility.SetDirty(config);
      plan = null;
    }
    private void Refresh(MenuStyleConfig config, string id)
    {
      serializedObject.ApplyModifiedProperties();
      plan = MenuStyleAuthoring.Preview(config, id);
      message = null;
    }
    private void Rebuild(MenuStyleConfig config, string id)
    {
      serializedObject.ApplyModifiedProperties();
      // Reuse a displayed plan so external changes cannot be silently accepted by the button.
      if (plan == null || plan.StyleId != id) { Refresh(config, id); message = "Review the proposed paths, then press Rebuild again."; return; }
      var output = MenuStyleAuthoring.Rebuild(plan);
      plan = MenuStyleAuthoring.Preview(config, id);
      serializedObject.Update();
      message = "Rebuilt " + output.Length + " runtime profile(s). Existing profile and prefab identities were retained.";
      if (output.Length > 0) EditorGUIUtility.PingObject(output[0]);
    }
    private void DrawPlan()
    {
      if (plan == null) return;
      EditorGUILayout.LabelField("Proposed asset changes", EditorStyles.boldLabel);
      foreach (var entry in plan.Entries)
      {
        EditorGUILayout.LabelField(entry.CreatesProfile ? "Create profile" : "Update profile", entry.OutputPath);
        if (!string.IsNullOrEmpty(entry.PresenterPath))
          EditorGUILayout.LabelField("Native presenter", entry.PresenterPath);
      }
      foreach (string diagnostic in plan.Diagnostics) EditorGUILayout.HelpBox(diagnostic, MessageType.Error);
      if (plan.CanBuild) EditorGUILayout.HelpBox("Validated. Rebuild applies only these profiles and any missing owned native presenter outputs. Other prefabs, scenes and dirty assets are preserved.", MessageType.Info);
    }
    private void Execute(Action action)
    {
      try { failed = false; action(); }
      catch (Exception error) { failed = true; message = error.Message; }
    }
  }
}
