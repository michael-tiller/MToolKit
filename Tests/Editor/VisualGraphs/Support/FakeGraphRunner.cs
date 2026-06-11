using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.State;

namespace MToolKit.Tests.Editor.VisualGraphs.Support
{
  /// <summary>
  ///   Recording <see cref="IGraphRunner" /> used to pin both the router (HandleMessageAsync call counts,
  ///   cancellation) and GraphStateSaveController (ExportState / ImportState round-trip). It records every
  ///   message it is handed and every snapshot imported, and can be configured to throw on export.
  /// </summary>
  public sealed class FakeGraphRunner : IGraphRunner
  {
    public FakeGraphRunner(string graphId, IRuntimeGraphDefinition definition = null, string graphDomain = "Quest")
    {
      GraphId = graphId;
      GraphDomain = graphDomain;
      Definition = definition ?? new TestRuntimeGraphDefinition { GraphId = graphId, GraphDomain = graphDomain };
      ExportSnapshot = new GraphStateSnapshot { GraphId = graphId };
    }

    public int HandleMessageAsyncCallCount { get; private set; }
    public List<(IGameMessage message, string domain)> Handled { get; } = new();
    public List<GraphStateSnapshot> Imported { get; } = new();

    /// <summary>Snapshot returned by ExportState (defaults to an empty snapshot keyed to this graph id).</summary>
    public GraphStateSnapshot ExportSnapshot { get; set; }

    /// <summary>When true, ExportState throws — to pin that one runner's failure does not abort the save.</summary>
    public bool ThrowOnExport { get; set; }

    public string GraphId { get; }
    public string GraphDomain { get; }
    public IRuntimeGraphDefinition Definition { get; }

    public bool CanHandle(Type messageType, string domain = null)
    {
      if (messageType == null || Definition?.Subscriptions == null)
        return false;

      foreach (var sub in Definition.Subscriptions)
        if (sub.MessageType != null && sub.MessageType.Type == messageType &&
            (string.IsNullOrEmpty(sub.DomainFilter) || sub.DomainFilter == (domain ?? string.Empty)))
          return true;

      return false;
    }

    public UniTask HandleMessageAsync(IGameMessage message, string domain = null, CancellationToken ct = default)
    {
      HandleMessageAsyncCallCount++;
      Handled.Add((message, domain));
      return UniTask.CompletedTask;
    }

    public GraphStateSnapshot ExportState()
    {
      if (ThrowOnExport)
        throw new InvalidOperationException($"FakeGraphRunner '{GraphId}' intentional export failure");

      return ExportSnapshot;
    }

    public void ImportState(GraphStateSnapshot snapshot)
    {
      Imported.Add(snapshot);
    }
  }
}
