using System;
using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Variables
{
  /// <summary>
  ///   Scriptable object containing a set of graph variable declarations.
  ///   Can be used for global variables, per-definition initial variables, or as a graph asset's
  ///   declared-variables block (export validation + authoring tooling).
  /// </summary>
  [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Variable Set", fileName = "GraphVariables", order = 200)]
  public sealed class GraphVariableSet : ScriptableObject
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<GraphVariableSet>().ForFeature("VisualGraphs.State"));

    private static ILogger log => logLazy.Value ?? Logger.None;

    [ListDrawerSettings(ShowFoldout = true, DraggableItems = true)]
    public List<GraphVariableDeclaration> entries = new();

    /// <summary>
    ///   Apply the declared defaults of these variables to a graph state.
    ///   Entries that are null, have empty keys, or carry an out-of-range serialized type are
    ///   skipped (with a warning for the latter) — this runs during graph init and must not
    ///   crash on corrupt authoring data; export validation is where such data fails loud.
    /// </summary>
    public void ApplyTo(IGraphState state)
    {
      if (state == null || entries == null) return;

      foreach (var entry in entries)
      {
        if (entry == null || string.IsNullOrEmpty(entry.key)) continue;

        switch (entry.type)
        {
          case EGraphVariableType.String:
            state.Set(entry.key, entry.stringValue);
            break;
          case EGraphVariableType.Int:
            state.Set(entry.key, entry.intValue);
            break;
          case EGraphVariableType.Float:
            state.Set(entry.key, entry.floatValue);
            break;
          case EGraphVariableType.Bool:
            state.Set(entry.key, entry.boolValue);
            break;
          case EGraphVariableType.Vector3:
            state.Set(entry.key, entry.vector3Value);
            break;
          case EGraphVariableType.Vector2:
            state.Set(entry.key, entry.vector2Value);
            break;
          case EGraphVariableType.Color:
            state.Set(entry.key, entry.colorValue);
            break;
          default:
            log.Warning("Skipping variable '{Key}' with unsupported serialized type value {TypeValue}",
              entry.key, (int)entry.type);
            break;
        }
      }
    }

    /// <summary>
    ///   Find the declaration for a key. Exact ordinal match, no trimming; null when absent.
    ///   First match wins — duplicate keys are rejected by export validation for declared graphs.
    /// </summary>
    public GraphVariableDeclaration Find(string key)
    {
      if (string.IsNullOrEmpty(key) || entries == null) return null;

      foreach (var entry in entries)
      {
        if (entry == null || string.IsNullOrEmpty(entry.key)) continue;
        if (string.Equals(entry.key, key, StringComparison.Ordinal)) return entry;
      }

      return null;
    }
  }
}
