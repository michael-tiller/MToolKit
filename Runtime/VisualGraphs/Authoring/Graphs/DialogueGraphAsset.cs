using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring
{
    /// <summary>
    /// Dialogue graph asset for authoring dialogue logic.
    /// </summary>
    [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Dialogue Graph", fileName = "DialogueGraph", order = 101)]
    public sealed class DialogueGraphAsset : NodeGraph
    {
        [TextArea(3, 10)]
        public string description;
        
        [Tooltip("Default speaker for this dialogue")]
        public string defaultSpeaker;
    }
}

