using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring
{
    /// <summary>
    /// Quest graph asset for authoring quest logic.
    /// </summary>
    [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Quest Graph", fileName = "QuestGraph", order = 100)]
    public sealed class QuestGraphAsset : NodeGraph
    {
        [TextArea(3, 10)]
        public string description;
    }
}

