using System.Collections.Generic;
using Sirenix.OdinInspector;
using MToolKit.Runtime.VisualGraphs.Variables;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Transform
{

  /// <summary>Builds a String from ordered state arguments using invariant composite formatting.</summary>
  [CreateNodeMenu("Core/Transform/FormatString")]
  [NodeWidth(300)]
  public sealed class FormatStringNode : VisualGraphNodeBase
  {
    /// <summary>Incoming execution connection.</summary>
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;
    /// <summary>Outgoing execution connections continued after the operation.</summary>
    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    /// <summary>Invariant composite format whose argument indices refer to Keys in order.</summary>
    [BoxGroup("Operands")] [Tooltip("Invariant composite format, for example: Growth={0:F2}.")] public string Format = "{0}";
    /// <summary>Ordered scoped argument keys referenced by the composite format.</summary>
    [BoxGroup("Operands")] public List<string> Keys = new List<string>();
    /// <summary>Scoped destination key receiving the computed result.</summary>
    [BoxGroup("Result")] [Required] public string ResultKey = "result";

    /// <summary>Returns no data-port value; this node exchanges operands through graph state.</summary>
    /// <param name="port">Authoring port whose value was requested.</param>
    /// <returns>Always null.</returns>
    public override object GetValue(NodePort port) => null;
  }
}
