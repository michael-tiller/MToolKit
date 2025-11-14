using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.State
{
  /// <summary>
  ///   Reads a state value and stores it in another state key (for use in comparisons or other nodes).
  ///   Useful for copying state values or making them available for later checks.
  ///   Example: Read "enemies_defeated" and store as "temp_count" for later comparison
  /// </summary>
  [CreateNodeMenu("State/Get State")]
  [NodeTint("#6B8B5C")]
  [NodeWidth(400)]
  public sealed class GenericStateGetNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    [BoxGroup("Source")]
    [Required]
    [Tooltip("State key to read from (e.g., 'enemies_defeated', 'player_has_key')")]
    public string SourceStateKey = "source_key";

    [BoxGroup("Destination")]
    [Required]
    [Tooltip("State key to store the value in (can be used by other nodes)")]
    public string DestinationStateKey = "temp_value";

    [BoxGroup("Options")]
    [Tooltip("Default value to use if source key doesn't exist")]
    public string DefaultValue = "0";

    [BoxGroup("Options")]
    [Tooltip("If true, logs the retrieved value for debugging")]
    public bool DebugLog = false;

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}

