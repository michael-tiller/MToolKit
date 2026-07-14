using System.Collections.Generic;
using System.Linq;
using MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Logic;
using MToolKit.Runtime.VisualGraphs.Executors.Logic;
using MToolKit.Runtime.VisualGraphs.Export;
using MToolKit.Runtime.VisualGraphs.Quest.Graphs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Messages;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;

namespace MToolKit.Tests.Editor.VisualGraphs.Core.Logic
{
  /// <summary>
  ///   Sources an <see cref="AndNode" /> (n-ary, list-typed <c>Keys</c> field) from a real
  ///   <see cref="QuestGraphAsset" />, exports it through the production <see cref="XNodeGraphExporter" />
  ///   path, then runs the exported definition through <see cref="GraphRunnerHarness" />.
  /// </summary>
  [TestFixture]
  public sealed class AndNodeExporterRoundTripTests : UnityObjectCleanup
  {
    [Test]
    public void ExportedAndNode_FoldsKeys_WritesResultKey_AndEmitsStateChanged()
    {
      var graph = Track(ScriptableObject.CreateInstance<QuestGraphAsset>());
      graph.name = "AndSmokeQuest";
      var entry = Track(graph.AddNode<TestEntryNode>());
      var and = Track(graph.AddNode<AndNode>());
      and.Keys = new List<string> { "k1", "k2" };
      and.ResultKey = "result";
      entry.GetOutputPort("Next").Connect(and.GetInputPort("Input"));

      var registry = new NodeExecutorRegistry();
      registry.Register(new AndNodeExecutor());
      var def = new XNodeGraphExporter(registry).Export(graph);

      var harness = new GraphRunnerHarness(def);
      harness.Executors.Register(new AndNodeExecutor());
      harness.State.Set("k1", true);
      harness.State.Set("k2", true);

      harness.Run(new TestMessageA());

      Assert.That(harness.State.TryGet<bool>("result", out var result), Is.True);
      Assert.That(result, Is.True);

      var emitted = harness.Emitter.Emitted.Select(e => e.message).OfType<GraphStateChangedMessage>().ToList();
      Assert.That(emitted, Has.Count.EqualTo(1),
        "the exported+executed AndNode's Keys list survives the reflection-based PascalCase parameter export");
      Assert.That(emitted[0].StateKey, Is.EqualTo("result"));
      Assert.That(emitted[0].NewValue, Is.EqualTo(true));
    }
  }
}
