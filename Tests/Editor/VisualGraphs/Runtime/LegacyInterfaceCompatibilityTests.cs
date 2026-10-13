using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;

namespace MToolKit.Tests.Editor.VisualGraphs.Runtime
{
  /// <summary>Pins interface evolution for implementations that provide only the legacy contract.</summary>
  public sealed class LegacyInterfaceCompatibilityTests
  {
    private sealed class LegacyDefinition : IRuntimeGraphDefinition
    {
      public string GraphId => "legacy_graph";
      public string GraphDomain => "Event";
      public int MaxExecutionSteps => 100;
      public IReadOnlyList<RuntimeSubscriptionDefinition> Subscriptions => Array.Empty<RuntimeSubscriptionDefinition>();
      public IReadOnlyList<RuntimeNodeDefinition> Nodes => Array.Empty<RuntimeNodeDefinition>();
      public IReadOnlyList<RuntimeConnectionDefinition> Connections => Array.Empty<RuntimeConnectionDefinition>();
      public RuntimeNodeDefinition GetNodeById(string nodeId) => null;
      public IEnumerable<RuntimeConnectionDefinition> GetConnectionsFrom(string nodeId) => Array.Empty<RuntimeConnectionDefinition>();
    }

    private sealed class LegacyRouter : IGraphEventRouter
    {
      private readonly List<IGraphRunner> runners = new();
      private readonly HashSet<IGraphRunner> indexed = new();
      public int ClearCalls { get; private set; }
      public int RegisterCalls { get; private set; }
      public bool IsIndexed(IGraphRunner runner) => indexed.Contains(runner);

      public void RegisterRunner(IGraphRunner runner)
      {
        RegisterCalls++;
        runners.Add(runner);
        indexed.Add(runner);
      }

      public void Clear()
      {
        ClearCalls++;
        runners.Clear();
        indexed.Clear();
      }

      // A lazy view proves that the fallback snapshots survivors before it clears legacy indexes.
      public IEnumerable<IGraphRunner> GetRunners() => runners.Where(runner => true);
      public HashSet<Type> GetSubscribedMessageTypes() => new();
      public async UniTask RouteAsync(IGameMessage message, string domain = null, CancellationToken ct = default)
      {
        foreach (var runner in runners.ToArray())
          if (runner.CanHandle(message.GetType(), domain))
            await runner.HandleMessageAsync(message, domain, ct);
      }
    }

    private sealed class LegacyRunner : IGraphRunner
    {
      public string GraphId => "same_graph_id";
      public string GraphDomain => "Event";
      public IRuntimeGraphDefinition Definition { get; } = new LegacyDefinition();
      public int HandledMessages { get; private set; }
      public bool CanHandle(Type messageType, string domain = null) => messageType == typeof(TestMessageA);
      public UniTask HandleMessageAsync(IGameMessage message, string domain = null, CancellationToken ct = default)
      {
        HandledMessages++;
        return UniTask.CompletedTask;
      }
      public GraphStateSnapshot ExportState() => new();
      public void ImportState(GraphStateSnapshot snapshot) { }
    }

    [Test]
    public void LegacyDefinition_ReceivesNullDialogueNameDefault()
    {
      IRuntimeGraphDefinition definition = new LegacyDefinition();
      Assert.That(definition.DialogueName, Is.Null);
      Assert.That(definition.GraphId, Is.EqualTo("legacy_graph"));
    }

    [Test]
    public void LegacyRouter_UnregisterRemovesEveryReferenceAndPreservesOtherRunnerOrder()
    {
      var router = new LegacyRouter();
      var remove = new LegacyRunner();
      var keepFirst = new LegacyRunner();
      var keepSecond = new LegacyRunner();
      router.RegisterRunner(remove);
      router.RegisterRunner(keepFirst);
      router.RegisterRunner(remove);
      router.RegisterRunner(keepSecond);
      router.RegisterRunner(keepFirst);
      router.RegisterRunner(remove);

      ((IGraphEventRouter)router).UnregisterRunner(remove);

      var retained = router.GetRunners().ToArray();
      Assert.That(retained, Has.Length.EqualTo(3));
      Assert.That(retained[0], Is.SameAs(keepFirst));
      Assert.That(retained[1], Is.SameAs(keepSecond));
      Assert.That(retained[2], Is.SameAs(keepFirst), "Duplicate survivor registrations retain their order.");
      Assert.That(router.IsIndexed(remove), Is.False);
      Assert.That(router.IsIndexed(keepFirst), Is.True);
      Assert.That(router.IsIndexed(keepSecond), Is.True);
      Assert.That(router.ClearCalls, Is.EqualTo(1));
      Assert.That(router.RegisterCalls, Is.EqualTo(9), "The fallback rebuilds retained indexes using legacy registration.");

      ((IGraphEventRouter)router).RouteAsync(new TestMessageA()).GetAwaiter().GetResult();
      Assert.That(remove.HandledMessages, Is.Zero, "Removed references receive no later delivery.");
      Assert.That(keepFirst.HandledMessages, Is.EqualTo(2));
      Assert.That(keepSecond.HandledMessages, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LegacyRouter_UnregisterAbsentOrNullRunnerIsANoop(bool nullTarget)
    {
      var router = new LegacyRouter();
      var keep = new LegacyRunner();
      router.RegisterRunner(keep);
      var absent = nullTarget ? null : new LegacyRunner();

      ((IGraphEventRouter)router).UnregisterRunner(absent);

      Assert.That(router.GetRunners().Single(), Is.SameAs(keep));
      Assert.That(router.IsIndexed(keep), Is.True);
      Assert.That(router.ClearCalls, Is.Zero);
      Assert.That(router.RegisterCalls, Is.EqualTo(1));
    }
  }
}
