using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes
{
    /// <summary>
    /// Sets a quest stage value and continues to next node.
    /// </summary>
    [Node.CreateNodeMenu("Quest/Set Stage")]
    [Node.NodeTint("#2196F3")]
    public sealed class QuestSetStageNode : VisualGraphNodeBase
    {
        [Input(connectionType = ConnectionType.Multiple)]
        public NodeConnection input;

        [Output(connectionType = ConnectionType.Multiple)]
        public NodeConnection output;

        [BoxGroup("Stage")]
        [Tooltip("Stage key to set (e.g., 'mainQuest', 'subQuest1')")]
        public string stageKey = "mainQuest";

        [BoxGroup("Stage")]
        [Tooltip("Stage value to set (e.g., stage number)")]
        public int stageValue = 1;

        public override object GetValue(NodePort port)
        {
            return null;
        }
    }
}

