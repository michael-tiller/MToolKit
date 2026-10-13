using MToolKit.Runtime.VisualGraphs.Authoring;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.State
{
    /// <summary>Writes whether a scoped key has a stored value or a declared default.</summary>
    [CreateNodeMenu("Core/State/Has State")]
    public sealed class HasStateNode : VisualGraphNodeBase
    {
        /// <summary>Execution input.</summary>
        [Input(connectionType = ConnectionType.Multiple)] public NodeConnection Input;
        /// <summary>Execution continuation.</summary>
        [Output(connectionType = ConnectionType.Multiple)] public NodeConnection Output;
        /// <summary>Local or scoped variable whose existence is checked.</summary>
        [Required] public string Key = "value";
        /// <summary>Local or scoped Boolean result key.</summary>
        [Required] public string ResultKey = "exists";
        /// <summary>Execution nodes carry no data-flow value.</summary>
        /// <param name="port">The queried authoring port.</param>
        /// <returns>Always null.</returns>
        public override object GetValue(NodePort port) => null;
    }
}
