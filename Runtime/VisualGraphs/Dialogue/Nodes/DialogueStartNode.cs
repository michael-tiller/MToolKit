using MToolKit.Runtime.VisualGraphs.Authoring;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Dialogue.Nodes
{
  /// <summary>
  ///   Dialogue entry node.
  /// </summary>
  [CreateNodeMenu("Dialogue/Start")]
  [NodeTint("#8B6B93")]
  public sealed class DialogueStartNode : EntryNodeBase
  {
    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}