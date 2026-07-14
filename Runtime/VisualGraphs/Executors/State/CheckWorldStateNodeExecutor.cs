using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Contexts;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Variables;
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Executors.State
{
  /// <summary>
  ///   Executor for CheckWorldStateNode. Resolves Key through <see cref="ScopedKeyResolver" /> (same
  ///   bare/scoped wiring as <see cref="GetVarNodeExecutor" />, read-only — no ResultKey, no emit) and
  ///   compares the resolved value against <see cref="GraphVariableDeclaration.ExpectedValue" />'s typed
  ///   literal, branching Matches/DoesntMatch.
  /// </summary>
  public sealed class CheckWorldStateNodeExecutor : IGraphNodeExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<CheckWorldStateNodeExecutor>().ForFeature("VisualGraphs.State"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private readonly ScopedKeyResolver resolver;

    public CheckWorldStateNodeExecutor(ScopedKeyResolver resolver)
    {
      this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public string NodeType => "CheckWorldStateNode";

    public UniTask Execute(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      var key = node.Parameters.TryGetValue("Key", out var k) ? k as string : null;
      var comparisonOperator = node.Parameters.TryGetValue("ComparisonOperator", out var co) ? co as string : "Equals";
      var expectedDecl = node.Parameters.TryGetValue("ExpectedValue", out var ev) ? ev as GraphVariableDeclaration : null;

      var actualValue = Resolve(key, state);
      var matches = expectedDecl != null && Compare(actualValue, expectedDecl, comparisonOperator);

      EnqueueOutputPort(graph, node, matches ? "Matches" : "DoesntMatch", context);

      return UniTask.CompletedTask;
    }

    private object Resolve(string key, IGraphState state)
    {
      if (string.IsNullOrEmpty(key))
      {
        log.ForMethod().Warning("CheckWorldState: Key is empty, resolving to null (DoesntMatch)");
        return null;
      }

      var parsed = ScopedKeyResolver.Parse(key);
      if (parsed.IsLocal)
        return state.TryGet<object>(key, out var local) ? local : null;

      return resolver.Get<object>(key, null);
    }

    private bool Compare(object actualValue, GraphVariableDeclaration expected, string comparisonOperator)
    {
      // A legally-stored null (e.g. a declared-String key set to null, per VariableStorage's own rules) has
      // no runtime type — checked BEFORE GetType() for every operator, including Equals/NotEquals, so a
      // type mismatch never silently reads as "not equal" (a naive !matches would).
      if (actualValue == null)
      {
        log.ForMethod().Warning("CheckWorldState: resolved value is null, treated as a type mismatch (DoesntMatch)");
        return false;
      }

      if (actualValue.GetType() != expected.GetValueType())
      {
        log.ForMethod().Warning("CheckWorldState: resolved value type {ActualType} does not match ExpectedValue.type {ExpectedType} (DoesntMatch)",
          actualValue.GetType().Name, expected.type);
        return false;
      }

      var expectedValue = expected.GetDefaultValue();

      switch (comparisonOperator)
      {
        case "Equals":
          return actualValue.Equals(expectedValue);
        case "NotEquals":
          return !actualValue.Equals(expectedValue);
        case "GreaterThan":
        case "LessThan":
        case "GreaterThanOrEqual":
        case "LessThanOrEqual":
          if (expected.type != EGraphVariableType.Int && expected.type != EGraphVariableType.Float)
          {
            log.ForMethod().Warning("CheckWorldState: ordering operator '{Operator}' is invalid for type {Type} (DoesntMatch)",
              comparisonOperator, expected.type);
            return false;
          }

          var actualNum = Convert.ToDouble(actualValue);
          var expectedNum = Convert.ToDouble(expectedValue);
          return comparisonOperator switch
          {
            "GreaterThan" => actualNum > expectedNum,
            "LessThan" => actualNum < expectedNum,
            "GreaterThanOrEqual" => actualNum >= expectedNum,
            "LessThanOrEqual" => actualNum <= expectedNum,
            _ => false
          };
        default:
          log.ForMethod().Warning("CheckWorldState: unknown comparison operator '{Operator}' (DoesntMatch)", comparisonOperator);
          return false;
      }
    }

    private void EnqueueOutputPort(IRuntimeGraphDefinition graph, RuntimeNodeDefinition node, string portName, GraphNodeExecutionContext context)
    {
      foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
        if (connection.PortName == portName)
          context.EnqueueNext(connection.ToNodeId);
    }
  }
}
