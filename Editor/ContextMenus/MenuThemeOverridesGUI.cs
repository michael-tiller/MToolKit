using System;
using System.Collections.Generic;
using System.IO;
using MToolKit.ContextMenus.Configuration;
using UnityEditor;
using UnityEngine;
using ThemeSwatch = MToolKit.Theme.Swatch.Swatch;
using ThemeTypeset = MToolKit.Theme.Typeset.Typeset;

namespace MToolKit.Editor.ContextMenus
{
  /// <summary>Compact typed state slots; referenced asset details are drawn only on explicit expansion.</summary>
  internal sealed class MenuThemeOverridesGUI : IDisposable
  {
    private static readonly MenuThemeState[] States = (MenuThemeState[])Enum.GetValues(typeof(MenuThemeState));
    private static readonly MenuVisualRole[] Roles = (MenuVisualRole[])Enum.GetValues(typeof(MenuVisualRole));
    private readonly Dictionary<string, bool> foldouts = new Dictionary<string, bool>();
    private readonly Dictionary<string, string> errors = new Dictionary<string, string>();
    private readonly Dictionary<string, MenuStyleSetConversionPlan> conversions = new Dictionary<string, MenuStyleSetConversionPlan>();
    private UnityEditor.Editor details;
    private string expanded;

    internal bool Draw(SerializedProperty reference, string key, string label, string assetName, string outputFolder)
    {
      if (reference == null) return false;
      EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
      EditorGUILayout.BeginHorizontal();
      EditorGUI.BeginChangeCheck();
      EditorGUILayout.PropertyField(reference, new GUIContent("State style layer"));
      bool changed = EditorGUI.EndChangeCheck();
      if (GUILayout.Button("New layer", GUILayout.Width(80)))
      {
        string path = EditorUtility.SaveFilePanelInProject("Create runtime menu state layer", assetName,
          "asset", "Choose a new runtime MenuSkin layer outside Editor folders.", RuntimeFolder(outputFolder));
        if (!string.IsNullOrEmpty(path))
          try { reference.objectReferenceValue = MenuStyleAuthoring.CreateThemeOverrideAsset(path); changed = true; errors.Remove(key); }
          catch (Exception error) { errors[key] = error.Message; }
      }
      EditorGUILayout.EndHorizontal();
      var layer = reference.objectReferenceValue as MenuSkin;
      if (layer == null)
      {
        EditorGUILayout.HelpBox("Inherits the next layer. Assign or create a layer to configure reusable state style assets.", MessageType.None);
        DrawError(key); return changed;
      }
      EditorGUILayout.HelpBox("One reusable menu style asset per state. Empty slots inherit. Open Details only when editing that asset.", MessageType.None);
      using (var data = new SerializedObject(layer))
      {
        data.Update();
        var slots = Property(data, nameof(MenuSkin.StateStyles));
        foreach (var state in States)
        {
          int index = FindSlot(slots, state);
          var previous = index >= 0 ? Field(slots.GetArrayElementAtIndex(index), nameof(MenuSkinStyle.Style)).objectReferenceValue as MenuStyleSet : null;
          string slotKey = key + ":slot:" + state;
          EditorGUILayout.BeginHorizontal();
          EditorGUI.BeginChangeCheck();
          var next = EditorGUILayout.ObjectField(StateLabel(state), previous, typeof(MenuStyleSet), false) as MenuStyleSet;
          if (EditorGUI.EndChangeCheck()) { SetSlot(slots, state, next); changed = true; }
          if (GUILayout.Button("New", GUILayout.Width(42)))
          {
            string layerFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(layer))?.Replace('\\', '/');
            string path = EditorUtility.SaveFilePanelInProject("Create reusable menu state style", assetName + "_" + state,
              "asset", "The style contains MTK token references and can be reused in other state slots.", RuntimeFolder(layerFolder));
            if (!string.IsNullOrEmpty(path))
              try { next = MenuStyleAuthoring.CreateMenuStyleSetAsset(path); SetSlot(slots, state, next); changed = true; errors.Remove(key); }
              catch (Exception error) { errors[key] = error.Message; }
          }
          using (new EditorGUI.DisabledScope(next == null))
            if (GUILayout.Button(expanded == slotKey ? "Hide" : "Details", GUILayout.Width(58)))
              expanded = expanded == slotKey ? null : slotKey;
          EditorGUILayout.EndHorizontal();
          if (expanded == slotKey && next != null)
          {
            using (new EditorGUI.IndentLevelScope())
            {
              EditorGUILayout.HelpBox("Editing this shared asset changes every slot that references it. Values apply on the next bind, refresh or theme update.", MessageType.None);
              UnityEditor.Editor.CreateCachedEditor(next, typeof(MenuStyleSetEditor), ref details);
              EditorGUI.BeginChangeCheck(); details.OnInspectorGUI(); changed |= EditorGUI.EndChangeCheck();
            }
          }
        }
        changed |= data.ApplyModifiedProperties();
      }
      int count = LegacyCount(layer);
      if (Foldout(key + ":legacy", "Advanced legacy overrides (" + count + ")", false))
      {
        using (new EditorGUI.IndentLevelScope())
        {
          changed |= DrawLegacy(layer, key);
          changed |= DrawConversion(layer, key);
        }
      }
      DrawError(key);
      return changed;
    }

