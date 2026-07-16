using MToolKit.Runtime.VisualGraphs.Authoring;
using UnityEngine;
using XNode;

namespace MToolKit.Tests.Editor.VisualGraphs.Support
{
  /// <summary>
  ///   Concrete xNode entry node for exporter tests. <see cref="EntryNodeBase" /> already declares the
  ///   <c>[Output] NodeConnection Next</c> port; this just makes it instantiable.
  /// </summary>
  public sealed class TestEntryNode : EntryNodeBase
  {
    public override object GetValue(NodePort port)
    {
      return null;
    }
  }

  /// <summary>
  ///   Concrete xNode action node for exporter tests. Carries real <c>[Input]/[Output] NodeConnection</c>
  ///   ports (so connections wire and extract) plus a public field and a <c>[SerializeField]</c> private field
  ///   to exercise parameter extraction. Its NodeType (GetType().Name) is "TestActionNode".
  /// </summary>
  public sealed class TestActionNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection In;

    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Next;

    public string PublicParam = "p";

    [SerializeField]
    private int hiddenParam = 7;

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}
