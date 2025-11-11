using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes
{
    /// <summary>
    /// Displays a dialogue line and waits for acknowledgment before continuing.
    /// </summary>
    [Node.CreateNodeMenu("Dialogue/Line")]
    [Node.NodeTint("#E91E63")]
    public sealed class DialogueLineNode : VisualGraphNodeBase
    {
        [Input(connectionType = ConnectionType.Multiple)]
        public NodeConnection input;

        [Output(connectionType = ConnectionType.Multiple)]
        public NodeConnection output;

        [BoxGroup("Speaker")]
        [Tooltip("Speaker ID or name")]
        public string speakerId = "NPC";

        [BoxGroup("Content")]
        [TextArea(3, 10)]
        [Tooltip("Dialogue text to display")]
        public string text = "Hello, traveler!";

        [BoxGroup("Content")]
        [Tooltip("Optional localization key")]
        public string localizationKey;

        public override object GetValue(NodePort port)
        {
            return null;
        }
    }
}

