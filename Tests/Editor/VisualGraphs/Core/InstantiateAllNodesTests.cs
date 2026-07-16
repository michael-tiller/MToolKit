using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Authoring;
using MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Logic;
using MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Math;
using MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.State;
using MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Transform;
using MToolKit.Runtime.VisualGraphs.Contexts;
using MToolKit.Runtime.VisualGraphs.Executors.Logic;
using MToolKit.Runtime.VisualGraphs.Executors.Math;
using MToolKit.Runtime.VisualGraphs.Executors.State;
using MToolKit.Runtime.VisualGraphs.Executors.Transform;
using MToolKit.Runtime.VisualGraphs.Export;
using MToolKit.Runtime.VisualGraphs.Quest.Graphs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Variables;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;
using XNode;

namespace MToolKit.Tests.Editor.VisualGraphs.Core
{
  /// <summary>
  ///   For each of the 13 9.4 node classes (Math x4, Logic x4, State-query x2, Transform x3): instantiate via
  ///   a real graph asset's <c>AddNode&lt;T&gt;()</c>, assert a non-empty <c>Guid</c>, export through the
  ///   production <see cref="XNodeGraphExporter" /> path, assert the resulting <c>RuntimeNodeDefinition</c>
  ///   has a valid NodeId, AND (Round 8) assert every authored field survives export under its exact
  ///   PascalCase key with its authored value — the direct regression test for the pre-existing
  ///   lowercase-vs-PascalCase exporter/executor casing mismatch documented in TECHNICAL_DEBT.md.
  /// </summary>
  [TestFixture]
  public sealed class InstantiateAllNodesTests : UnityObjectCleanup
  {
    private QuestGraphAsset graph;
    private NodeExecutorRegistry registry;

    [SetUp]
    public void SetUp()
    {
      graph = Track(ScriptableObject.CreateInstance<QuestGraphAsset>());
      graph.name = "InstantiateAllNodesScratch";
      Track(graph.AddNode<TestEntryNode>());

      registry = new NodeExecutorRegistry();
      registry.Register(new AddNodeExecutor());
      registry.Register(new MultiplyNodeExecutor());
      registry.Register(new ClampNodeExecutor());
      registry.Register(new LerpNodeExecutor());
      registry.Register(new AndNodeExecutor());
      registry.Register(new OrNodeExecutor());
      registry.Register(new NotNodeExecutor());
      registry.Register(new XorNodeExecutor());
      registry.Register(new PositionNodeExecutor());
      registry.Register(new RotationNodeExecutor());
      registry.Register(new ScaleNodeExecutor());
      // GetVarNode/CheckWorldStateNode executors need a ScopedKeyResolver — construct with a throwaway one,
      // this test never executes the graph, it only exports it (export doesn't touch executor instances).
      var resolver = new ScopedKeyResolver(new GraphContextRegistry(new RecordingEmitter()));
      registry.Register(new GetVarNodeExecutor(resolver));
      registry.Register(new CheckWorldStateNodeExecutor(resolver));
    }

    private void AssertExportsCleanly<T>(T node, string nodeTypeName, Dictionary<string, object> expectedFields)
      where T : Node
    {
      Assert.That(node, Is.Not.Null);
      var vgNode = node as VisualGraphNodeBase;
      Assert.That(vgNode, Is.Not.Null, $"{nodeTypeName} must derive from VisualGraphNodeBase");
      Assert.That(vgNode.Guid, Is.Not.Null.And.Not.Empty, $"{nodeTypeName} must have a non-empty Guid");

      var def = new XNodeGraphExporter(registry).Export(graph);
      var exportedNode = def.GetNodeById(vgNode.Guid);
      Assert.That(exportedNode, Is.Not.Null, $"{nodeTypeName} must survive export with a valid NodeId");
      Assert.That(exportedNode.NodeId, Is.EqualTo(vgNode.Guid));

      foreach (var kvp in expectedFields)
      {
        Assert.That(exportedNode.Parameters.ContainsKey(kvp.Key), Is.True,
          $"{nodeTypeName}: exported Parameters must contain the exact PascalCase key '{kvp.Key}'");
        Assert.That(exportedNode.Parameters[kvp.Key], Is.EqualTo(kvp.Value),
          $"{nodeTypeName}: exported Parameters['{kvp.Key}'] must equal the authored value");
      }
    }

    [Test]
    public void AddNode_ExportsCleanly()
    {
      var node = Track(graph.AddNode<AddNode>());
      node.AKey = "a"; node.BKey = "b"; node.ResultKey = "result";
      AssertExportsCleanly(node, "AddNode", new Dictionary<string, object> { { "AKey", "a" }, { "BKey", "b" }, { "ResultKey", "result" } });
    }

