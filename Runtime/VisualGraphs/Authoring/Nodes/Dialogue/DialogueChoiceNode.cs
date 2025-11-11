using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes
{
    /// <summary>
    /// Presents dialogue choices to the player and branches based on selection.
    /// </summary>
    [Node.CreateNodeMenu("Dialogue/Choice")]
    [Node.NodeTint("#FF5722")]
    [Node.NodeWidth(350)]
    public sealed class DialogueChoiceNode : VisualGraphNodeBase
    {
        [Input(connectionType = ConnectionType.Multiple)]
        public NodeConnection input;

        [BoxGroup("Choices")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true)]
        public List<Choice> choices = new();

        [System.Serializable]
        public class Choice
        {
            [TextArea(1, 3)]
            public string text = "Choice text";

            [Tooltip("Optional localization key")]
            public string localizationKey;

            [Output(dynamicPortList = true)]
            public NodeConnection output;
        }

        public override object GetValue(NodePort port)
        {
            return null;
        }
    }
}

