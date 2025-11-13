using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Dialogue
{
  /// <summary>
  ///   Dialogue entry node.
  /// </summary>
  [CreateNodeMenu("Dialogue/Start")]
  [NodeTint("#9C27B0")]
  public sealed class DialogueStartNode : EntryNodeBase
  {
    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}