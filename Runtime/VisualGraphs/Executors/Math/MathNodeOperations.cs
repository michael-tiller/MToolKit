using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Variables;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Executors.Math
{
  /// <summary>Typed arithmetic with compatible float defaults and scoped state access.</summary>
  internal static class MathNodeOperations
  {
    internal static void Execute(RuntimeNodeDefinition node, GraphNodeValues values, string operation)
    {
      var type = NodeOperationParameters.ValueType(node);
      if (type != EGraphVariableType.Float && type != EGraphVariableType.Int)
      {
        NodeOperationParameters.Warn(node, "Arithmetic requires Int or Float; output left untouched");
        values.Continue(node);
        return;
      }
      if (type == EGraphVariableType.Int)
        values.Set(NodeOperationParameters.Key(node, "ResultKey"), ExecuteInt(node, values, operation));
      else
        values.Set(NodeOperationParameters.Key(node, "ResultKey"),
          NodeOperationParameters.FiniteResult(node, ExecuteFloat(node, values, operation)));
      values.Continue(node);
    }

    private static float Float(GraphNodeValues values, RuntimeNodeDefinition node, string field) =>
      NodeOperationParameters.Read<float>(values, node, NodeOperationParameters.Key(node, field));

    private static int Int(GraphNodeValues values, RuntimeNodeDefinition node, string field) =>
      NodeOperationParameters.Read<int>(values, node, NodeOperationParameters.Key(node, field));

    private static float ExecuteFloat(RuntimeNodeDefinition node, GraphNodeValues values, string operation)
    {
      if (operation == "Clamp")
        return Mathf.Clamp(Float(values, node, "ValueKey"), Float(values, node, "MinKey"),
          Float(values, node, "MaxKey"));
      var a = Float(values, node, "AKey");
      if (operation.StartsWith("Conditional") && !NodeOperationParameters.Read<bool>(values, node,
          NodeOperationParameters.Key(node, "ConditionKey"))) return a;
      var b = Float(values, node, "BKey");
      switch (operation)
      {
        case "Add": case "ConditionalAdd": return a + b;
        case "Subtract": return a - b;
        case "Multiply": case "ConditionalMultiply": return a * b;
        case "Divide":
          if (b != 0f) return a / b;
          NodeOperationParameters.Warn(node, "Division by zero; using fallback");
          return NodeOperationParameters.Fallback(node);
        case "Lerp": return Mathf.Lerp(a, b, Float(values, node, "TKey"));
        default: return 0f;
      }
    }

    private static int ExecuteInt(RuntimeNodeDefinition node, GraphNodeValues values, string operation)
    {
      if (operation == "Clamp")
      {
        var value = Int(values, node, "ValueKey");
        var min = Int(values, node, "MinKey");
        var max = Int(values, node, "MaxKey");
        return value < min ? min : value > max ? max : value;
      }
      var a = Int(values, node, "AKey");
      if (operation.StartsWith("Conditional") && !NodeOperationParameters.Read<bool>(values, node,
          NodeOperationParameters.Key(node, "ConditionKey"))) return a;
      var b = Int(values, node, "BKey");
      double result;
      switch (operation)
      {
        case "Add": case "ConditionalAdd": result = (long)a + b; break;
        case "Subtract": result = (long)a - b; break;
        case "Multiply": case "ConditionalMultiply": result = (long)a * b; break;
        case "Divide":
          if (b == 0)
          {
            NodeOperationParameters.Warn(node, "Division by zero; using fallback");
            result = NodeOperationParameters.Fallback(node);
          }
          else result = (long)a / b;
          break;
        case "Lerp":
          // T is always Float; integer output truncates toward zero.
          result = a + ((double)b - a) * Mathf.Clamp01(Float(values, node, "TKey")); break;
        default: result = 0; break;
      }
      return NodeOperationParameters.IntResult(node, result);
    }
  }
}
