using MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Logic;
using MToolKit.Runtime.VisualGraphs.Executors.Math;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Variables;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Executors.Logic
{
  internal static class LogicNodeOperations
  {
    internal static void Execute(RuntimeNodeDefinition node, GraphNodeValues values, string operation)
    {
      var resultKey = NodeOperationParameters.Key(node, "ResultKey");
      if (operation == "Select")
      {
        Select(node, values, resultKey);
        values.Continue(node);
        return;
      }

      bool result;
      switch (operation)
      {
        case "Compare": result = Compare(node, values); break;
        case "Not":
          result = !NodeOperationParameters.Read<bool>(values, node, NodeOperationParameters.Key(node, "Key")); break;
        case "Xor":
          result = NodeOperationParameters.Read<bool>(values, node, NodeOperationParameters.Key(node, "LeftKey")) ^
            NodeOperationParameters.Read<bool>(values, node, NodeOperationParameters.Key(node, "RightKey")); break;
        default:
          var all = operation == "And" || operation == "AllTrue";
          result = all;
          foreach (var key in NodeOperationParameters.Keys(node))
          {
            if (values.TryGet<bool>(key, out var value)) result = all ? result && value : result || value;
            else
            {
              NodeOperationParameters.Warn(node, "State key '" + key + "' is missing or not Bool");
              // Preserve legacy And/Or skip semantics. Explicit AllTrue treats missing as false.
              if (operation == "AllTrue") result = false;
            }
          }
          break;
      }
      values.Set(resultKey, result);
      values.Continue(node);
    }

    private static bool Compare(RuntimeNodeDefinition node, GraphNodeValues values)
    {
      var type = NodeOperationParameters.ValueType(node);
      var operation = NodeOperationParameters.EnumValue(node, "Operator", EGraphComparisonOperator.GreaterThan);
      var aKey = NodeOperationParameters.Key(node, "AKey");
      var bKey = NodeOperationParameters.Key(node, "BKey");
      if (type == EGraphVariableType.Bool)
      {
        if (operation != EGraphComparisonOperator.Equal && operation != EGraphComparisonOperator.NotEqual)
        {
          NodeOperationParameters.Warn(node, "Bool supports Equal and NotEqual only");
          return false;
        }
        if (values.TryGet<bool>(aKey, out var a) && values.TryGet<bool>(bKey, out var b))
          return operation == EGraphComparisonOperator.Equal ? a == b : a != b;
      }
      else if (type == EGraphVariableType.Int)
      {
        if (values.TryGet<int>(aKey, out var a) && values.TryGet<int>(bKey, out var b))
          return CompareNumbers(a, b, operation);
      }
      else if (type == EGraphVariableType.Float)
      {
        if (values.TryGet<float>(aKey, out var a) && values.TryGet<float>(bKey, out var b))
          return CompareNumbers(a, b, operation);
      }
      NodeOperationParameters.Warn(node, "Compare operands missing, wrong-typed, or unsupported; using false");
      return false;
    }

    private static bool CompareNumbers(double a, double b, EGraphComparisonOperator operation)
    {
      switch (operation)
      {
        case EGraphComparisonOperator.GreaterThan: return a > b;
        case EGraphComparisonOperator.GreaterOrEqual: return a >= b;
        case EGraphComparisonOperator.LessThan: return a < b;
        case EGraphComparisonOperator.LessOrEqual: return a <= b;
        case EGraphComparisonOperator.Equal: return a == b;
        case EGraphComparisonOperator.NotEqual: return a != b;
        default: return false;
      }
    }

    private static void Select(RuntimeNodeDefinition node, GraphNodeValues values, string resultKey)
    {
      var condition = NodeOperationParameters.Read<bool>(values, node, NodeOperationParameters.Key(node, "ConditionKey"));
      var source = NodeOperationParameters.Key(node, condition ? "TrueKey" : "FalseKey");
      switch (NodeOperationParameters.ValueType(node))
      {
        case EGraphVariableType.String: values.Set(resultKey, NodeOperationParameters.Read(values, node, source, "")); break;
        case EGraphVariableType.Int: values.Set(resultKey, NodeOperationParameters.Read<int>(values, node, source)); break;
        case EGraphVariableType.Float: values.Set(resultKey, NodeOperationParameters.Read<float>(values, node, source)); break;
        case EGraphVariableType.Bool: values.Set(resultKey, NodeOperationParameters.Read<bool>(values, node, source)); break;
        case EGraphVariableType.Vector2: values.Set(resultKey, NodeOperationParameters.Read<Vector2>(values, node, source)); break;
        case EGraphVariableType.Vector3: values.Set(resultKey, NodeOperationParameters.Read<Vector3>(values, node, source)); break;
        case EGraphVariableType.Color: values.Set(resultKey, NodeOperationParameters.Read<Color>(values, node, source)); break;
        default: NodeOperationParameters.Warn(node, "Unsupported selection type; output left untouched"); break;
      }
    }
  }
}
