using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Executors.Transform
{
  /// <summary>Executes ExtractValueNode through typed, scoped graph values.</summary>
  public sealed class ExtractValueNodeExecutor : IGraphNodeExecutor
  {
    public string NodeType => "ExtractValueNode";

    public UniTask Execute(IRuntimeGraphDefinition graph, RuntimeNodeDefinition node, IGraphState state,
      IGameMessage message, GraphNodeExecutionContext context, CancellationToken ct = default)
    {
      TransformNodeOperations.Execute(node, new GraphNodeValues(graph, state, context), "ExtractValue");
      return UniTask.CompletedTask;
    }
  }
}