    private bool DrawLegacy(MenuSkin layer, string key)
    {
      EditorGUILayout.HelpBox("These existing direct values win over the style in the same exact state. Convert representable tokens to state assets, or retain deliberate overrides here. Raw local colors remain advanced.", MessageType.None);
      using (var data = new SerializedObject(layer))
      {
        data.Update();
        var direct = Property(data, nameof(MenuSkin.OverrideTheme));
        EditorGUILayout.PropertyField(direct, new GUIContent("Lock entire layer to authored tokens"));
        EditorGUILayout.PropertyField(Property(data, nameof(MenuSkin.Spacing)), new GUIContent("Legacy default Spacing"));
        var colors = Property(data, nameof(MenuSkin.Colors));
        var typography = Property(data, nameof(MenuSkin.Typography));
        bool changed = false;
        foreach (var state in States)
        {
          string stateKey = key + ":legacy:" + state;
          if (!Foldout(stateKey, StateLabel(state), false)) continue;
          using (new EditorGUI.IndentLevelScope())
          {
            changed |= DrawColors(colors, state, stateKey);
            changed |= DrawTypography(data, typography, state, false);
            changed |= DrawTypography(data, typography, state, true);
          }
        }
        return data.ApplyModifiedProperties() || changed;
      }
    }

    private bool DrawConversion(MenuSkin layer, string key)
    {
      bool changed = false;
      if (GUILayout.Button("Preview legacy conversion"))
      {
        string current = Path.GetDirectoryName(AssetDatabase.GetAssetPath(layer))?.Replace('\\', '/');
        string start = Path.Combine(Directory.GetParent(Application.dataPath).FullName, RuntimeFolder(current));
        string folder = EditorUtility.OpenFolderPanel("Choose an existing runtime folder for new state styles", start, "");
        if (!string.IsNullOrEmpty(folder))
        {
          string project = FileUtil.GetProjectRelativePath(folder);
          conversions[key] = MenuStyleAuthoring.PreviewStyleSetConversion(layer, project);
        }
      }
      if (!conversions.TryGetValue(key, out var plan) || plan.Source != layer) return false;
      foreach (var entry in plan.Entries) EditorGUILayout.LabelField(entry.State.ToString(), entry.OutputPath);
      foreach (var diagnostic in plan.Diagnostics) EditorGUILayout.HelpBox(diagnostic, MessageType.Error);
      if (plan.Entries.Count == 0 && plan.CanConvert) EditorGUILayout.HelpBox("No convertible legacy tokens in empty state slots.", MessageType.None);
      using (new EditorGUI.DisabledScope(!plan.CanConvert || plan.Entries.Count == 0))
        if (GUILayout.Button("Convert previewed tokens"))
          try { MenuStyleAuthoring.ConvertStyleSets(plan); conversions.Remove(key); errors.Remove(key); changed = true; }
          catch (Exception error) { errors[key] = error.Message; }
      return changed;
    }

