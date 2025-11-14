using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.Debug;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Runtime
{
  /// <summary>
  ///   Runs a single graph, consuming IGameMessage directly from MessagePipe.
  /// </summary>
  public sealed class GraphRunner : IGraphRunner
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<GraphRunner>().ForFeature("VisualGraphs"));
    private static ILogger log => logLazy.Value ?? Logger.None;

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

    public bool CanHandle(Type messageType, string domain = null)
    {
      if (messageType == null) return false;

      var domainFilter = domain ?? string.Empty;
      return Definition.Subscriptions.Any(s =>
        s.MessageType != null &&
        s.MessageType.Type == messageType &&
        (string.IsNullOrEmpty(s.DomainFilter) || s.DomainFilter == domainFilter));
    }

    public async UniTask HandleMessageAsync(IGameMessage message, string domain = null, CancellationToken ct = default)
    {
      if (message == null) return;

      log.ForMethod().Information("Quest: Graph '{GraphId}' received message '{MessageType}' (domain: {Domain})",
        Definition.GraphId, message.GetType().Name, domain ?? "null");

      // Emit debug event for graph execution start
      NodeDebugEvents.RaiseGraphExecutionChanged(
        Definition.GraphId,
        Definition.GraphDomain,
        isStarting: true,
        message.GetType().Name);

      var queue = new NodeExecutionQueue();

      // Find entry nodes - these are the nodes that should start execution
      // Common entry node types: QuestOnEventNode, DialogueStartNode, EntryNodeBase
      var entryNodeCount = 0;
      foreach (var node in Definition.Nodes)
        if (IsEntryNode(node.NodeType))
        {
          queue.Enqueue(node.NodeId);
          entryNodeCount++;
        }

      log.ForMethod().Information("Quest: Found {EntryNodeCount} entry nodes in graph '{GraphId}'",
        entryNodeCount, Definition.GraphId);

      var context = new GraphNodeExecutionContext(queue, services, emitter);
      var steps = 0;
      var maxSteps = Definition.MaxExecutionSteps;

      while (queue.TryDequeue(out var nodeId))
      {
        if (ct.IsCancellationRequested) break;

        if (++steps > maxSteps)
        {
          log.ForMethod().Warning("Quest: Graph '{GraphId}' execution halted - max steps ({MaxSteps}) reached",
            Definition.GraphId, maxSteps);
          // TODO: Emit Graph.ExecutionHalted message
          break;
        }

        var nodeDef = Definition.GetNodeById(nodeId);
        if (nodeDef == null)
        {
          log.ForMethod().Warning("Quest: Graph '{GraphId}' - node {NodeId} not found in definition",
            Definition.GraphId, nodeId);
          // TODO: Emit Graph.NodeMissing message
          continue;
        }

        // Entry nodes are subscription points - they don't have executors
        // Instead, enqueue all nodes connected from this entry node
        if (IsEntryNode(nodeDef.NodeType))
        {
          log.ForMethod().Information("Quest: Processing entry node {NodeId} (type: {NodeType}) - enqueueing connected nodes",
            nodeId, nodeDef.NodeType);
          var connectionCount = 0;
          foreach (var connection in Definition.GetConnectionsFrom(nodeId))
          {
            queue.Enqueue(connection.ToNodeId);
            connectionCount++;
            log.ForMethod().Information("Quest: Enqueued node {ToNodeId} from entry node {FromNodeId}",
              connection.ToNodeId, nodeId);
          }
          log.ForMethod().Information("Quest: Entry node {NodeId} enqueued {ConnectionCount} connected node(s)",
            nodeId, connectionCount);
          continue;
        }

        IGraphNodeExecutor executor;
        try
        {
          executor = executors.Get(nodeDef.NodeType);
        }
        catch (Exception ex)
        {
          log.ForMethod().Error(ex, "Quest: Graph '{GraphId}' - no executor found for node {NodeId} (type: {NodeType})",
            Definition.GraphId, nodeId, nodeDef.NodeType);
          // TODO: Emit Graph.ExecutorMissing message
          continue;
        }

        log.ForMethod().Information("Quest: Executing node {NodeId} (type: {NodeType}) in graph '{GraphId}'",
          nodeId, nodeDef.NodeType, Definition.GraphId);

        var stopwatch = Stopwatch.StartNew();
        string errorMessage = null;

        try
        {
          await executor.ExecuteAsync(Definition, nodeDef, state, message, context, ct);
          log.ForMethod().Information("Quest: Completed execution of node {NodeType} ({NodeId})",
            nodeDef.NodeType, nodeId);
        }
        catch (Exception ex)
        {
          errorMessage = ex.Message;
          log.ForMethod().Error(ex, "Quest: Graph '{GraphId}' - error executing node {NodeType} ({NodeId}): {Message}",
            Definition.GraphId, nodeDef.NodeType, nodeId, ex.Message);
          // TODO: Emit Graph.ExecutorError message
        }
        finally
        {
          stopwatch.Stop();
          // Emit debug event for node execution
          NodeDebugEvents.RaiseNodeExecuted(
            Definition.GraphId,
            nodeId,
            nodeDef.NodeType,
            stopwatch.Elapsed,
            payload: message,
            errorMessage: errorMessage);
        }
      }

      // Emit debug event for graph execution end
      NodeDebugEvents.RaiseGraphExecutionChanged(
        Definition.GraphId,
        Definition.GraphDomain,
        isStarting: false,
        message.GetType().Name);
    }

    public GraphStateSnapshot ExportState()
    {
      return new GraphStateSnapshot
      {
        GraphId = GraphId,
        Data = new Dictionary<string, object>(state.AsReadOnly())
      };
    }

    public void ImportState(GraphStateSnapshot snapshot)
    {
      if (snapshot == null || snapshot.GraphId != GraphId)
        return;

      foreach (var kv in snapshot.Data)
        state.Set(kv.Key, kv.Value);
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