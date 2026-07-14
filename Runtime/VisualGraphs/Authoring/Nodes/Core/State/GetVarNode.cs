using MToolKit.Runtime.VisualGraphs.Variables;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.State
{
  /// <summary>
  ///   Resolves a scoped or local key (e.g. <c>world.foo</c>, <c>player.bar</c>, <c>quest:&lt;id&gt;.baz</c>,
  ///   or a plain local key) through the 9.0.2 ScopedKeyResolver and writes the resolved value to a local
  ///   state key. Writes <see cref="Fallback" /> when the key doesn't resolve.
  /// </summary>
  [CreateNodeMenu("Core/State/Get Var")]
  [NodeTint("#6B5C8B")]
  [NodeWidth(400)]
  public sealed class GetVarNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    [BoxGroup("Source")]
    [Required]
    [Tooltip("Scoped (world./player./quest:<id>.) or local key to resolve")]
    public string Key = "world.my_key";

    [BoxGroup("Result")]
    [Required]
    [Tooltip("Local state key the resolved value is written to")]
    public string ResultKey = "result";

    [BoxGroup("Fallback")]
    [Tooltip("Typed literal written to ResultKey when Key doesn't resolve")]
    public GraphVariableDeclaration Fallback = new();

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}
