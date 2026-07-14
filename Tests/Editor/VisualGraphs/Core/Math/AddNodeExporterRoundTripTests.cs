using System.Linq;
using MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Math;
using MToolKit.Runtime.VisualGraphs.Executors.Math;
using MToolKit.Runtime.VisualGraphs.Export;
using MToolKit.Runtime.VisualGraphs.Quest.Graphs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Messages;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;

namespace MToolKit.Tests.Editor.VisualGraphs.Core.Math
{
  /// <summary>
  ///   Sources an <see cref="AddNode" /> from a real <see cref="QuestGraphAsset" />, exports it through the
  ///   SAME production path a real quest graph goes through (<see cref="XNodeGraphExporter" />), then runs
  ///   the exported definition through <see cref="GraphRunnerHarness" /> — this is the DoD's "a math node in
  ///   a quest graph computes and publishes results" integration smoke.
  /// </summary>
  [TestFixture]
  public sealed class AddNodeExporterRoundTripTests : UnityObjectCleanup
  {
    [Test]
    public void ExportedAddNode_ComputesSum_WritesResultKey_AndEmitsStateChanged()
    {
      var graph = Track(ScriptableObject.CreateInstance<QuestGraphAsset>());
      graph.name = "AddSmokeQuest";
      var entry = Track(graph.AddNode<TestEntryNode>());
      var add = Track(graph.AddNode<AddNode>());
      add.AKey = "a";
      add.BKey = "b";
      add.ResultKey = "result";
      entry.GetOutputPort("Next").Connect(add.GetInputPort("Input"));

      var registry = new NodeExecutorRegistry();
      registry.Register(new AddNodeExecutor());
      var def = new XNodeGraphExporter(registry).Export(graph);

      var harness = new GraphRunnerHarness(def);
      harness.Executors.Register(new AddNodeExecutor());
      harness.State.Set("a", 2f);
      harness.State.Set("b", 3f);

      harness.Run(new TestMessageA());

      Assert.That(harness.State.TryGet<float>("result", out var result), Is.True);
      Assert.That(result, Is.EqualTo(5f));

      var emitted = harness.Emitter.Emitted.Select(e => e.message).OfType<GraphStateChangedMessage>().ToList();
      Assert.That(emitted, Has.Count.EqualTo(1),
        "the exported+executed AddNode's result write is observable via a GraphStateChangedMessage");
      Assert.That(emitted[0].StateKey, Is.EqualTo("result"));
      Assert.That(emitted[0].NewValue, Is.EqualTo(5f));
    }
  }
}
