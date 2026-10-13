#if UNITY_EDITOR
using MToolKit.Runtime.VisualGraphs.Rules;
using UnityEditor;
using UnityEngine;

namespace MToolKit.Editor.VisualGraphs.Rules
{
  /// <summary>Thin rule inspector that keeps authoring in the existing EventGraphAsset editor.</summary>
  [CustomEditor(typeof(GameRuleDefinition))]
  public sealed class GameRuleDefinitionEditor : UnityEditor.Editor
  {
    /// <summary>Draw rule settings, shared-state guidance and an event-graph selection action.</summary>
    public override void OnInspectorGUI()
    {
      DrawDefaultInspector();
      var rule = (GameRuleDefinition)target;
      EditorGUILayout.HelpBox("Rule variables use the persistent World scope. Register this rule in VisualGraphConfig.GameRules.", MessageType.Info);
      using (new EditorGUI.DisabledScope(rule.Graph == null))
        if (GUILayout.Button("Select Event Graph")) Selection.activeObject = rule.Graph;
    }
  }
}
#endif
