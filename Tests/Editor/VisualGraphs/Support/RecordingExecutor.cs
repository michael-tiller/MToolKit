using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Tests.Editor.VisualGraphs.Support
{
  /// <summary>
  ///   Recording <see cref="IGraphNodeExecutor" />: captures every (nodeId, message) it executes so
  ///   tests can assert which nodes ran. Optionally enqueues continuation nodes or throws, to exercise
  ///   GraphRunner's continuation and error-isolation paths.
  /// </summary>
  public sealed class RecordingExecutor : IGraphNodeExecutor
  {
    public RecordingExecutor(string nodeType)
    {
      NodeType = nodeType;
    }

    public List<string> ExecutedNodeIds { get; } = new();
    public List<IGameMessage> ExecutedMessages { get; } = new();
    public int ExecuteCallCount => ExecutedNodeIds.Count;
    public bool Executed => ExecutedNodeIds.Count > 0;

    /// <summary>When true, Execute throws after recording (to pin GraphRunner's catch-and-continue).</summary>
    public bool ShouldThrow { get; set; }

    public string ExceptionMessage { get; set; } = "RecordingExecutor intentional failure";

    /// <summary>Hook invoked after recording, before any throw — use to context.EnqueueNext(...) in tests.</summary>
    public Action<RuntimeNodeDefinition, GraphNodeExecutionContext> OnExecute { get; set; }

    public string NodeType { get; }

    public UniTask Execute(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      ExecutedNodeIds.Add(node.NodeId);
      ExecutedMessages.Add(message);
      OnExecute?.Invoke(node, context);

      if (ShouldThrow)
        throw new InvalidOperationException(ExceptionMessage);

      return UniTask.CompletedTask;
    }
  }
}
