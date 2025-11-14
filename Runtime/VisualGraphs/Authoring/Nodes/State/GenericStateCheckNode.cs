using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.State
{
  /// <summary>
  ///   Checks if a state key equals an expected value and branches execution.
  ///   Use this to create conditional logic based on stored state.
  ///   Example: Check if "player_has_key" == true, or "enemies_defeated" >= 5
  /// </summary>
  [CreateNodeMenu("State/Check State")]
  [NodeTint("#8B6B5C")]
  [NodeWidth(400)]
  public sealed class GenericStateCheckNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    [Tooltip("Executes if state value matches expected value")]
    public NodeConnection Matches;

    [Output(connectionType = ConnectionType.Multiple)]
    [Tooltip("Executes if state value doesn't match or key doesn't exist")]
    public NodeConnection DoesntMatch;

    [BoxGroup("State")]
    [Required]
    [Tooltip("State key to check (e.g., 'player_has_key', 'enemies_defeated')")]
    public string StateKey = "my_state_key";

    [BoxGroup("Comparison")]
    [Tooltip("Expected value to compare against. Will be converted to state value's type at runtime.")]
    public string ExpectedValue = "true";

    [BoxGroup("Comparison")]
    [Tooltip("Comparison operator to use")]
    [ValueDropdown("GetComparisonOperators")]
    public string ComparisonOperator = "Equals";

    [BoxGroup("Options")]
    [Tooltip("If true, comparison is case-insensitive (for string values)")]
    public bool IgnoreCase = false;

    private static string[] GetComparisonOperators()
    {
      return new[] { "Equals", "NotEquals", "GreaterThan", "LessThan", "GreaterThanOrEqual", "LessThanOrEqual" };
    }

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}

