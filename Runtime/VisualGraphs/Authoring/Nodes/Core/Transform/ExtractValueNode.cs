using System.Collections.Generic;
using Sirenix.OdinInspector;
using MToolKit.Runtime.VisualGraphs.Variables;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Transform
{
  /// <summary>Vector components and equivalent color-channel aliases.</summary>
  public enum EGraphValueComponent
  {
    /// <summary>First vector component.</summary>
    X = 0,
    /// <summary>Second vector component.</summary>
    Y = 1,
    /// <summary>Third vector component.</summary>
    Z = 2,
    /// <summary>Fourth vector component, valid for a four-channel color.</summary>
    W = 3,
    /// <summary>Red channel, equivalent to X.</summary>
    R = 0,
    /// <summary>Green channel, equivalent to Y.</summary>
    G = 1,
    /// <summary>Blue channel, equivalent to Z.</summary>
    B = 2,
    /// <summary>Alpha channel, equivalent to W.</summary>
    A = 3
  }

  /// <summary>Reads a Vector2, Vector3 or Color component and writes its Float value.</summary>
  [CreateNodeMenu("Core/Transform/ExtractValue")]
  [NodeWidth(300)]
  public sealed class ExtractValueNode : VisualGraphNodeBase
  {
    /// <summary>Incoming execution connection.</summary>
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;
    /// <summary>Outgoing execution connections continued after the operation.</summary>
    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    /// <summary>Scoped key holding the vector or color to inspect.</summary>
    [BoxGroup("Operands")] [Required] public string SourceKey = "value";
    /// <summary>Composite source type: Vector2, Vector3 or Color.</summary>
    [BoxGroup("Operands")] public EGraphVariableType ValueType = EGraphVariableType.Vector3;
    /// <summary>Vector component or equivalent color channel to extract.</summary>
    [BoxGroup("Operands")] public EGraphValueComponent Component = EGraphValueComponent.X;
    /// <summary>Scoped destination key receiving the computed result.</summary>
    [BoxGroup("Result")] [Required] public string ResultKey = "result";

    /// <summary>Returns no data-port value; this node exchanges operands through graph state.</summary>
    /// <param name="port">Authoring port whose value was requested.</param>
    /// <returns>Always null.</returns>
    public override object GetValue(NodePort port) => null;
  }
}
