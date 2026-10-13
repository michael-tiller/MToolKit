using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Executors.Logic
{
  /// <summary>Executes AnyTrueNode through typed, scoped graph values.</summary>
  public sealed class AnyTrueNodeExecutor : IGraphNodeExecutor
  {
    public string NodeType => "AnyTrueNode";

    public UniTask Execute(IRuntimeGraphDefinition graph, RuntimeNodeDefinition node, IGraphState state,
      IGameMessage message, GraphNodeExecutionContext context, CancellationToken ct = default)
    {
      LogicNodeOperations.Execute(node, new GraphNodeValues(graph, state, context), "AnyTrue");
      return UniTask.CompletedTask;
    }
  }
}