    [Test]
    public void MultiplyNode_ExportsCleanly()
    {
      var node = Track(graph.AddNode<MultiplyNode>());
      node.AKey = "a"; node.BKey = "b"; node.ResultKey = "result";
      AssertExportsCleanly(node, "MultiplyNode", new Dictionary<string, object> { { "AKey", "a" }, { "BKey", "b" }, { "ResultKey", "result" } });
    }

    [Test]
    public void ClampNode_ExportsCleanly()
    {
      var node = Track(graph.AddNode<ClampNode>());
      node.ValueKey = "value"; node.MinKey = "min"; node.MaxKey = "max"; node.ResultKey = "result";
      AssertExportsCleanly(node, "ClampNode", new Dictionary<string, object>
        { { "ValueKey", "value" }, { "MinKey", "min" }, { "MaxKey", "max" }, { "ResultKey", "result" } });
    }

    [Test]
    public void LerpNode_ExportsCleanly()
    {
      var node = Track(graph.AddNode<LerpNode>());
      node.AKey = "a"; node.BKey = "b"; node.TKey = "t"; node.ResultKey = "result";
      AssertExportsCleanly(node, "LerpNode", new Dictionary<string, object>
        { { "AKey", "a" }, { "BKey", "b" }, { "TKey", "t" }, { "ResultKey", "result" } });
    }

    [Test]
    public void AndNode_ExportsCleanly()
    {
      var node = Track(graph.AddNode<AndNode>());
      node.Keys = new List<string> { "k1", "k2" }; node.ResultKey = "result";
      AssertExportsCleanly(node, "AndNode", new Dictionary<string, object> { { "ResultKey", "result" } });
    }

    [Test]
    public void OrNode_ExportsCleanly()
    {
      var node = Track(graph.AddNode<OrNode>());
      node.Keys = new List<string> { "k1", "k2" }; node.ResultKey = "result";
      AssertExportsCleanly(node, "OrNode", new Dictionary<string, object> { { "ResultKey", "result" } });
    }

    [Test]
    public void NotNode_ExportsCleanly()
    {
      var node = Track(graph.AddNode<NotNode>());
      node.Key = "value"; node.ResultKey = "result";
      AssertExportsCleanly(node, "NotNode", new Dictionary<string, object> { { "Key", "value" }, { "ResultKey", "result" } });
    }

    [Test]
    public void XorNode_ExportsCleanly()
    {
      var node = Track(graph.AddNode<XorNode>());
      node.LeftKey = "left"; node.RightKey = "right"; node.ResultKey = "result";
      AssertExportsCleanly(node, "XorNode", new Dictionary<string, object>
        { { "LeftKey", "left" }, { "RightKey", "right" }, { "ResultKey", "result" } });
    }

    [Test]
    public void GetVarNode_ExportsCleanly()
    {
      var node = Track(graph.AddNode<GetVarNode>());
      node.Key = "world.gold"; node.ResultKey = "result";
      AssertExportsCleanly(node, "GetVarNode", new Dictionary<string, object> { { "Key", "world.gold" }, { "ResultKey", "result" } });
    }

    [Test]
    public void CheckWorldStateNode_ExportsCleanly()
    {
      var node = Track(graph.AddNode<CheckWorldStateNode>());
      node.Key = "world.gold"; node.ComparisonOperator = "GreaterThan";
      AssertExportsCleanly(node, "CheckWorldStateNode", new Dictionary<string, object> { { "Key", "world.gold" }, { "ComparisonOperator", "GreaterThan" } });
    }

    [Test]
    public void PositionNode_ExportsCleanly()
    {
      var node = Track(graph.AddNode<PositionNode>());
      node.SourceKey = "source"; node.DestinationKey = "result";
      AssertExportsCleanly(node, "PositionNode", new Dictionary<string, object> { { "SourceKey", "source" }, { "DestinationKey", "result" } });
    }

    [Test]
    public void RotationNode_ExportsCleanly()
    {
      var node = Track(graph.AddNode<RotationNode>());
      node.SourceKey = "source"; node.DestinationKey = "result";
      AssertExportsCleanly(node, "RotationNode", new Dictionary<string, object> { { "SourceKey", "source" }, { "DestinationKey", "result" } });
    }

    [Test]
    public void ScaleNode_ExportsCleanly()
    {
      var node = Track(graph.AddNode<ScaleNode>());
      node.SourceKey = "source"; node.DestinationKey = "result";
      AssertExportsCleanly(node, "ScaleNode", new Dictionary<string, object> { { "SourceKey", "source" }, { "DestinationKey", "result" } });
    }
  }
}
