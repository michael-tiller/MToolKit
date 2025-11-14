using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Message
{
  /// <summary>
  ///   Extracts a field value from the current message and stores it in graph state.
  ///   Uses reflection to access message fields at runtime.
  ///   Example: Extract EnemyDefeatedMessage.experience and store as "earned_xp"
  /// </summary>
  [CreateNodeMenu("Message/Get Field")]
  [NodeTint("#6B8FA8")]
  [NodeWidth(400)]
  public sealed class MessageFieldGetNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    [BoxGroup("Field")]
    [Required]
    [Tooltip("Name of the field/property to extract from the message (case-sensitive)")]
    public string FieldName = "enemyType";

    [BoxGroup("Storage")]
    [Required]
    [Tooltip("State key to store the extracted value (can be used by other nodes)")]
    public string StateKey = "temp_value";

    [BoxGroup("Options")]
    [Tooltip("If true, logs the extracted value to console for debugging")]
    public bool DebugLog = false;

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}

