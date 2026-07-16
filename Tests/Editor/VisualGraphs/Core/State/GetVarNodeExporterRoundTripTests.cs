using System.Linq;
using MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.State;
using MToolKit.Runtime.VisualGraphs.Contexts;
using MToolKit.Runtime.VisualGraphs.Executors.State;
using MToolKit.Runtime.VisualGraphs.Export;
using MToolKit.Runtime.VisualGraphs.Quest.Graphs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Messages;
using MToolKit.Runtime.VisualGraphs.Variables;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;

namespace MToolKit.Tests.Editor.VisualGraphs.Core.State
{
  /// <summary>
  ///   Sources a <see cref="GetVarNode" /> (carrying a non-primitive <see cref="GraphVariableDeclaration" />
  ///   Fallback field) from a real <see cref="QuestGraphAsset" />, exports it through the production
  ///   <see cref="XNodeGraphExporter" /> path, then runs the exported definition through
  ///   <see cref="GraphRunnerHarness" />.
  /// </summary>
  [TestFixture]
  public sealed class GetVarNodeExporterRoundTripTests : UnityObjectCleanup
  {
    [Test]
    public void ExportedGetVarNode_ResolvesWorldKey_WritesResultKey_AndEmitsStateChanged()
    {
      var graph = Track(ScriptableObject.CreateInstance<QuestGraphAsset>());
      graph.name = "GetVarSmokeQuest";
      var entry = Track(graph.AddNode<TestEntryNode>());
      var getVar = Track(graph.AddNode<GetVarNode>());
      getVar.Key = "world.gold";
      getVar.ResultKey = "result";
      getVar.Fallback = new GraphVariableDeclaration { type = EGraphVariableType.Int, intValue = -1 };
      entry.GetOutputPort("Next").Connect(getVar.GetInputPort("Input"));

      var registry = new NodeExecutorRegistry();
      var contextRegistry = new GraphContextRegistry(new RecordingEmitter());
      var resolver = new ScopedKeyResolver(contextRegistry);
      registry.Register(new GetVarNodeExecutor(resolver));
      var def = new XNodeGraphExporter(registry).Export(graph);

      contextRegistry.GetOrCreate(EGraphContextScope.World, null).Variables.Set("gold", 42);

      var harness = new GraphRunnerHarness(def);
      harness.Executors.Register(new GetVarNodeExecutor(resolver));

      harness.Run(new TestMessageA());

      Assert.That(harness.State.TryGet<object>("result", out var result), Is.True);
      Assert.That(result, Is.EqualTo(42));

      var emitted = harness.Emitter.Emitted.Select(e => e.message).OfType<GraphStateChangedMessage>().ToList();
      Assert.That(emitted, Has.Count.EqualTo(1),
        "the exported+executed GetVarNode's non-primitive Fallback field survives the reflection-based export");
      Assert.That(emitted[0].StateKey, Is.EqualTo("result"));
      Assert.That(emitted[0].NewValue, Is.EqualTo(42));
    }
  }
}
