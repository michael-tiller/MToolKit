#if UNITY_EDITOR
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MToolKit.Runtime.VisualGraphs.Runtime.Debug;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Editor.VisualGraphs
{
  /// <summary>
  ///   Editor-only debug state that tracks runtime graph execution.
  ///   Listens to runtime debug events and maintains state for editor tools.
  /// </summary>
  [InitializeOnLoad]
  public static class XNodeDebugState
  {
    private static readonly Dictionary<string, string> _lastNodePerGraph = new();
    private static readonly Dictionary<string, ActiveGraphInfo> _activeGraphs = new();
    private static readonly List<NodeExecutionRecord> _executionHistory = new();
    private static readonly List<StateChangeRecord> _stateChangeHistory = new();
    private static readonly Dictionary<string, GraphExecutionStats> _graphStats = new();

    private const int MaxHistorySize = 1000;
    private const int MaxStateChangeHistorySize = 500;

    static XNodeDebugState()
    {
      NodeDebugEvents.NodeExecuted += OnNodeExecuted;
      NodeDebugEvents.StateChanged += OnStateChanged;
      NodeDebugEvents.GraphExecutionChanged += OnGraphExecutionChanged;

      EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
      if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.ExitingPlayMode)
      {
        // Clear state when exiting play mode
        _lastNodePerGraph.Clear();
        _activeGraphs.Clear();
        _executionHistory.Clear();
        _stateChangeHistory.Clear();
        _graphStats.Clear();
        RepaintAllGraphWindows();
      }
    }

    private static void OnNodeExecuted(INodeExecutionDebugEvent e)
    {
      _lastNodePerGraph[e.GraphId] = e.NodeId;

      // Update active graph info
      if (!_activeGraphs.TryGetValue(e.GraphId, out var graphInfo))
      {
        graphInfo = new ActiveGraphInfo { GraphId = e.GraphId };
        _activeGraphs[e.GraphId] = graphInfo;
      }

      graphInfo.LastExecutedNodeId = e.NodeId;
      graphInfo.LastExecutedNodeType = e.NodeType;
      graphInfo.LastExecutionTime = e.TimeUtc;
      graphInfo.TotalNodeExecutions++;

      // Update stats
      if (!_graphStats.TryGetValue(e.GraphId, out var stats))
      {
        stats = new GraphExecutionStats { GraphId = e.GraphId };
        _graphStats[e.GraphId] = stats;
      }

      stats.TotalExecutions++;
      stats.TotalExecutionTime += e.ExecutionTime;
      if (e.ExecutionTime > stats.MaxExecutionTime)
        stats.MaxExecutionTime = e.ExecutionTime;
      if (stats.MinExecutionTime == TimeSpan.Zero || e.ExecutionTime < stats.MinExecutionTime)
        stats.MinExecutionTime = e.ExecutionTime;

      if (e.ErrorMessage != null)
        stats.ErrorCount++;

      // Add to history
      _executionHistory.Add(new NodeExecutionRecord
      {
        GraphId = e.GraphId,
        NodeId = e.NodeId,
        NodeType = e.NodeType,
        ExecutionTime = e.ExecutionTime,
        TimeUtc = e.TimeUtc,
        ErrorMessage = e.ErrorMessage
      });

      // Trim history
      if (_executionHistory.Count > MaxHistorySize)
        _executionHistory.RemoveAt(0);

      EditorApplication.delayCall += RepaintAllGraphWindows;
    }

    private static void OnStateChanged(IGraphStateChangeDebugEvent e)
    {
      // Add to state change history
      _stateChangeHistory.Add(new StateChangeRecord
      {
        GraphId = e.GraphId,
        StateKey = e.StateKey,
        OldValue = e.OldValue,
        NewValue = e.NewValue,
        TimeUtc = e.TimeUtc
      });

      // Trim history
      if (_stateChangeHistory.Count > MaxStateChangeHistorySize)
        _stateChangeHistory.RemoveAt(0);

      EditorApplication.delayCall += RepaintAllGraphWindows;
    }

    private static void OnGraphExecutionChanged(IGraphExecutionDebugEvent e)
    {
      if (!_activeGraphs.TryGetValue(e.GraphId, out var graphInfo))
      {
        graphInfo = new ActiveGraphInfo
        {
          GraphId = e.GraphId,
          GraphDomain = e.GraphDomain
        };
        _activeGraphs[e.GraphId] = graphInfo;
      }

      graphInfo.GraphDomain = e.GraphDomain;
      graphInfo.IsExecuting = e.IsStarting;
      graphInfo.LastTriggerMessageType = e.TriggerMessageType;

      if (e.IsStarting)
      {
        graphInfo.ExecutionStartTime = e.TimeUtc;
        graphInfo.ExecutionCount++;
      }
      else
      {
        if (graphInfo.ExecutionStartTime.HasValue)
        {
          graphInfo.LastExecutionDuration = e.TimeUtc - graphInfo.ExecutionStartTime.Value;
          graphInfo.ExecutionStartTime = null;
        }
      }

      EditorApplication.delayCall += RepaintAllGraphWindows;
    }

    private static void RepaintAllGraphWindows()
    {
      var windows = UnityEngine.Resources.FindObjectsOfTypeAll<XNodeEditor.NodeEditorWindow>();
      foreach (var w in windows)
        w.Repaint();
    }

    public static bool IsLastExecuted(string graphId, string nodeId)
    {
      return _lastNodePerGraph.TryGetValue(graphId, out var last) && last == nodeId;
    }

    public static IReadOnlyDictionary<string, string> LastNodePerGraph => _lastNodePerGraph;
    public static IReadOnlyDictionary<string, ActiveGraphInfo> ActiveGraphs => _activeGraphs;
    public static IReadOnlyList<NodeExecutionRecord> ExecutionHistory => _executionHistory;
    public static IReadOnlyList<StateChangeRecord> StateChangeHistory => _stateChangeHistory;
    public static IReadOnlyDictionary<string, GraphExecutionStats> GraphStats => _graphStats;

    public static ActiveGraphInfo? GetGraphInfo(string graphId)
    {
      return _activeGraphs.TryGetValue(graphId, out var info) ? info : null;
    }

    public static GraphExecutionStats? GetGraphStats(string graphId)
    {
      return _graphStats.TryGetValue(graphId, out var stats) ? stats : null;
    }

    public static void ClearHistory()
    {
      _executionHistory.Clear();
      _stateChangeHistory.Clear();
    }
  }

  public sealed class ActiveGraphInfo
  {
    public string GraphId { get; set; } = "";
    public string GraphDomain { get; set; } = "";
    public string? LastExecutedNodeId { get; set; }
    public string? LastExecutedNodeType { get; set; }
    public DateTime? LastExecutionTime { get; set; }
    public bool IsExecuting { get; set; }
    public DateTime? ExecutionStartTime { get; set; }
    public TimeSpan? LastExecutionDuration { get; set; }
    public string? LastTriggerMessageType { get; set; }
    public int ExecutionCount { get; set; }
    public int TotalNodeExecutions { get; set; }
  }

  public sealed class NodeExecutionRecord
  {
    public string GraphId { get; set; } = "";
    public string NodeId { get; set; } = "";
    public string NodeType { get; set; } = "";
    public TimeSpan ExecutionTime { get; set; }
    public DateTime TimeUtc { get; set; }
    public string? ErrorMessage { get; set; }
  }

  public sealed class StateChangeRecord
  {
    public string GraphId { get; set; } = "";
    public string StateKey { get; set; } = "";
    public object? OldValue { get; set; }
    public object? NewValue { get; set; }
    public DateTime TimeUtc { get; set; }
  }

  public sealed class GraphExecutionStats
  {
    public string GraphId { get; set; } = "";
    public int TotalExecutions { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public TimeSpan MaxExecutionTime { get; set; }
    public TimeSpan MinExecutionTime { get; set; }
    public int ErrorCount { get; set; }

    public TimeSpan AverageExecutionTime =>
      TotalExecutions > 0 ? TimeSpan.FromMilliseconds(TotalExecutionTime.TotalMilliseconds / TotalExecutions) : TimeSpan.Zero;
  }
}
#endif

