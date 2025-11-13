// TweenAnimatedButtonEditor.cs

#if UNITY_EDITOR
using MToolKit.Runtime.Components;
using UnityEditor;
using UnityEditor.UI;

namespace MToolKit.Editor
{
  [CustomEditor(typeof(TweenAnimatedButton))]
  public class TweenAnimatedButtonEditor : ButtonEditor
  {
    private SerializedProperty border;
    private SerializedProperty disabledBorderColor;
    private SerializedProperty disabledButtonColor;
    private SerializedProperty disabledLabelColor;
    private SerializedProperty highlightedBorderColor;
    private SerializedProperty highlightedButtonColor;
    private SerializedProperty highlightedLabelColor;
    private SerializedProperty label;
    private SerializedProperty normalBorderColor;
    private SerializedProperty normalButtonColor;
    private SerializedProperty normalLabelColor;
    private SerializedProperty pressedBorderColor;
    private SerializedProperty pressedButtonColor;
    private SerializedProperty pressedLabelColor;
    private SerializedProperty selectedBorderColor;
    private SerializedProperty selectedButtonColor;
    private SerializedProperty selectedLabelColor;
    private SerializedProperty targetGraphicProperty;
    private SerializedProperty tweenDuration;

    protected override void OnEnable()
    {
      base.OnEnable();
      SerializedObject t = serializedObject;
      normalButtonColor = t.FindProperty("normalButtonColor");
      highlightedButtonColor = t.FindProperty("highlightedButtonColor");
      pressedButtonColor = t.FindProperty("pressedButtonColor");
      selectedButtonColor = t.FindProperty("selectedButtonColor");
      disabledButtonColor = t.FindProperty("disabledButtonColor");
      tweenDuration = t.FindProperty("tweenDuration");
      label = t.FindProperty("label");
      normalLabelColor = t.FindProperty("normalLabelColor");
      highlightedLabelColor = t.FindProperty("highlightedLabelColor");
      pressedLabelColor = t.FindProperty("pressedLabelColor");
      selectedLabelColor = t.FindProperty("selectedLabelColor");
      disabledLabelColor = t.FindProperty("disabledLabelColor");

      border = t.FindProperty("border");
      normalBorderColor = t.FindProperty("normalBorderColor");
      highlightedBorderColor = t.FindProperty("highlightedBorderColor");
      pressedBorderColor = t.FindProperty("pressedBorderColor");
      selectedBorderColor = t.FindProperty("selectedBorderColor");
      disabledBorderColor = t.FindProperty("disabledBorderColor");
      targetGraphicProperty = serializedObject.FindProperty("m_TargetGraphic");
    }

    public override void OnInspectorGUI()
    {
      base.OnInspectorGUI();


      EditorGUILayout.Space();
      //EditorGUILayout.LabelField("Button Settings", EditorStyles.boldLabel);
      EditorGUILayout.PropertyField(targetGraphicProperty);
      EditorGUILayout.PropertyField(normalButtonColor);
      EditorGUILayout.PropertyField(highlightedButtonColor);
      EditorGUILayout.PropertyField(pressedButtonColor);
      EditorGUILayout.PropertyField(selectedButtonColor);
      EditorGUILayout.PropertyField(disabledButtonColor);
      EditorGUILayout.PropertyField(tweenDuration);

      EditorGUILayout.Space();
      //EditorGUILayout.LabelField("Label Settings", EditorStyles.boldLabel);
      EditorGUILayout.PropertyField(label);
      EditorGUILayout.PropertyField(normalLabelColor);
      EditorGUILayout.PropertyField(highlightedLabelColor);
      EditorGUILayout.PropertyField(pressedLabelColor);
      EditorGUILayout.PropertyField(selectedLabelColor);
      EditorGUILayout.PropertyField(disabledLabelColor);

      EditorGUILayout.Space();
      // EditorGUILayout.LabelField("Border Settings", EditorStyles.boldLabel);
      EditorGUILayout.PropertyField(border);
      EditorGUILayout.PropertyField(normalBorderColor);
      EditorGUILayout.PropertyField(highlightedBorderColor);
      EditorGUILayout.PropertyField(pressedBorderColor);
      EditorGUILayout.PropertyField(selectedBorderColor);
      EditorGUILayout.PropertyField(disabledBorderColor);

      bool changed = serializedObject.ApplyModifiedProperties();

      if (changed)
      {
        ((TweenAnimatedButton)target).ForceValidate();
        EditorUtility.SetDirty(target);
      }
    }
  }
}

#endif