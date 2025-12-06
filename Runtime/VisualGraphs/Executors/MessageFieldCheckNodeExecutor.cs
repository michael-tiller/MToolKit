using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Executors
{
  /// <summary>
  ///   Executor for MessageFieldCheckNode.
  ///   Uses reflection to check message field values and branches execution.
  /// </summary>
  public sealed class MessageFieldCheckNodeExecutor : IGraphNodeExecutor
  {
    public string NodeType => "MessageFieldCheckNode";

    public UniTask Execute(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      if (message == null)
      {
        // No message, continue to DoesntMatch
        EnqueueOutputPort(graph, node, "DoesntMatch", context);
        return UniTask.CompletedTask;
      }

      // Extract parameters (try both camelCase and PascalCase for compatibility)
      var fieldName = node.Parameters.TryGetValue("fieldName", out var fn) ? fn as string : null;
      if (string.IsNullOrEmpty(fieldName))
        fieldName = node.Parameters.TryGetValue("FieldName", out fn) ? fn as string : null;

      var expectedValue = node.Parameters.TryGetValue("expectedValue", out var ev) ? ev as string : null;
      if (string.IsNullOrEmpty(expectedValue))
        expectedValue = node.Parameters.TryGetValue("ExpectedValue", out ev) ? ev as string : null;

      var ignoreCase = node.Parameters.TryGetValue("ignoreCase", out var ic) && Convert.ToBoolean(ic);
      if (!ignoreCase)
        ignoreCase = node.Parameters.TryGetValue("IgnoreCase", out ic) && Convert.ToBoolean(ic);

      if (string.IsNullOrEmpty(fieldName))
      {
        UnityEngine.Debug.LogWarning("[MessageFieldCheck] FieldName is empty");
        EnqueueOutputPort(graph, node, "DoesntMatch", context);
        return UniTask.CompletedTask;
      }

      // Use reflection to get field/property value
      var messageType = message.GetType();
      var field = messageType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
      var property = messageType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);

      object actualValue = null;
      if (field != null)
      {
        actualValue = field.GetValue(message);
      }
      else if (property != null)
      {
        actualValue = property.GetValue(message);
      }
      else
      {
        UnityEngine.Debug.LogWarning($"[MessageFieldCheck] Field/Property '{fieldName}' not found on message type '{messageType.Name}'");
        EnqueueOutputPort(graph, node, "DoesntMatch", context);
        return UniTask.CompletedTask;
      }

      // Compare values
      bool matches = CompareValues(actualValue, expectedValue, ignoreCase);

      // Branch based on result
      var targetPort = matches ? "Matches" : "DoesntMatch";
      EnqueueOutputPort(graph, node, targetPort, context);

      return UniTask.CompletedTask;
    }

    private bool CompareValues(object actualValue, string expectedValue, bool ignoreCase)
    {
      if (actualValue == null && string.IsNullOrEmpty(expectedValue))
        return true;

      if (actualValue == null || expectedValue == null)
        return false;

      // Convert expected value to actual type
      var actualType = actualValue.GetType();

      try
      {
        // String comparison
        if (actualType == typeof(string))
        {
          var actualStr = actualValue as string;
          return ignoreCase
            ? string.Equals(actualStr, expectedValue, StringComparison.OrdinalIgnoreCase)
            : string.Equals(actualStr, expectedValue, StringComparison.Ordinal);
        }

        // Enum comparison
        if (actualType.IsEnum)
        {
          var enumValue = Enum.Parse(actualType, expectedValue, ignoreCase);
          return actualValue.Equals(enumValue);
        }

        // Numeric comparisons
        if (actualType == typeof(int) || actualType == typeof(long) || actualType == typeof(short) || actualType == typeof(byte))
        {
          var expectedInt = Convert.ToInt64(expectedValue);
          var actualInt = Convert.ToInt64(actualValue);
          return actualInt == expectedInt;
        }

        if (actualType == typeof(float) || actualType == typeof(double))
        {
          var expectedFloat = Convert.ToDouble(expectedValue);
          var actualFloat = Convert.ToDouble(actualValue);
          return Math.Abs(actualFloat - expectedFloat) < 0.0001;
        }

        if (actualType == typeof(bool))
        {
          var expectedBool = Convert.ToBoolean(expectedValue);
          var actualBool = (bool)actualValue;
          return actualBool == expectedBool;
        }

        // Fallback: ToString comparison
        return actualValue.ToString().Equals(expectedValue, ignoreCase
          ? StringComparison.OrdinalIgnoreCase
          : StringComparison.Ordinal);
      }
      catch (Exception ex)
      {
        UnityEngine.Debug.LogWarning($"[MessageFieldCheck] Failed to compare values: {ex.Message}");
        return false;
      }
    }

    private void EnqueueOutputPort(IRuntimeGraphDefinition graph, RuntimeNodeDefinition node, string portName, GraphNodeExecutionContext context)
    {
      // Filter connections manually to avoid LINQ allocation in hot path
      foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
      {
        if (connection.PortName == portName)
          context.EnqueueNext(connection.ToNodeId);
      }
    }
  }
}

