using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Executors.Math
{
  /// <summary>Executes SubtractNode through typed, scoped graph values.</summary>
  public sealed class SubtractNodeExecutor : IGraphNodeExecutor
  {
    public string NodeType => "SubtractNode";

    public UniTask Execute(IRuntimeGraphDefinition graph, RuntimeNodeDefinition node, IGraphState state,
      IGameMessage message, GraphNodeExecutionContext context, CancellationToken ct = default)
    {
      MathNodeOperations.Execute(node, new GraphNodeValues(graph, state, context), "Subtract");
      return UniTask.CompletedTask;
    }
  }
}
