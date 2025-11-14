using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Executors
{
  /// <summary>
  ///   Executor for GenericStateCheckNode.
  ///   Checks if a state key equals an expected value and branches execution.
  /// </summary>
  public sealed class GenericStateCheckNodeExecutor : IGraphNodeExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<GenericStateCheckNodeExecutor>().ForFeature("VisualGraphs.State"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public string NodeType => "GenericStateCheckNode";

    public UniTask ExecuteAsync(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      // Extract parameters
      var stateKey = node.Parameters.TryGetValue("stateKey", out var sk) ? sk as string : null;
      var expectedValue = node.Parameters.TryGetValue("expectedValue", out var ev) ? ev as string : null;
      var comparisonOperator = node.Parameters.TryGetValue("comparisonOperator", out var co) ? co as string : "Equals";
      var ignoreCase = node.Parameters.TryGetValue("ignoreCase", out var ic) && Convert.ToBoolean(ic);

      if (string.IsNullOrEmpty(stateKey))
      {
        log.ForMethod().Warning("GenericStateCheck: StateKey is empty, continuing to DoesntMatch");
        EnqueueOutputPort(graph, node, "DoesntMatch", context);
        return UniTask.CompletedTask;
      }

      // Try to get state value
      if (!state.TryGet<object>(stateKey, out var actualValue))
      {
        log.ForMethod().Debug("GenericStateCheck: State key '{StateKey}' not found, continuing to DoesntMatch", stateKey);
        EnqueueOutputPort(graph, node, "DoesntMatch", context);
        return UniTask.CompletedTask;
      }

      // Compare values
      bool matches = CompareValues(actualValue, expectedValue, comparisonOperator, ignoreCase);

      // Branch based on result
      var targetPort = matches ? "Matches" : "DoesntMatch";
      EnqueueOutputPort(graph, node, targetPort, context);

      return UniTask.CompletedTask;
    }

    private bool CompareValues(object actualValue, string expectedValue, string comparisonOperator, bool ignoreCase)
    {
      if (actualValue == null && string.IsNullOrEmpty(expectedValue))
        return comparisonOperator == "Equals" || comparisonOperator == "NotEquals";

      if (actualValue == null || expectedValue == null)
        return comparisonOperator == "NotEquals";

      var actualType = actualValue.GetType();

      try
      {
        // Handle string comparison
        if (actualType == typeof(string))
        {
          var actualStr = actualValue as string;
          var comparison = ignoreCase
            ? string.Compare(actualStr, expectedValue, StringComparison.OrdinalIgnoreCase)
            : string.Compare(actualStr, expectedValue, StringComparison.Ordinal);

          return EvaluateComparison(comparison, comparisonOperator);
        }

        // Handle numeric comparisons
        if (IsNumericType(actualType))
        {
          var actualNum = Convert.ToDouble(actualValue);
          var expectedNum = Convert.ToDouble(expectedValue);

          return comparisonOperator switch
          {
            "Equals" => Math.Abs(actualNum - expectedNum) < 0.0001,
            "NotEquals" => Math.Abs(actualNum - expectedNum) >= 0.0001,
            "GreaterThan" => actualNum > expectedNum,
            "LessThan" => actualNum < expectedNum,
            "GreaterThanOrEqual" => actualNum >= expectedNum,
            "LessThanOrEqual" => actualNum <= expectedNum,
            _ => Math.Abs(actualNum - expectedNum) < 0.0001
          };
        }

        // Handle bool comparison
        if (actualType == typeof(bool))
        {
          var actualBool = (bool)actualValue;
          var expectedBool = Convert.ToBoolean(expectedValue);

          return comparisonOperator switch
          {
            "Equals" => actualBool == expectedBool,
            "NotEquals" => actualBool != expectedBool,
            _ => actualBool == expectedBool
          };
        }

        // Handle enum comparison
        if (actualType.IsEnum)
        {
          var enumValue = Enum.Parse(actualType, expectedValue, ignoreCase);
          var equals = actualValue.Equals(enumValue);

          return comparisonOperator switch
          {
            "Equals" => equals,
            "NotEquals" => !equals,
            _ => equals
          };
        }

        // Fallback: ToString comparison
        var actualStr2 = actualValue.ToString();
        var comparison2 = ignoreCase
          ? string.Compare(actualStr2, expectedValue, StringComparison.OrdinalIgnoreCase)
          : string.Compare(actualStr2, expectedValue, StringComparison.Ordinal);

        return EvaluateComparison(comparison2, comparisonOperator);
      }
      catch (Exception ex)
      {
        log.ForMethod().Warning(ex, "GenericStateCheck: Failed to compare values: {ActualValue} ({ActualType}) vs {ExpectedValue}",
          actualValue, actualType.Name, expectedValue);
        return false;
      }
    }

    private bool IsNumericType(Type type)
    {
      return type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
             type == typeof(float) || type == typeof(double) || type == typeof(decimal) ||
             type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte);
    }

    private bool EvaluateComparison(int comparison, string comparisonOperator)
    {
      return comparisonOperator switch
      {
        "Equals" => comparison == 0,
        "NotEquals" => comparison != 0,
        "GreaterThan" => comparison > 0,
        "LessThan" => comparison < 0,
        "GreaterThanOrEqual" => comparison >= 0,
        "LessThanOrEqual" => comparison <= 0,
        _ => comparison == 0
      };
    }

    private void EnqueueOutputPort(IRuntimeGraphDefinition graph, RuntimeNodeDefinition node, string portName, GraphNodeExecutionContext context)
    {
      var connections = graph.GetConnectionsFrom(node.NodeId)
        .Where(c => c.PortName == portName);

      foreach (var connection in connections)
        context.EnqueueNext(connection.ToNodeId);
    }
  }
}

