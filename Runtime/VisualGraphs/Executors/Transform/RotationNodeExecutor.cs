using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Executors.Transform
{
  /// <summary>Executes RotationNode through typed, scoped graph values.</summary>
  public sealed class RotationNodeExecutor : IGraphNodeExecutor
  {
    public string NodeType => "RotationNode";

    public UniTask Execute(IRuntimeGraphDefinition graph, RuntimeNodeDefinition node, IGraphState state,
      IGameMessage message, GraphNodeExecutionContext context, CancellationToken ct = default)
    {
      TransformNodeOperations.Execute(node, new GraphNodeValues(graph, state, context), "Rotation");
      return UniTask.CompletedTask;
    }
  }
}