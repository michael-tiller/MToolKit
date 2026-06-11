using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MToolKit.Runtime.Core.Types;
using MToolKit.Runtime.VisualGraphs.Export;
using MToolKit.Runtime.VisualGraphs.Quest.Graphs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using XNode;

namespace MToolKit.Tests.Editor.VisualGraphs.Export
{
  /// <summary>
  ///   Characterization of <see cref="XNodeGraphExporter" /> over in-memory xNode graphs (no assets on disk).
  ///   Pins validation (entry-node required, executor required), per-graph config copy (max steps, domain,
  ///   subscriptions with invalid skipped), node-id = GUID, connection extraction from real output ports, and
  ///   the two Debug-logged guards (self-loop skip, duplicate-GUID drop) verified via LogAssert. Parameter
  ///   extraction is asserted as a SUBSET (public port fields also appear) plus a no-Unity-internal-leak check.
  /// </summary>
  [TestFixture]
  public sealed class XNodeGraphExporterTests : UnityObjectCleanup
  {
    // NOTE: this fixture does NOT set LogAssert.ignoreFailingMessages. The exporter's only Unity Debug logs are
    // the self-loop Warning and the duplicate-GUID Error, each consumed by an explicit LogAssert.Expect below.
    // Suppressing instead of consuming them lets the unmatched log linger in LogAssert's queue and fail an
    // unrelated later test in the same domain (it did: a Dirigible WorkOrder test caught the leaked dup-GUID error).

    private QuestGraphAsset NewGraph(string name = "TestGraph")
    {
      var graph = Track(ScriptableObject.CreateInstance<QuestGraphAsset>());
      graph.name = name;
      return graph;
    }

    private T AddNode<T>(QuestGraphAsset graph) where T : Node
    {
      return Track(graph.AddNode<T>());
    }

    private static XNodeGraphExporter ExporterWithActionExecutor()
    {
      var registry = new NodeExecutorRegistry();
      registry.Register(new RecordingExecutor("TestActionNode"));
      return new XNodeGraphExporter(registry);
    }

    [Test]
    public void Export_NullGraph_ThrowsArgumentNull()
    {
      Assert.Throws<ArgumentNullException>(() => ExporterWithActionExecutor().Export(null));
    }

    [Test]
    public void Validate_NoEntryNode_ThrowsInvalidGraph()
    {
      var graph = NewGraph();
      AddNode<TestActionNode>(graph); // an action node but no entry node

      var ex = Assert.Throws<InvalidGraphException>(() => ExporterWithActionExecutor().Export(graph));
      Assert.That(ex.Message, Does.Contain("entry node"));
    }

    [Test]
    public void Validate_UnregisteredNodeType_ThrowsInvalidGraph()
    {
      var graph = NewGraph();
      AddNode<TestEntryNode>(graph);
      AddNode<TestActionNode>(graph);

      var exporter = new XNodeGraphExporter(new NodeExecutorRegistry()); // no executor registered
      var ex = Assert.Throws<InvalidGraphException>(() => exporter.Export(graph));
      Assert.That(ex.Message, Does.Contain("No executor registered"));
    }

    [Test]
    public void Export_QuestGraph_CopiesMaxSteps_Domain_AndId()
    {
      var graph = NewGraph("MyQuest");
      graph.MaxExecutionSteps = 512;
      AddNode<TestEntryNode>(graph);

      var def = ExporterWithActionExecutor().Export(graph);

      Assert.That(def.GraphId, Is.EqualTo("MyQuest"));
      Assert.That(def.GraphDomain, Is.EqualTo("Quest"));
      Assert.That(def.MaxExecutionSteps, Is.EqualTo(512));
    }

