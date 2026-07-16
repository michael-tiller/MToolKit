using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Dialogue.Messages;
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
    private readonly Variables.GraphVariableSet declarations;

    public GraphRunner(
      IRuntimeGraphDefinition definition,
      IGraphState state,
      NodeExecutorRegistry executors,
      IServiceProvider services,
      IEventEmitter emitter,
      Variables.GraphVariableSet declarations = null)
    {
      Definition = definition ?? throw new ArgumentNullException(nameof(definition));
      this.state = state ?? throw new ArgumentNullException(nameof(state));
      this.executors = executors ?? throw new ArgumentNullException(nameof(executors));
      this.services = services ?? throw new ArgumentNullException(nameof(services));
      this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
      this.declarations = declarations; // optional — runners sharing one state must share ONE set (9.0.4)
    }

    #region IGraphRunner Members

    public string GraphId => Definition.GraphId;
    public string GraphDomain => Definition.GraphDomain;
    public IRuntimeGraphDefinition Definition { get; }

    public bool CanHandle(Type messageType, string domain = null)
    {
      if (messageType == null) return false;

      // DialogueContinueMessage can always be handled by dialogue graphs
      // This allows dialogue execution to resume after pausing
      if (messageType == typeof(DialogueContinueMessage))
      {
        if (Definition.GraphDomain == "Dialogue")
          return true;

        // Check nodes manually to avoid LINQ allocation
        foreach (var node in Definition.Nodes)
        {
          if (node.NodeType == "DialogueStartNode" || node.NodeType == "DialogueLineNode" || node.NodeType == "DialogueChoiceNode")
            return true;
        }
        return false;
      }

      var domainFilter = domain ?? string.Empty;
      // Check subscriptions manually to avoid LINQ allocation
      foreach (var subscription in Definition.Subscriptions)
      {
        if (subscription.MessageType != null &&
            subscription.MessageType.Type == messageType &&
            (string.IsNullOrEmpty(subscription.DomainFilter) || subscription.DomainFilter == domainFilter))
        {
          return true;
        }
      }
      return false;
    }

    public async UniTask HandleMessageAsync(IGameMessage message, string domain = null, CancellationToken ct = default)
    {
      if (message == null) return;

      var messageTypeName = message.GetType().Name;
      log.ForMethod().Information("Quest: Graph '{GraphId}' received message '{MessageType}' (domain: {Domain})",
        Definition.GraphId, messageTypeName, domain ?? "null");

      // Special logging for dialogue messages
      if (message is DialogueContinueMessage continueMsg)
      {
        log.ForMethod().Information("Dialogue: Graph '{GraphId}' received DialogueContinueMessage with GraphId={MessageGraphId}",
          Definition.GraphId, continueMsg.GraphId);
      }

      // Emit debug event for graph execution start
      NodeDebugEvents.RaiseGraphExecutionChanged(
        Definition.GraphId,
        Definition.GraphDomain,
        isStarting: true,
        message.GetType().Name);

      var queue = new NodeExecutionQueue();

      // Special handling for DialogueContinueMessage: read next node IDs from state
      if (message is DialogueContinueMessage continueMessage)
      {
        // Verify graph ID matches
        if (!string.IsNullOrEmpty(continueMessage.GraphId) && continueMessage.GraphId != Definition.GraphId)
        {
          log.ForMethod().Verbose("DialogueContinueMessage GraphId mismatch: expected {ExpectedGraphId}, got {ActualGraphId} - ignoring",
            Definition.GraphId, continueMessage.GraphId);
          return;
        }

        // Read next node IDs from state
        if (state.TryGet<List<string>>("Dialogue.NextNodeIds", out var nextNodeIds) && nextNodeIds != null && nextNodeIds.Count > 0)
        {
          log.ForMethod().Information("Dialogue: Continuing execution for graph '{GraphId}' with {Count} next node ID(s): {NodeIds}",
            Definition.GraphId, nextNodeIds.Count, string.Join(", ", nextNodeIds));

          foreach (var nodeId in nextNodeIds)
          {
            queue.Enqueue(nodeId);
          }

          // Clear the next node IDs from state
          state.Set<List<string>>("Dialogue.NextNodeIds", new List<string>());
        }
        else
        {
          // Dialogue has naturally ended - emit close message instead of warning
          log.ForMethod().Information("Dialogue: No next node IDs found in state for graph '{GraphId}'. Dialogue has ended - closing dialogue view.",
            Definition.GraphId);

          // Emit DialogueProgressMessage with shouldClose=true to gracefully close the dialogue
          emitter.Emit(new DialogueProgressMessage(shouldClose: true, Definition.GraphId), Definition.GraphDomain);
          return;
        }
      }
      else
      {
        // Normal message handling: find entry nodes - these are the nodes that should start execution
        // Common entry node types: QuestOnEventNode, DialogueStartNode, EntryNodeBase
        var entryNodeCount = 0;
        foreach (var node in Definition.Nodes)
          if (IsEntryNode(node.NodeType) && EntryNodeMatches(node, message, domain))
          {
            queue.Enqueue(node.NodeId);
            entryNodeCount++;
          }

        log.ForMethod().Information("Quest: Found {EntryNodeCount} entry nodes in graph '{GraphId}'",
          entryNodeCount, Definition.GraphId);
      }

      var context = new GraphNodeExecutionContext(queue, services, emitter);
      var steps = 0;
      var maxSteps = Definition.MaxExecutionSteps;

      // Per-message reentrancy guard. Authoring-time feedback edges (e.g. Increment.Output → Check.Input
      // for "re-evaluate after incrementing") are valid and intended, but each node should execute at most
      // once per message dispatch — without this, a single message walks the loop to completion and the
      // increment fires N times for one event.
      var executedThisDispatch = new HashSet<string>();

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

        if (!executedThisDispatch.Add(nodeId))
        {
          log.ForMethod().Verbose("Quest: Graph '{GraphId}' - node {NodeId} already executed this dispatch, skipping (feedback-edge guard)",
            Definition.GraphId, nodeId);
          continue;
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
        // For dialogue graphs, store next node IDs in state instead of enqueueing directly
        // This ensures proper pausing after each dialogue node
        if (IsEntryNode(nodeDef.NodeType))
        {
          log.ForMethod().Information("Quest: Processing entry node {NodeId} (type: {NodeType})",
            nodeId, nodeDef.NodeType);

          // Check if this is a dialogue graph - if so, store next node IDs in state
          // Otherwise, enqueue directly (for quests and other systems)
          // Iterate connections directly to avoid LINQ allocations in hot path
          var connectionCount = 0;
          var nextNodeIds = new List<string>();

          foreach (var connection in Definition.GetConnectionsFrom(nodeId))
          {
            connectionCount++;

            if (Definition.GraphDomain == "Dialogue" || nodeDef.NodeType == "DialogueStartNode")
            {
              // For dialogue, collect node IDs to store in state
              nextNodeIds.Add(connection.ToNodeId);

              // Log connection details for debugging
              var targetNode = Definition.GetNodeById(connection.ToNodeId);
              var targetText = targetNode?.NodeType == "DialogueLineNode" && targetNode.Parameters.TryGetValue("Text", out var txt)
                ? txt as string ?? ""
                : "N/A";
              log.ForMethod().Information("Dialogue: Entry node connection: {FromNodeId} -> {ToNodeId} (Port: {PortName}, Target Text: '{Text}')",
                connection.FromNodeId, connection.ToNodeId, connection.PortName, targetText);
            }
            else
            {
              // For non-dialogue graphs, enqueue directly
              queue.Enqueue(connection.ToNodeId);
              log.ForMethod().Information("Quest: Enqueued node {ToNodeId} from entry node {FromNodeId}",
                connection.ToNodeId, nodeId);
            }
          }

          if (Definition.GraphDomain == "Dialogue" || nodeDef.NodeType == "DialogueStartNode")
          {
            if (nextNodeIds.Count > 0)
            {
              state.Set("Dialogue.NextNodeIds", nextNodeIds);
              log.ForMethod().Information("Dialogue: Entry node {NodeId} stored {Count} next node ID(s) in state: {NodeIds}. Will process when DialogueContinueMessage is received.",
                nodeId, nextNodeIds.Count, string.Join(", ", nextNodeIds));
              // Break immediately - execution will continue when DialogueContinueMessage is received
              break;
            }
          }
          else
          {
            log.ForMethod().Information("Quest: Entry node {NodeId} enqueued {ConnectionCount} connected node(s)",
              nodeId, connectionCount);
          }
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

        // Log node details for dialogue nodes
        if (nodeDef.NodeType == "DialogueLineNode" && nodeDef.Parameters.TryGetValue("Text", out var textParam))
        {
          var nodeText = textParam as string ?? "";
          log.ForMethod().Information("Quest: Executing DialogueLineNode {NodeId} in graph '{GraphId}' with text '{Text}'",
            nodeId, Definition.GraphId, nodeText);
        }
        else
        {
          log.ForMethod().Information("Quest: Executing node {NodeId} (type: {NodeType}) in graph '{GraphId}'",
            nodeId, nodeDef.NodeType, Definition.GraphId);
        }

        var stopwatch = Stopwatch.StartNew();
        string errorMessage = null;

        try
        {
          await executor.Execute(Definition, nodeDef, state, message, context, ct);
          log.ForMethod().Information("Quest: Completed execution of node {NodeType} ({NodeId})",
            nodeDef.NodeType, nodeId);

          // Check if this is a dialogue node that has stored next node IDs in state
          // If so, pause execution and wait for DialogueContinueMessage
          // This allows dialogue to pause after each line/choice and wait for user input
          if (nodeDef.NodeType == "DialogueLineNode" || nodeDef.NodeType == "DialogueChoiceNode")
          {
            if (state.TryGet<List<string>>("Dialogue.NextNodeIds", out var storedNextNodes) &&
                storedNextNodes != null && storedNextNodes.Count > 0)
            {
              log.ForMethod().Information("Dialogue: Pausing execution after {NodeType} ({NodeId}). Waiting for user input to continue.",
                nodeDef.NodeType, nodeId);
              // Stop processing - execution will resume when DialogueContinueMessage is received
              break;
            }
          }
        }
        catch (ArgumentException)
        {
          // Invalid node configuration is an authoring error, not an operational graph
          // failure. In particular, ScopedKeyResolver guarantees malformed reserved
          // keys fail loudly; swallowing them here turns a bad graph into a silent
          // fallback and violates that contract.
          throw;
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
      var filteredData = new Dictionary<string, object>();

      // Filter out ScriptableObject references - convert to GUIDs or skip
      foreach (var kv in state.AsReadOnly())
      {
        // Skip ScriptableObject references (they can't be serialized by ES3)
        // QuestDefinition and other ScriptableObjects should be resolved by GUID at load time
        if (kv.Value is UnityEngine.ScriptableObject)
        {
          log.ForMethod().Debug("Skipping ScriptableObject reference in state: {Key} (type: {Type})",
            kv.Key, kv.Value.GetType().Name);
          continue;
        }

        filteredData[kv.Key] = kv.Value;
      }

      return new GraphStateSnapshot
      {
        GraphId = GraphId,
        Data = filteredData
      };
    }

    public void ImportState(GraphStateSnapshot snapshot)
    {
      if (snapshot == null || snapshot.GraphId != GraphId || snapshot.Data == null)
        return;

      // 9.0.4 schema-change behavior #4: a saved value whose type no longer matches its declaration is
      // discarded loudly and the declared default applies — sanitize a copy, never the caller's snapshot.
      var data = new Dictionary<string, object>(snapshot.Data);
      Persistence.GraphSnapshotSchemaSanitizer.SanitizeTypeMismatches(data, declarations, GraphId);

      foreach (var kv in data)
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

    /// <summary>
    ///   Gate an entry node on the message that triggered this dispatch. An entry node that declares a
    ///   MessageType only starts execution for messages of that exact type, and one that declares a
    ///   DomainFilter only starts for that exact domain. Without this, every entry node in a multi-trigger
    ///   graph fires on ANY subscribed message — which is both wrong (trigger A's action chain runs for
    ///   trigger B's event) and the ignition path for event-graph feedback loops. Entry nodes that declare
    ///   neither (dialogue starts, legacy quest entries) keep the fire-on-dispatch behavior.
    /// </summary>
    private static bool EntryNodeMatches(DTOs.RuntimeNodeDefinition node, IGameMessage message, string domain)
    {
      if (node.Parameters == null) return true;

      if (node.Parameters.TryGetValue("MessageType", out var typeParam) &&
          typeParam is Core.Types.MessageTypeReference typeRef && typeRef.IsValid &&
          typeRef.Type != message.GetType())
        return false;

      if (node.Parameters.TryGetValue("DomainFilter", out var filterParam) &&
          filterParam is string filter && !string.IsNullOrEmpty(filter) &&
          !string.Equals(filter, domain ?? string.Empty, StringComparison.Ordinal))
        return false;

      return true;
    }
  }
}
