using System.Collections.Generic;
using Sirenix.OdinInspector;
using MToolKit.Runtime.VisualGraphs.Variables;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Transform
{

  /// <summary>Combines Float state components into a Vector2, Vector3 or Color value.</summary>
  [CreateNodeMenu("Core/Transform/CombineValues")]
  [NodeWidth(300)]
  public sealed class CombineValuesNode : VisualGraphNodeBase
  {
    /// <summary>Incoming execution connection.</summary>
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;
    /// <summary>Outgoing execution connections continued after the operation.</summary>
    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    /// <summary>Ordered scoped Float keys for x/y/z or r/g/b/a; omitted alpha defaults to one.</summary>
    [BoxGroup("Operands")] [Tooltip("Float component keys in x/y/z or r/g/b/a order.")] public List<string> Keys = new List<string> { "x", "y", "z" };
    /// <summary>Composite output type: Vector2, Vector3 or Color.</summary>
    [BoxGroup("Operands")] public EGraphVariableType ValueType = EGraphVariableType.Vector3;
    /// <summary>Scoped destination key receiving the computed result.</summary>
    [BoxGroup("Result")] [Required] public string ResultKey = "result";

    /// <summary>Returns no data-port value; this node exchanges operands through graph state.</summary>
    /// <param name="port">Authoring port whose value was requested.</param>
    /// <returns>Always null.</returns>
    public override object GetValue(NodePort port) => null;
  }
}
