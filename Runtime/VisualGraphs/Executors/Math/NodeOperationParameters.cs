using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Variables;
using Serilog;

namespace MToolKit.Runtime.VisualGraphs.Executors.Math
{
  /// <summary>Parameter decoding for the state-key mediated core operations.</summary>
  internal static class NodeOperationParameters
  {
    internal static string Key(RuntimeNodeDefinition node, string name) =>
      node.Parameters.TryGetValue(name, out var value) ? value as string : null;

    internal static List<string> Keys(RuntimeNodeDefinition node) =>
      node.Parameters.TryGetValue("Keys", out var value) && value is IEnumerable<string> keys
        ? keys.ToList() : new List<string>();

    internal static T EnumValue<T>(RuntimeNodeDefinition node, string name, T fallback) where T : struct
    {
      if (!node.Parameters.TryGetValue(name, out var value)) return fallback;
      if (value is T typed) return typed;
      if (System.Enum.TryParse<T>(Convert.ToString(value, CultureInfo.InvariantCulture), true, out var parsed))
        return parsed;
      Warn(node, "Invalid " + name + "; using " + fallback);
      return fallback;
    }

    internal static EGraphVariableType ValueType(RuntimeNodeDefinition node,
      EGraphVariableType fallback = EGraphVariableType.Float) => EnumValue(node, "ValueType", fallback);

    internal static bool Flag(RuntimeNodeDefinition node, string name, bool fallback = false) =>
      node.Parameters.TryGetValue(name, out var value) && value is bool flag ? flag : fallback;

    internal static float Fallback(RuntimeNodeDefinition node)
    {
      if (!node.Parameters.TryGetValue("Fallback", out var value)) return 0f;
      if (value is float f) return f;
      if (value is int i) return i;
      if (value is GraphVariableDeclaration declaration)
      {
        var declared = declaration.GetDefaultValue();
        if (declared is float df) return df;
        if (declared is int di) return di;
      }
      if (value is string text && float.TryParse(text, NumberStyles.Float,
          CultureInfo.InvariantCulture, out var parsed)) return parsed;
      Warn(node, "Invalid numeric fallback; using zero");
      return 0f;
    }

    internal static T Read<T>(GraphNodeValues values, RuntimeNodeDefinition node, string key, T fallback = default)
    {
      if (values.TryGet<T>(key, out var value)) return value;
      Warn(node, "State key '" + key + "' is missing or not a " + typeof(T).Name + "; using fallback");
      return fallback;
    }

    internal static void Warn(RuntimeNodeDefinition node, string reason) =>
      Log.Logger.ForContext("Feature", "VisualGraphs.Core")
        .Warning("Graph node {NodeType} ({NodeId}): {Reason}", node.NodeType, node.NodeId, reason);

    internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    internal static float FiniteResult(RuntimeNodeDefinition node, float result)
    {
      if (IsFinite(result)) return result;
      Warn(node, "Non-finite numeric result; using fallback");
      var fallback = Fallback(node);
      return IsFinite(fallback) ? fallback : 0f;
    }

    internal static int IntResult(RuntimeNodeDefinition node, double value)
    {
      if (double.IsNaN(value) || double.IsInfinity(value))
      {
        Warn(node, "Non-finite integer result; using zero");
        return 0;
      }
      if (value > int.MaxValue || value < int.MinValue)
      {
        Warn(node, "Integer result exceeds range; clamping");
        return value > int.MaxValue ? int.MaxValue : int.MinValue;
      }
      return (int)value;
    }
  }
}
