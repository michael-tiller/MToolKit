using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Logic
{
  /// <summary>
  ///   Folds a list of bool state keys with AND (left-to-right; empty list is the identity, true) and
  ///   writes the result to a state key.
  /// </summary>
  [CreateNodeMenu("Core/Logic/And")]
  [NodeTint("#5C5C8B")]
  [NodeWidth(300)]
  public sealed class AndNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    [BoxGroup("Operands")]
    [Tooltip("Bool state keys folded with AND, left-to-right. Missing/unresolvable keys are skipped.")]
    public List<string> Keys = new();

    [BoxGroup("Result")]
    [Required]
    [Tooltip("State key the fold result is written to")]
    public string ResultKey = "result";

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}
