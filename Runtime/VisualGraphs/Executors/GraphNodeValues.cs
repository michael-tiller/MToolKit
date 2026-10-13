using System;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Contexts;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.Messages;
using Serilog;

namespace MToolKit.Runtime.VisualGraphs.Executors
{
    /// <summary>Routes executor reads and writes through the existing typed scoped storage.</summary>
    internal sealed class GraphNodeValues
    {
        private readonly IRuntimeGraphDefinition graph;
        private readonly GraphNodeExecutionContext execution;
        private readonly IGraphContext local;
        private readonly ScopedKeyResolver resolver;

        /// <summary>Uses the registered owner context; standalone runners retain local-state fallback.</summary>
        public GraphNodeValues(IRuntimeGraphDefinition graph, IGraphState state, GraphNodeExecutionContext context)
        {
            this.graph = graph;
            execution = context;
            var registry = TryResolve<GraphContextRegistry>(context);
            resolver = TryResolve<ScopedKeyResolver>(context);
            if (resolver == null && registry != null) resolver = new ScopedKeyResolver(registry);
            if (registry != null && graph.GraphDomain == "Rule")
                local = registry.GetOrCreate(EGraphContextScope.World, null);
            else if (registry != null && registry.TryGet(EGraphContextScope.Graph, graph.GraphId, out var registered))
                local = registered;
            else
            {
                Log.Logger.Warning("Graph '{GraphId}' has no registered context; using its local state", graph.GraphId);
                local = new GraphContext(EGraphContextScope.Graph, graph.GraphId, state, new Emitter(context));
            }
        }

        /// <summary>Reads an exact typed value or a declared default, returning the caller fallback on a miss.</summary>
        public T Get<T>(string key, T fallback = default) => TryGet<T>(key, out var value) ? value : fallback;

        /// <summary>Tests the resolved value's runtime type without coercion or mutation.</summary>
        public bool TryGet<T>(string key, out T value)
        {
            value = default;
            if (!Resolve(key, out var storage, out var bare) || !storage.Contains(bare)) return false;
            var raw = storage.Get<object>(bare);
            if (raw is T typed) { value = typed; return true; }
            if (raw == null && !typeof(T).IsValueType) return true;
            return false;
        }

        /// <summary>Reads a boxed value, including a declared default; a missing value returns null.</summary>
        public object GetObject(string key) => Get<object>(key);

        /// <summary>Reads exported PascalCase fields while retaining legacy lowercase dictionaries.</summary>
        public static object Parameter(RuntimeNodeDefinition node, string name, string legacyName, object fallback = null)
        {
            if (node.Parameters.TryGetValue(name, out var value)) return value;
            return node.Parameters.TryGetValue(legacyName, out value) ? value : fallback;
        }

        /// <summary>Includes declarations in existence checks, even when the stored value is false or null.</summary>
        public bool Contains(string key) => Resolve(key, out var storage, out var bare) && storage.Contains(bare);

        /// <summary>Writes through declared-type enforcement and emits a change only when the write was accepted.</summary>
        public void Set<T>(string key, T value)
        {
            if (!Resolve(key, out var storage, out var bare)) return;
            var old = storage.Get<object>(bare);
            storage.Set(bare, value);
            var actual = storage.Get<object>(bare);
            if (!storage.Contains(bare) || !Equals(actual, value)) return;
            execution.Emit(new GraphStateChangedMessage(graph.GraphId, key, old, actual));
        }

        /// <summary>Enqueues the current node's outgoing execution connections.</summary>
        public void Continue(RuntimeNodeDefinition node)
        {
            foreach (var connection in graph.GetConnectionsFrom(node.NodeId)) execution.EnqueueNext(connection.ToNodeId);
        }

        private bool Resolve(string key, out Variables.IVariableStorage storage, out string bare)
        {
            storage = null; bare = null;
            if (string.IsNullOrEmpty(key)) return false;
            var parsed = ScopedKeyResolver.Parse(key);
            if (parsed.IsLocal) { storage = local.Variables; bare = parsed.Key; return true; }
            if (resolver != null && resolver.TryResolveStorage(key, local, out storage, out bare)) return true;
            Log.Logger.Warning("Scoped key '{Key}' has no available context; operation ignored", key);
            return false;
        }

        private static T TryResolve<T>(GraphNodeExecutionContext context) where T : class
        {
            try { return context.Resolve<T>(); }
            catch (InvalidOperationException) { return null; }
            catch (VContainer.VContainerException) { return null; }
        }

        private sealed class Emitter : IEventEmitter
        {
            private readonly GraphNodeExecutionContext context;
            public Emitter(GraphNodeExecutionContext context) => this.context = context;
            public void Emit(IGameMessage message, string domain = null) => context.Emit(message, domain);
        }
    }
}
