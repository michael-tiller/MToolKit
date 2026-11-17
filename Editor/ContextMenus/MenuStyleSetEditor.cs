using MToolKit.ContextMenus.Configuration;
using MToolKit.Theme;
using UnityEditor;
using UnityEngine;

namespace MToolKit.Editor.ContextMenus
{
  [CustomEditor(typeof(MenuStyleSet))]
  public sealed class MenuStyleSetEditor : UnityEditor.Editor
  {
    private bool channels;
    public override void OnInspectorGUI()
    {
      serializedObject.Update();
      EditorGUILayout.HelpBox("Reusable appearance only. Choose its state in the caller's slot; asset names such as PositiveInactive or NegativeSelected are project conventions.", MessageType.None);
      Draw(nameof(ThemeStyleSet.Background), "Surface");
      Draw(nameof(ThemeStyleSet.Foreground), "Label / foreground");
      Draw(nameof(ThemeStyleSet.Description), "Description");
      Draw(nameof(ThemeStyleSet.Icon), "Icon");
      Draw(nameof(ThemeStyleSet.Border), "Border");
      Draw(nameof(ThemeStyleSet.LabelTypeset), "Label Typeset");
      Draw(nameof(ThemeStyleSet.DescriptionTypeset), "Description Typeset");
      Draw(nameof(ThemeStyleSet.Spacing), "Spacing");
      Draw(nameof(ThemeStyleSet.OverrideTheme), "Use authored token values");
      channels = EditorGUILayout.Foldout(channels, "Menu-specific channels", true);
      if (channels)
        using (new EditorGUI.IndentLevelScope())
        {
          EditorGUILayout.HelpBox("Empty fill channels use Surface; empty inner/outer borders use Border. Center, auxiliary and outside backdrop are separate so a button surface never becomes a full-screen overlay.", MessageType.None);
          Draw(nameof(MenuStyleSet.Normal), "Normal fill"); Draw(nameof(MenuStyleSet.Hovered), "Hovered fill");
          Draw(nameof(MenuStyleSet.Selected), "Selected fill / indicator"); Draw(nameof(MenuStyleSet.Disabled), "Disabled fill");
          Draw(nameof(MenuStyleSet.PanelBackground), "Custom panel background");
          Draw(nameof(MenuStyleSet.CenterBackground), "Center background");
          Draw(nameof(MenuStyleSet.AuxiliaryBackground), "Auxiliary / footer background");
          Draw(nameof(MenuStyleSet.Backdrop), "Outside backdrop");
          Draw(nameof(MenuStyleSet.InnerBorder), "Inner border"); Draw(nameof(MenuStyleSet.OuterBorder), "Outer border");
          Draw(nameof(MenuStyleSet.Separator), "Separator"); Draw(nameof(MenuStyleSet.Checked), "Check marker");
        }
      if (GUILayout.Button("Open MTK Theme tools")) EditorApplication.ExecuteMenuItem("Tools/MToolKit/Theme Configurator");
      if (serializedObject.ApplyModifiedProperties()) GUI.changed = true;
    }
    private void Draw(string field, string label)
      => EditorGUILayout.PropertyField(serializedObject.FindProperty("<" + field + ">k__BackingField"), new GUIContent(label));
  }
}
