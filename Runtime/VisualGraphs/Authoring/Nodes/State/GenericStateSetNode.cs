using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.State
{
  /// <summary>
  ///   Sets an arbitrary state key to a value.
  ///   Use this to store game state that can be checked later by GenericStateCheckNode.
  ///   Example: Set "player_has_key" = true, or "enemies_defeated" = 5
  /// </summary>
  [CreateNodeMenu("State/Set State")]
  [NodeTint("#5C8B6B")]
  [NodeWidth(400)]
  public sealed class GenericStateSetNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    [BoxGroup("State")]
    [Required]
    [Tooltip("State key to set (e.g., 'player_has_key', 'enemies_defeated')")]
    public string StateKey = "my_state_key";

    [BoxGroup("State")]
    [Tooltip("Value to set. Can be string, int, float, or bool. Will be converted at runtime.")]
    public string Value = "true";

    [BoxGroup("State")]
    [Tooltip("Type of value to store. Determines how Value string is parsed.")]
    [ValueDropdown("GetValueTypes")]
    public string ValueType = "bool";

    [BoxGroup("Options")]
    [Tooltip("If true, logs the state change for debugging")]
    public bool DebugLog = false;

    private static string[] GetValueTypes()
    {
      return new[] { "bool", "int", "float", "string" };
    }

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}