    private static int FindSlot(SerializedProperty slots, MenuThemeState state)
    {
      for (int i = slots.arraySize - 1; i >= 0; i--)
        if (Field(slots.GetArrayElementAtIndex(i), nameof(MenuSkinStyle.State)).intValue == (int)state) return i;
      return -1;
    }
    private static void SetSlot(SerializedProperty slots, MenuThemeState state, MenuStyleSet style)
    {
      for (int i = slots.arraySize - 1; i >= 0; i--)
        if (Field(slots.GetArrayElementAtIndex(i), nameof(MenuSkinStyle.State)).intValue == (int)state) slots.DeleteArrayElementAtIndex(i);
      if (style == null) return;
      int index = slots.arraySize++;
      var entry = slots.GetArrayElementAtIndex(index);
      Field(entry, nameof(MenuSkinStyle.State)).intValue = (int)state;
      Field(entry, nameof(MenuSkinStyle.Style)).objectReferenceValue = style;
    }
    private static int LegacyCount(MenuSkin layer)
    {
      int count = layer.OverrideTheme ? 1 : 0;
      if (layer.Colors != null) foreach (var entry in layer.Colors) if (entry != null && (entry.OverrideColor || entry.Swatch != null)) count++;
      if (layer.Typography != null) foreach (var entry in layer.Typography) if (entry?.Typeset != null) count++;
      if (layer.LabelTypeset != null) count++; if (layer.DescriptionTypeset != null) count++; if (layer.Spacing != null) count++;
      return count;
    }
    private static string RuntimeFolder(string path)
      => !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path) && !IsEditorFolder(path) ? path : "Assets";
    private void DrawError(string key)
    { if (errors.TryGetValue(key, out var error)) EditorGUILayout.HelpBox(error, MessageType.Error); }
    public void Dispose()
    { if (details != null) UnityEngine.Object.DestroyImmediate(details); details = null; }

    private bool DrawColors(SerializedProperty colors, MenuThemeState state, string key)
    {
      bool changed = false;
      foreach (var role in Roles)
      {
        int index = FindColor(colors, state, role);
        bool clear;
        EditorGUILayout.BeginHorizontal();
        if (index >= 0)
        {
          var entry = colors.GetArrayElementAtIndex(index);
          var token = Field(entry, nameof(MenuSkinColor.Swatch));
          var previous = token.objectReferenceValue;
          EditorGUI.BeginChangeCheck();
          EditorGUILayout.PropertyField(token, new GUIContent(RoleCaption(role)));
          changed |= EditorGUI.EndChangeCheck();
          if (token.objectReferenceValue != previous && token.objectReferenceValue != null)
            Field(entry, nameof(MenuSkinColor.OverrideColor)).boolValue = false;
        }
        else
        {
          var token = EditorGUILayout.ObjectField(RoleCaption(role), null, typeof(ThemeSwatch), false) as ThemeSwatch;
          if (token != null)
          {
            index = AddColor(colors, state, role);
            Field(colors.GetArrayElementAtIndex(index), nameof(MenuSkinColor.Swatch)).objectReferenceValue = token;
          }
        }
        using (new EditorGUI.DisabledScope(index < 0)) clear = GUILayout.Button("Clear", GUILayout.Width(42));
        EditorGUILayout.EndHorizontal();
        if (clear) { RemoveColors(colors, state, role); continue; }
        if (index < 0) continue;
        var colorEntry = colors.GetArrayElementAtIndex(index);
        var local = Field(colorEntry, nameof(MenuSkinColor.OverrideColor));
        string caption = local.boolValue ? "Local color override (active)" : "Local color override";
        if (!Foldout(key + ":" + role + ":color", caption, local.boolValue)) continue;
        using (new EditorGUI.IndentLevelScope())
        {
          EditorGUILayout.PropertyField(local, new GUIContent("Use local color"));
          if (local.boolValue)
          {
            EditorGUILayout.PropertyField(Field(colorEntry, nameof(MenuSkinColor.Color)), GUIContent.none);
            EditorGUILayout.HelpBox("This explicit color wins over the Swatch. Assigning a different Swatch clears this local color override.", MessageType.None);
          }
        }
      }
      return changed;
    }

    private static bool DrawTypography(SerializedObject data, SerializedProperty typography, MenuThemeState state, bool description)
    {
      int index = FindTypography(typography, state, description);
      var fallback = state == MenuThemeState.Any
        ? Property(data, description ? nameof(MenuSkin.DescriptionTypeset) : nameof(MenuSkin.LabelTypeset)) : null;
      var token = index >= 0 ? Field(typography.GetArrayElementAtIndex(index), nameof(MenuSkinTypography.Typeset)) : fallback;
      if (token != null && token.objectReferenceValue == null && fallback != null) token = fallback;
      var previous = token != null ? token.objectReferenceValue as ThemeTypeset : null;
      string label = description ? "Description Typeset" : "Label Typeset";
      EditorGUILayout.BeginHorizontal();
      EditorGUI.BeginChangeCheck();
      ThemeTypeset next;
      if (token != null)
      {
        EditorGUILayout.PropertyField(token, new GUIContent(label));
        next = token.objectReferenceValue as ThemeTypeset;
      }
      else next = EditorGUILayout.ObjectField(label, null, typeof(ThemeTypeset), false) as ThemeTypeset;
      bool tokenChanged = EditorGUI.EndChangeCheck();
      bool clear;
      using (new EditorGUI.DisabledScope(previous == null && next == null)) clear = GUILayout.Button("Clear", GUILayout.Width(42));
      EditorGUILayout.EndHorizontal();
      if (!clear && previous == next) return tokenChanged;
      RemoveTypography(typography, state, description);
      if (clear) next = null;
      if (state == MenuThemeState.Any) fallback.objectReferenceValue = next;
      else if (next != null)
      {
        int at = typography.arraySize++;
        var entry = typography.GetArrayElementAtIndex(at);
        Field(entry, nameof(MenuSkinTypography.State)).intValue = (int)state;
        Field(entry, nameof(MenuSkinTypography.Description)).boolValue = description;
        Field(entry, nameof(MenuSkinTypography.Typeset)).objectReferenceValue = next;
      }
      return true;
    }

    private bool Foldout(string key, string label, bool initial)
    {
      bool current = foldouts.TryGetValue(key, out bool value) ? value : initial;
      bool next = EditorGUILayout.Foldout(current, label, true);
      foldouts[key] = next;
      return next;
    }

    private static int FindColor(SerializedProperty colors, MenuThemeState state, MenuVisualRole role)
    {
      for (int i = colors.arraySize - 1; i >= 0; i--)
      {
        var entry = colors.GetArrayElementAtIndex(i);
        if (Field(entry, nameof(MenuSkinColor.State)).intValue == (int)state
          && Field(entry, nameof(MenuSkinColor.Role)).intValue == (int)role) return i;
      }
      return -1;
    }

    private static int AddColor(SerializedProperty colors, MenuThemeState state, MenuVisualRole role)
    {
      int index = colors.arraySize++;
      var entry = colors.GetArrayElementAtIndex(index);
      Field(entry, nameof(MenuSkinColor.State)).intValue = (int)state;
      Field(entry, nameof(MenuSkinColor.Role)).intValue = (int)role;
      Field(entry, nameof(MenuSkinColor.Swatch)).objectReferenceValue = null;
      Field(entry, nameof(MenuSkinColor.OverrideColor)).boolValue = false;
      Field(entry, nameof(MenuSkinColor.Color)).colorValue = Color.white;
      return index;
    }

    private static void RemoveColors(SerializedProperty colors, MenuThemeState state, MenuVisualRole role)
    {
      for (int i = colors.arraySize - 1; i >= 0; i--)
      {
        var entry = colors.GetArrayElementAtIndex(i);
        if (Field(entry, nameof(MenuSkinColor.State)).intValue == (int)state
          && Field(entry, nameof(MenuSkinColor.Role)).intValue == (int)role) colors.DeleteArrayElementAtIndex(i);
      }
    }

    private static int FindTypography(SerializedProperty typography, MenuThemeState state, bool description)
    {
      for (int i = typography.arraySize - 1; i >= 0; i--)
      {
        var entry = typography.GetArrayElementAtIndex(i);
        if (Field(entry, nameof(MenuSkinTypography.State)).intValue == (int)state
          && Field(entry, nameof(MenuSkinTypography.Description)).boolValue == description) return i;
      }
      return -1;
    }

    private static void RemoveTypography(SerializedProperty typography, MenuThemeState state, bool description)
    {
      for (int i = typography.arraySize - 1; i >= 0; i--)
      {
        var entry = typography.GetArrayElementAtIndex(i);
        if (Field(entry, nameof(MenuSkinTypography.State)).intValue == (int)state
          && Field(entry, nameof(MenuSkinTypography.Description)).boolValue == description) typography.DeleteArrayElementAtIndex(i);
      }
    }

    private static SerializedProperty Property(SerializedObject data, string name)
      => data.FindProperty("<" + name + ">k__BackingField");
    private static SerializedProperty Field(SerializedProperty data, string name)
      => data.FindPropertyRelative("<" + name + ">k__BackingField");
    private static bool IsEditorFolder(string path)
      => Array.Exists(path.Split('/'), part => string.Equals(part, "Editor", StringComparison.OrdinalIgnoreCase));
    private static string RoleCaption(MenuVisualRole role)
    {
      switch (role)
      {
        case MenuVisualRole.Normal: return "Item fill: normal / checked";
        case MenuVisualRole.Hovered: return "Item fill: hovered";
        case MenuVisualRole.Selected: return "Selected fill / indicator";
        case MenuVisualRole.Disabled: return "Item fill: disabled";
        case MenuVisualRole.Center: return "Center fill";
        case MenuVisualRole.Auxiliary: return "Auxiliary / footer fill";
        case MenuVisualRole.Checked: return "Check marker";
        case MenuVisualRole.Background: return "Custom background";
        case MenuVisualRole.Backdrop: return "Outside backdrop";
        default: return ObjectNames.NicifyVariableName(role.ToString());
      }
    }
    private static string StateLabel(MenuThemeState state)
      => state == MenuThemeState.Any ? "Default (Any state)" : state == MenuThemeState.Normal ? "Enabled (Normal)" : state.ToString();
  }
}
