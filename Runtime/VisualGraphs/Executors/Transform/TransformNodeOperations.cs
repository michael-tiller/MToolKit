using System;
using System.Globalization;
using System.Linq;
using MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Transform;
using MToolKit.Runtime.VisualGraphs.Executors.Math;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Variables;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Executors.Transform
{
  internal static class TransformNodeOperations
  {
    internal static void Execute(RuntimeNodeDefinition node, GraphNodeValues values, string operation)
    {
      var resultKey = NodeOperationParameters.Key(node, "ResultKey");
      switch (operation)
      {
        case "MapValue": Map(node, values, resultKey); break;
        case "CombineValues": Combine(node, values, resultKey); break;
        case "ExtractValue": values.Set(resultKey, Extract(node, values)); break;
        case "FormatString": values.Set(resultKey, Format(node, values)); break;
        default:
          values.Set(NodeOperationParameters.Key(node, "DestinationKey"),
            NodeOperationParameters.Read<Vector3>(values, node, NodeOperationParameters.Key(node, "SourceKey")));
          break;
      }
      values.Continue(node);
    }

    private static void Map(RuntimeNodeDefinition node, GraphNodeValues values, string resultKey)
    {
      var type = NodeOperationParameters.ValueType(node);
      if (type != EGraphVariableType.Int && type != EGraphVariableType.Float)
      {
        NodeOperationParameters.Warn(node, "Range mapping requires Int or Float; output left untouched");
        return;
      }
      double Read(string field) => type == EGraphVariableType.Int
        ? (double)NodeOperationParameters.Read<int>(values, node, NodeOperationParameters.Key(node, field))
        : NodeOperationParameters.Read<float>(values, node, NodeOperationParameters.Key(node, field));
      var value = Read("ValueKey");
      var min = Read("InputMinKey");
      var max = Read("InputMaxKey");
      var low = Read("OutputMinKey");
      var high = Read("OutputMaxKey");
      double result;
      if (max == min)
      {
        NodeOperationParameters.Warn(node, "Input range has zero width; using fallback");
        result = NodeOperationParameters.Fallback(node);
      }
      else
      {
        var t = (value - min) / (max - min);
        if (NodeOperationParameters.Flag(node, "ClampOutput")) t = System.Math.Max(0d, System.Math.Min(1d, t));
        result = low + (high - low) * t;
      }
      if (type == EGraphVariableType.Int) values.Set(resultKey, NodeOperationParameters.IntResult(node, result));
      else values.Set(resultKey, NodeOperationParameters.FiniteResult(node, (float)result));
    }

    private static void Combine(RuntimeNodeDefinition node, GraphNodeValues values, string resultKey)
    {
      var keys = NodeOperationParameters.Keys(node);
      float Component(int index, float fallback = 0f) => index < keys.Count
        ? NodeOperationParameters.Read(values, node, keys[index], fallback) : fallback;
      switch (NodeOperationParameters.ValueType(node, EGraphVariableType.Vector3))
      {
        case EGraphVariableType.Vector2: values.Set(resultKey, new Vector2(Component(0), Component(1))); break;
        case EGraphVariableType.Vector3: values.Set(resultKey, new Vector3(Component(0), Component(1), Component(2))); break;
        case EGraphVariableType.Color: values.Set(resultKey, new Color(Component(0), Component(1), Component(2), Component(3, 1f))); break;
        default: NodeOperationParameters.Warn(node, "Combination requires Vector2, Vector3 or Color; output left untouched"); break;
      }
    }

    private static float Extract(RuntimeNodeDefinition node, GraphNodeValues values)
    {
      var source = NodeOperationParameters.Key(node, "SourceKey");
      var component = (int)NodeOperationParameters.EnumValue(node, "Component", EGraphValueComponent.X);
      switch (NodeOperationParameters.ValueType(node, EGraphVariableType.Vector3))
      {
        case EGraphVariableType.Vector2:
          if (component >= 0 && component < 2) return NodeOperationParameters.Read<Vector2>(values, node, source)[component];
          break;
        case EGraphVariableType.Vector3:
          if (component >= 0 && component < 3) return NodeOperationParameters.Read<Vector3>(values, node, source)[component];
          break;
        case EGraphVariableType.Color:
          if (component >= 0 && component < 4) return NodeOperationParameters.Read<Color>(values, node, source)[component];
          break;
      }
      NodeOperationParameters.Warn(node, "Invalid component or extraction type; using zero");
      return 0f;
    }

    private static string Format(RuntimeNodeDefinition node, GraphNodeValues values)
    {
      var format = NodeOperationParameters.Key(node, "Format") ?? "";
      var arguments = NodeOperationParameters.Keys(node).Select(key =>
      {
        if (values.Contains(key)) return values.GetObject(key);
        NodeOperationParameters.Warn(node, "Format argument key '" + key + "' is missing; using empty text");
        return (object)"";
      }).ToArray();
      try { return string.Format(CultureInfo.InvariantCulture, format, arguments); }
      catch (FormatException)
      {
        NodeOperationParameters.Warn(node, "Malformed composite format or argument index; using empty text");
        return "";
      }
    }
  }
}