    [Test]
    public void Export_CopiesValidSubscriptions_SkipsInvalid()
    {
      var graph = NewGraph();
      AddNode<TestEntryNode>(graph);
      graph.Subscriptions = new List<MessageSubscription>
      {
        new() { MessageType = new MessageTypeReference(typeof(TestMessageA)), Required = false },
        new() { MessageType = new MessageTypeReference { Type = null }, Required = false }
      };

      var def = ExporterWithActionExecutor().Export(graph);

      Assert.That(def.Subscriptions.Count, Is.EqualTo(1), "the invalid (Type == null) subscription is skipped");
      Assert.That(def.Subscriptions[0].MessageType.Type, Is.EqualTo(typeof(TestMessageA)));
    }

    [Test]
    public void Export_NodeIds_AreNodeGuids()
    {
      var graph = NewGraph();
      var entry = AddNode<TestEntryNode>(graph);
      var action = AddNode<TestActionNode>(graph);

      var def = ExporterWithActionExecutor().Export(graph);

      Assert.That(entry.Guid, Is.Not.Empty, "nodes get a stable GUID on creation (Init)");
      Assert.That(def.Nodes.Select(n => n.NodeId), Is.EquivalentTo(new[] { entry.Guid, action.Guid }));
    }

    [Test]
    public void Export_ExtractsConnections_FromOutputPorts_AndBuildsLookup()
    {
      var graph = NewGraph();
      var entry = AddNode<TestEntryNode>(graph);
      var action = AddNode<TestActionNode>(graph);
      entry.GetOutputPort("Next").Connect(action.GetInputPort("In"));

      var def = ExporterWithActionExecutor().Export(graph);

      var conn = def.Connections.Single();
      Assert.That(conn.FromNodeId, Is.EqualTo(entry.Guid));
      Assert.That(conn.ToNodeId, Is.EqualTo(action.Guid));
      Assert.That(conn.PortName, Is.EqualTo("Next"));
      Assert.That(def.GetConnectionsFrom(entry.Guid).Single().ToNodeId, Is.EqualTo(action.Guid),
        "BuildLookupCaches wires GetConnectionsFrom");
    }

    [Test]
    public void Export_SelfLoop_SkippedWithWarning()
    {
      var graph = NewGraph();
      AddNode<TestEntryNode>(graph);
      var action = AddNode<TestActionNode>(graph);
      action.GetOutputPort("Next").Connect(action.GetInputPort("In")); // self-loop

      LogAssert.Expect(LogType.Warning, new Regex("self-loop"));
      var def = ExporterWithActionExecutor().Export(graph);

      Assert.That(def.Connections, Is.Empty, "the self-loop connection (from == to) is skipped");
    }

    [Test]
    public void Export_DuplicateGuid_SecondNodeDroppedWithError()
    {
      var graph = NewGraph();
      AddNode<TestEntryNode>(graph);
      var a1 = AddNode<TestActionNode>(graph);
      var a2 = AddNode<TestActionNode>(graph);
      a1.SetGuidFromHint("dup-guid");
      a2.SetGuidFromHint("dup-guid");

      LogAssert.Expect(LogType.Error, new Regex("Duplicate node ID"));
      var def = ExporterWithActionExecutor().Export(graph);

      Assert.That(def.Nodes.Count(n => n.NodeType == "TestActionNode"), Is.EqualTo(1),
        "the second node sharing a GUID is dropped to prevent connection corruption");
    }

    [Test]
    public void Export_ParameterExtraction_IncludesPublicAndSerializeField_NoUnityInternalLeak()
    {
      var graph = NewGraph();
      AddNode<TestEntryNode>(graph);
      AddNode<TestActionNode>(graph);

      var def = ExporterWithActionExecutor().Export(graph);
      var action = def.Nodes.Single(n => n.NodeType == "TestActionNode");

      Assert.That(action.Parameters.ContainsKey("PublicParam"), Is.True, "public field is extracted");
      Assert.That(action.Parameters.ContainsKey("hiddenParam"), Is.True, "[SerializeField] private field is extracted");
      Assert.That(action.Parameters.Keys.Any(k => k.StartsWith("m_")), Is.False,
        "no Unity-internal m_ fields leak into exported parameters");
    }
  }
}
