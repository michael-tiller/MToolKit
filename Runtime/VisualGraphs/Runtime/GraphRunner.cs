using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace MToolKit.Runtime.VisualGraphs
{
    /// <summary>
    /// Runs a single graph with idempotent event handling and executor-controlled continuation.
    /// </summary>
    public sealed class GraphRunner : IGraphRunner
    {
        private const string LastSeqKey = "__last_seq";
        private const int MaxExecutionSteps = 1024;

        private readonly IRuntimeGraphDefinition _definition;
        private readonly IGraphState _state;
        private readonly NodeExecutorRegistry _executors;
        private readonly IServiceProvider _services;
        private readonly IEventEmitter _emitter;

        public string GraphId => _definition.GraphId;
        public string GraphDomain => _definition.GraphDomain;
        public IRuntimeGraphDefinition Definition => _definition;

        public GraphRunner(
            IRuntimeGraphDefinition definition,
            IGraphState state,
            NodeExecutorRegistry executors,
            IServiceProvider services,
            IEventEmitter emitter)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _executors = executors ?? throw new ArgumentNullException(nameof(executors));
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
        }

        public bool CanHandle(IEventMessage message)
        {
            if (message == null) return false;
            
            var domain = message.Domain ?? string.Empty;
            return _definition.Subscriptions.Any(s =>
                s.EventType == message.Type &&
                (string.IsNullOrEmpty(s.EventDomain) || s.EventDomain == domain));
        }

        public async UniTask HandleEventAsync(IEventMessage message, CancellationToken ct = default)
        {
            if (message == null) return;

            // Idempotent: ignore already-processed events
            if (_state.TryGet<long>(LastSeqKey, out var lastSeq) && message.SequenceId <= lastSeq)
                return;

            _state.Set(LastSeqKey, message.SequenceId);

            var queue = new NodeExecutionQueue();
            
            // Find entry nodes - these are the nodes that should start execution
            // Common entry node types: QuestOnEventNode, DialogueStartNode, EntryNodeBase
            foreach (var node in _definition.Nodes)
            {
                if (IsEntryNode(node.NodeType))
                {
                    queue.Enqueue(node.NodeId);
                }
            }

            var context = new GraphNodeExecutionContext(queue, _services, _emitter);
            var steps = 0;

            while (queue.TryDequeue(out var nodeId))
            {
                if (ct.IsCancellationRequested) break;

                if (++steps > MaxExecutionSteps)
                {
                    _emitter.Emit(new BasicEventMessage(
                        "Graph.ExecutionHalted",
                        _definition.GraphDomain,
                        message.SequenceId,
                        new { GraphId, Reason = "StepLimit", NodeId = nodeId },
                        null));
                    break;
                }

                var nodeDef = _definition.GetNodeById(nodeId);
                if (nodeDef == null)
                {
                    _emitter.Emit(new BasicEventMessage(
                        "Graph.NodeMissing",
                        _definition.GraphDomain,
                        message.SequenceId,
                        new { GraphId, NodeId = nodeId },
                        null));
                    continue;
                }

                IGraphNodeExecutor executor;
                try
                {
                    executor = _executors.Get(nodeDef.NodeType);
                }
                catch (Exception ex)
                {
                    _emitter.Emit(new BasicEventMessage(
                        "Graph.ExecutorMissing",
                        _definition.GraphDomain,
                        message.SequenceId,
                        new { GraphId, NodeType = nodeDef.NodeType, Error = ex.Message },
                        null));
                    continue;
                }

                try
                {
                    await executor.ExecuteAsync(_definition, nodeDef, _state, message, context, ct);
                }
                catch (Exception ex)
                {
                    _emitter.Emit(new BasicEventMessage(
                        "Graph.ExecutorError",
                        _definition.GraphDomain,
                        message.SequenceId,
                        new { GraphId, NodeId = nodeId, NodeType = nodeDef.NodeType, Error = ex.ToString() },
                        null));
                }
            }
        }

        public GraphStateSnapshot ExportState()
        {
            var snapshot = new GraphStateSnapshot
            {
                GraphId = GraphId,
                Data = new System.Collections.Generic.Dictionary<string, object>(_state.AsReadOnly())
            };

            if (_state.TryGet<long>(LastSeqKey, out var lastSeq))
                snapshot.LastSequenceId = lastSeq;

            return snapshot;
        }

        public void ImportState(GraphStateSnapshot snapshot)
        {
            if (snapshot == null || snapshot.GraphId != GraphId)
                return;

            foreach (var kv in snapshot.Data)
            {
                _state.Set(kv.Key, kv.Value);
            }

            _state.Set(LastSeqKey, snapshot.LastSequenceId);
        }

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

