using System.Collections.Generic;
using Sirenix.OdinInspector;
using MToolKit.Runtime.VisualGraphs.Variables;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Transform
{

  /// <summary>Maps a numeric state value between ranges, with optional clamping and a zero-width fallback.</summary>
  [CreateNodeMenu("Core/Transform/MapValue")]
  [NodeWidth(300)]
  public sealed class MapValueNode : VisualGraphNodeBase
  {
    /// <summary>Incoming execution connection.</summary>
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;
    /// <summary>Outgoing execution connections continued after the operation.</summary>
    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    /// <summary>Scoped key holding the numeric value to map.</summary>
    [BoxGroup("Operands")] [Required] public string ValueKey = "value";
    /// <summary>Scoped numeric key for the lower endpoint of the input range.</summary>
    [BoxGroup("Operands")] [Required] public string InputMinKey = "inputMin";
    /// <summary>Scoped numeric key for the upper endpoint of the input range.</summary>
    [BoxGroup("Operands")] [Required] public string InputMaxKey = "inputMax";
    /// <summary>Scoped numeric key for the lower endpoint of the output range.</summary>
    [BoxGroup("Operands")] [Required] public string OutputMinKey = "outputMin";
    /// <summary>Scoped numeric key for the upper endpoint of the output range.</summary>
    [BoxGroup("Operands")] [Required] public string OutputMaxKey = "outputMax";
    /// <summary>Numeric operand and output type: Int or Float.</summary>
    [BoxGroup("Operands")] public EGraphVariableType ValueType = EGraphVariableType.Float;
    /// <summary>Whether the interpolation factor is restricted to the output range endpoints.</summary>
    [BoxGroup("Operands")] public bool ClampOutput;
    /// <summary>Numeric result used when the input range has zero width; Int mode truncates toward zero.</summary>
    [BoxGroup("Operands")] public float Fallback;
    /// <summary>Scoped destination key receiving the computed result.</summary>
    [BoxGroup("Result")] [Required] public string ResultKey = "result";

    /// <summary>Returns no data-port value; this node exchanges operands through graph state.</summary>
    /// <param name="port">Authoring port whose value was requested.</param>
    /// <returns>Always null.</returns>
    public override object GetValue(NodePort port) => null;
  }
}
