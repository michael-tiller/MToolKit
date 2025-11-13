using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.Messages;
using MToolKit.Runtime.VisualGraphs.Runtime.State;

namespace MToolKit.Runtime.VisualGraphs.Runtime
{
  /// <summary>
  ///   Runs a single graph with idempotent event handling and executor-controlled continuation.
  /// </summary>
  public sealed class GraphRunner : IGraphRunner
  {
    private const string LAST_SEQ_KEY = "__last_seq";
    private const int MAX_EXECUTION_STEPS = 1024;

    private readonly IEventEmitter emitter;
    private readonly NodeExecutorRegistry executors;
    private readonly IServiceProvider services;
    private readonly IGraphState state;

    public GraphRunner(
      IRuntimeGraphDefinition definition,
      IGraphState state,
      NodeExecutorRegistry executors,
      IServiceProvider services,
      IEventEmitter emitter)
    {
      Definition = definition ?? throw new ArgumentNullException(nameof(definition));
      this.state = state ?? throw new ArgumentNullException(nameof(state));
      this.executors = executors ?? throw new ArgumentNullException(nameof(executors));
      this.services = services ?? throw new ArgumentNullException(nameof(services));
      this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    #region IGraphRunner Members

    public string GraphId => Definition.GraphId;
    public string GraphDomain => Definition.GraphDomain;
    public IRuntimeGraphDefinition Definition { get; }

    public bool CanHandle(IEventMessage message)
    {
      if (message == null) return false;

      var domain = message.Domain ?? string.Empty;
      return Definition.Subscriptions.Any(s =>
        s.EventType == message.Type &&
        (string.IsNullOrEmpty(s.EventDomain) || s.EventDomain == domain));
    }

    public async UniTask HandleEventAsync(IEventMessage message, CancellationToken ct = default)
    {
      if (message == null) return;

      // Idempotent: ignore already-processed events
      if (state.TryGet<long>(LAST_SEQ_KEY, out var lastSeq) && message.SequenceId <= lastSeq)
        return;

      state.Set(LAST_SEQ_KEY, message.SequenceId);

      var queue = new NodeExecutionQueue();

      // Find entry nodes - these are the nodes that should start execution
      // Common entry node types: QuestOnEventNode, DialogueStartNode, EntryNodeBase
      foreach (var node in Definition.Nodes)
        if (IsEntryNode(node.NodeType))
          queue.Enqueue(node.NodeId);

      var context = new GraphNodeExecutionContext(queue, services, emitter);
      var steps = 0;

      while (queue.TryDequeue(out var nodeId))
      {
        if (ct.IsCancellationRequested) break;

        if (++steps > MAX_EXECUTION_STEPS)
        {
          emitter.Emit(new BasicEventMessage(
            "Graph.ExecutionHalted",
            Definition.GraphDomain,
            message.SequenceId,
            new { GraphId, Reason = "StepLimit", NodeId = nodeId }));
          break;
        }

        var nodeDef = Definition.GetNodeById(nodeId);
        if (nodeDef == null)
        {
          emitter.Emit(new BasicEventMessage(
            "Graph.NodeMissing",
            Definition.GraphDomain,
            message.SequenceId,
            new { GraphId, NodeId = nodeId }));
          continue;
        }

        IGraphNodeExecutor executor;
        try
        {
          executor = executors.Get(nodeDef.NodeType);
        }
        catch (Exception ex)
        {
          emitter.Emit(new BasicEventMessage(
            "Graph.ExecutorMissing",
            Definition.GraphDomain,
            message.SequenceId,
            new { GraphId, nodeDef.NodeType, Error = ex.Message }));
          continue;
        }

        try
        {
          await executor.ExecuteAsync(Definition, nodeDef, state, message, context, ct);
        }
        catch (Exception ex)
        {
          emitter.Emit(new BasicEventMessage(
            "Graph.ExecutorError",
            Definition.GraphDomain,
            message.SequenceId,
            new { GraphId, NodeId = nodeId, nodeDef.NodeType, Error = ex.ToString() }));
        }
      }
    }

    public GraphStateSnapshot ExportState()
    {
      var snapshot = new GraphStateSnapshot
      {
        GraphId = GraphId,
        Data = new Dictionary<string, object>(state.AsReadOnly())
      };

      if (state.TryGet<long>(LAST_SEQ_KEY, out var lastSeq))
        snapshot.LastSequenceId = lastSeq;

      return snapshot;
    }

    public void ImportState(GraphStateSnapshot snapshot)
    {
      if (snapshot == null || snapshot.GraphId != GraphId)
        return;

      foreach (var kv in snapshot.Data)
        state.Set(kv.Key, kv.Value);

      state.Set(LAST_SEQ_KEY, snapshot.LastSequenceId);
    }

    #endregion

    private static bool IsEntryNode(string nodeType)
    {
      // Common entry node patterns
      return nodeType == "QuestOnEventNode" ||
             nodeType == "DialogueStartNode" ||
             nodeType == "EntryNodeBase" ||
             nodeType.EndsWith("EntryNode");
    }
  }
}