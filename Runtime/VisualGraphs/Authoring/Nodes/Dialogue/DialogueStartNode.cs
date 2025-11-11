using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes
{
    /// <summary>
    /// Dialogue entry node.
    /// </summary>
    [Node.CreateNodeMenu("Dialogue/Start")]
    [Node.NodeTint("#9C27B0")]
    public sealed class DialogueStartNode : EntryNodeBase
    {
        public override object GetValue(NodePort port)
        {
            return null;
        }
    }
}

