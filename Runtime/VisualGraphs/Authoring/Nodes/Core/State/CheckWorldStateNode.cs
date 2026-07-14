using MToolKit.Runtime.VisualGraphs.Variables;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.State
{
  /// <summary>
  ///   Resolves a scoped or local key through the 9.0.2 ScopedKeyResolver and compares it against a typed
  ///   literal, branching Matches/DoesntMatch. Same six comparators as GenericStateCheckNode.
  /// </summary>
  [CreateNodeMenu("Core/State/Check World State")]
  [NodeTint("#6B5C8B")]
  [NodeWidth(400)]
  public sealed class CheckWorldStateNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    [Tooltip("Executes if the resolved value matches ExpectedValue")]
    public NodeConnection Matches;

    [Output(connectionType = ConnectionType.Multiple)]
    [Tooltip("Executes if the resolved value doesn't match, or a runtime-type mismatch occurred")]
    public NodeConnection DoesntMatch;

    [BoxGroup("Source")]
    [Required]
    [Tooltip("Scoped (world./player./quest:<id>.) or local key to resolve")]
    public string Key = "world.my_key";

    [BoxGroup("Comparison")]
    [ValueDropdown(nameof(GetComparisonOperators))]
    [Tooltip("Comparison operator (Equals/NotEquals valid for all types; ordering valid only for Int/Float)")]
    public string ComparisonOperator = "Equals";

    [BoxGroup("Comparison")]
    [Tooltip("Typed literal to compare the resolved value against")]
    public GraphVariableDeclaration ExpectedValue = new();

    public override object GetValue(NodePort port)
    {
      return null;
    }

    private static string[] GetComparisonOperators()
    {
      return new[] { "Equals", "NotEquals", "GreaterThan", "LessThan", "GreaterThanOrEqual", "LessThanOrEqual" };
    }
  }
}
