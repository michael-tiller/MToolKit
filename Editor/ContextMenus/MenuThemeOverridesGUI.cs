using System;
using System.Collections.Generic;
using MToolKit.ContextMenus.Configuration;
using UnityEditor;
using UnityEngine;
using ThemeSwatch = MToolKit.Theme.Swatch.Swatch;
using ThemeTypeset = MToolKit.Theme.Typeset.Typeset;

namespace MToolKit.Editor.ContextMenus
{
  /// <summary>Edits the existing MTK token assets through a sparse MenuSkin layer.</summary>
  internal sealed class MenuThemeOverridesGUI
  {
    private static readonly MenuThemeState[] States = (MenuThemeState[])Enum.GetValues(typeof(MenuThemeState));
    private static readonly MenuVisualRole[] Roles = (MenuVisualRole[])Enum.GetValues(typeof(MenuVisualRole));
    private readonly Dictionary<string, bool> foldouts = new Dictionary<string, bool>();
    private readonly Dictionary<string, string> errors = new Dictionary<string, string>();

    internal bool Draw(SerializedProperty reference, string key, string label, string assetName, string outputFolder)
    {
      if (reference == null) return false;
      EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
      EditorGUILayout.BeginHorizontal();
      EditorGUI.BeginChangeCheck();
      EditorGUILayout.PropertyField(reference, new GUIContent("Override asset"));
      bool changed = EditorGUI.EndChangeCheck();
      bool create = GUILayout.Button("Create overrides", GUILayout.Width(115));
      EditorGUILayout.EndHorizontal();
      if (create)
      {
        string folder = AssetDatabase.IsValidFolder(outputFolder) && !IsEditorFolder(outputFolder) ? outputFolder : "Assets";
        string path = EditorUtility.SaveFilePanelInProject("Create runtime MTK theme overrides", assetName,
          "asset", "Choose a new runtime MenuSkin asset outside Editor folders.", folder);
        if (!string.IsNullOrEmpty(path))
        {
          try
          {
            reference.objectReferenceValue = MenuStyleAuthoring.CreateThemeOverrideAsset(path);
            changed = true;
            errors.Remove(key);
          }
          catch (Exception error) { errors[key] = error.Message; }
        }
      }
      if (errors.TryGetValue(key, out var diagnostic)) EditorGUILayout.HelpBox(diagnostic, MessageType.Error);
      var layer = reference.objectReferenceValue as MenuSkin;
      if (layer == null)
      {
        EditorGUILayout.HelpBox("No local tokens. Inherit from the next theme layer.", MessageType.None);
        return changed;
      }
      EditorGUILayout.HelpBox("Assign MTK Swatch, Typeset and Spacing assets below. Empty tokens inherit. " +
        "Reference changes apply on Rebuild. Direct asset-value edits take effect on the next bind, refresh or MTK theme update.", MessageType.None);
      using (var data = new SerializedObject(layer))
      {
        data.Update();
        var colors = Property(data, nameof(MenuSkin.Colors));
        var typography = Property(data, nameof(MenuSkin.Typography));
        var direct = Property(data, nameof(MenuSkin.OverrideTheme));
        bool resolve = EditorGUILayout.Toggle(new GUIContent("Resolve active MTK theme",
          "Resolve Swatch and Typeset semantic IDs through the active IThemeService. Local color overrides always retain their explicit value."), !direct.boolValue);
        if (direct.boolValue == resolve) direct.boolValue = !resolve;
        EditorGUILayout.PropertyField(Property(data, nameof(MenuSkin.Spacing)), new GUIContent("Spacing (all states)"));
        if (GUILayout.Button("Open MTK Theme tools")) EditorApplication.ExecuteMenuItem("Tools/MToolKit/Theme Configurator");
        EditorGUILayout.HelpBox("Native item and wedge fills use Normal for enabled/checked, Hovered for pointer hover, " +
          "Selected for selection and Disabled for unavailable actions. Center and Auxiliary use their named roles; " +
          "labels, descriptions, icons and check markers keep their roles within each state. Custom presenters can use every role.", MessageType.None);
        foreach (var state in States)
        {
          string stateKey = key + ":" + state;
          if (!Foldout(stateKey, StateLabel(state), state == MenuThemeState.Any)) continue;
          using (new EditorGUI.IndentLevelScope())
          {
            EditorGUILayout.LabelField("Swatches", EditorStyles.miniBoldLabel);
            changed |= DrawColors(colors, state, stateKey);
            EditorGUILayout.LabelField("Typography", EditorStyles.miniBoldLabel);
            changed |= DrawTypography(data, typography, state, false);
            changed |= DrawTypography(data, typography, state, true);
          }
        }
        changed |= data.ApplyModifiedProperties();
      }
      return changed;
    }

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