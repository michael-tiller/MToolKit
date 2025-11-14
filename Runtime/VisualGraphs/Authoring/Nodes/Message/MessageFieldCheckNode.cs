using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Message
{
  /// <summary>
  ///   Checks if a field in the current message equals an expected value.
  ///   Uses reflection to access message fields at runtime.
  ///   Example: Check if EnemyDefeatedMessage.enemyType == "Turnip"
  /// </summary>
  [CreateNodeMenu("Message/Check Field")]
  [NodeTint("#B8905C")]
  [NodeWidth(400)]
  public sealed class MessageFieldCheckNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    [Tooltip("Executes if field value matches expected value")]
    public NodeConnection Matches;

    [Output(connectionType = ConnectionType.Multiple)]
    [Tooltip("Executes if field value doesn't match")]
    public NodeConnection DoesntMatch;

    [BoxGroup("Field")]
    [Required]
    [Tooltip("Name of the field/property to check in the message (case-sensitive)")]
    public string FieldName = "enemyType";

    [BoxGroup("Field")]
    [Tooltip("Expected value to compare against (converted to field's type at runtime)")]
    public string ExpectedValue = "Turnip";

    [BoxGroup("Options")]
    [Tooltip("If true, comparison is case-insensitive (for string fields)")]
    public bool IgnoreCase = false;

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}

