using UnityEditor;
using UnityEngine;

namespace MToolKit.Editor.ContextMenus
{
  [CustomEditor(typeof(MenuPresentationThemeSet), true)]
  public sealed class MenuPresentationThemeSetEditor : UnityEditor.Editor
  {
    private readonly MenuThemeOverridesGUI states = new MenuThemeOverridesGUI();
    private bool composition;
    private void OnDisable() => states.Dispose();
    public override void OnInspectorGUI()
    {
      serializedObject.Update();
      EditorGUILayout.HelpBox("This source owns its family's visual layout and composition when assigned. Preview/Rebuild copies it into runtime profiles; input and gameplay remain outside the theme.", MessageType.None);
      bool pie = target is PieMenuThemeSet;
      var layout = serializedObject.FindProperty("Layout");
      if (pie)
      {
        EditorGUILayout.LabelField("Button placement", EditorStyles.boldLabel);
        Draw(layout, "InnerRadius", "Orbit radius"); Draw(layout, "RingWidth", "Orbit depth");
        Draw(layout, "StartAngle", "Start angle"); Draw(layout, "Clockwise", "Clockwise");
        Draw(layout, "AngularGap", "Angular gap"); Draw(layout, "DrawSegments", "Draw background ring");
      }
      else EditorGUILayout.PropertyField(layout, new GUIContent("Family layout"), true);
      EditorGUILayout.PropertyField(serializedObject.FindProperty("MaximumVisibleItems"));
      EditorGUILayout.PropertyField(serializedObject.FindProperty("WidgetSize"), new GUIContent(pie ? "Button size" : "Item size"));
      EditorGUILayout.PropertyField(serializedObject.FindProperty("ShowCenter"));
      EditorGUILayout.PropertyField(serializedObject.FindProperty("Backdrop"));
      string path = AssetDatabase.GetAssetPath(target);
      string folder = string.IsNullOrEmpty(path) ? "Assets" : System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
      bool changed = states.Draw(serializedObject.FindProperty("ThemeOverrides"), "source:" + target.GetInstanceID(),
        "Reusable state style sets", target.name + "_States", folder);
      composition = EditorGUILayout.Foldout(composition, "Optional visual prefabs", true);
      if (composition)
        using (new EditorGUI.IndentLevelScope())
        {
          EditorGUILayout.HelpBox("Empty references inherit Common, then retain an existing output reference. Existing custom prefab contents are never rewritten.", MessageType.None);
          foreach (string field in new[] { "PresenterPrefab", "ItemPrefab", "CenterPrefab", "SecondaryPrefab" })
            EditorGUILayout.PropertyField(serializedObject.FindProperty(field));
        }
      if (serializedObject.ApplyModifiedProperties() || changed) GUI.changed = true;
    }
    private static void Draw(SerializedProperty parent, string name, string label)
      => EditorGUILayout.PropertyField(parent.FindPropertyRelative(name), new GUIContent(label));
  }
}
