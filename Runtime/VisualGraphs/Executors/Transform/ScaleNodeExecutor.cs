using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Executors.Transform
{
  /// <summary>Executes ScaleNode through typed, scoped graph values.</summary>
  public sealed class ScaleNodeExecutor : IGraphNodeExecutor
  {
    public string NodeType => "ScaleNode";

    public UniTask Execute(IRuntimeGraphDefinition graph, RuntimeNodeDefinition node, IGraphState state,
      IGameMessage message, GraphNodeExecutionContext context, CancellationToken ct = default)
    {
      TransformNodeOperations.Execute(node, new GraphNodeValues(graph, state, context), "Scale");
      return UniTask.CompletedTask;
    }
  }
}