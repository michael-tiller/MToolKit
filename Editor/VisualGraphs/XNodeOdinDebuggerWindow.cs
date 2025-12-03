#if UNITY_EDITOR
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Bootstrap;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.Debug;

namespace MToolKit.Editor.VisualGraphs
{
  /// <summary>
  ///   Comprehensive runtime debugger window for VisualGraphs.
  ///   Shows active graphs, execution history, state changes, and allows manual event triggering.
  /// </summary>
  public sealed class XNodeOdinDebuggerWindow : OdinEditorWindow
  {
    [MenuItem("Tools/MToolKit/VisualGraphs Debugger")]
    private static void Open()
    {
      var window = GetWindow<XNodeOdinDebuggerWindow>();
      window.titleContent = new GUIContent("VisualGraphs Debugger");
      window.Show();
    }

    [ShowInInspector, ReadOnly, PropertyOrder(-10)]
    [Title("Debugger Status")]
    private string Status => Application.isPlaying ? "Running" : "Not in Play Mode";

    [ShowInInspector, PropertyOrder(-9)]
    [Title("Active Graphs")]
    [TableList(IsReadOnly = true, AlwaysExpanded = true)]
    private List<ActiveGraphViewModel> ActiveGraphs { get; } = new();

    [ShowInInspector, PropertyOrder(-8)]
    [Title("Execution History")]
    [TableList(IsReadOnly = true)]
    private List<NodeExecutionViewModel> ExecutionHistory { get; } = new();

    [ShowInInspector, PropertyOrder(-7)]
    [Title("State Changes")]
    [TableList(IsReadOnly = true)]
    private List<StateChangeViewModel> StateChanges { get; } = new();

    [ShowInInspector, PropertyOrder(-6)]
    [Title("Graph Statistics")]
    [TableList(IsReadOnly = true)]
    private List<GraphStatsViewModel> GraphStatistics { get; } = new();

    [ShowInInspector, PropertyOrder(-5)]
    [Title("Manual Event Triggering")]
    [InfoBox("Select a message type and provide JSON data to trigger events manually for testing.")]
    private ManualEventTriggerViewModel ManualTrigger { get; } = new();

    [ShowInInspector, PropertyOrder(-4)]
    [Title("State Inspector")]
    [InfoBox("View and edit graph state values at runtime.")]
    private StateInspectorViewModel StateInspector { get; set; } = null!;

    internal GraphEventRouter? router;
    private bool isSubscribed;

    protected override void Initialize()
    {
      base.Initialize();
      StateInspector = new StateInspectorViewModel(this);
      SubscribeToEvents();
      EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    protected override void OnDestroy()
    {
      base.OnDestroy();
      UnsubscribeFromEvents();
      EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
      if (state == PlayModeStateChange.EnteredPlayMode)
      {
        // Try to find router in play mode
        RefreshRouter();
      }
      else if (state == PlayModeStateChange.ExitingPlayMode)
      {
        router = null;
        ActiveGraphs.Clear();
        ExecutionHistory.Clear();
        StateChanges.Clear();
        GraphStatistics.Clear();
      }
    }

    private void RefreshRouter()
    {
      // Try to find GraphEventRouter in the scene
      // Uses reflection to access private fields since we're in editor context
      router = null;
      var bridge = FindFirstObjectByType<EventBusBridge>();
      if (bridge != null)
      {
        // Use reflection to get the router field
        var field = typeof(EventBusBridge).GetField("router",
          BindingFlags.NonPublic | BindingFlags.Instance);
        router = field?.GetValue(bridge) as GraphEventRouter;
      }
    }

    private void SubscribeToEvents()
    {
      if (isSubscribed) return;

      NodeDebugEvents.NodeExecuted += OnNodeExecuted;
      NodeDebugEvents.StateChanged += OnStateChanged;
      NodeDebugEvents.GraphExecutionChanged += OnGraphExecutionChanged;

      isSubscribed = true;
    }

    private void UnsubscribeFromEvents()
    {
      if (!isSubscribed) return;

      NodeDebugEvents.NodeExecuted -= OnNodeExecuted;
      NodeDebugEvents.StateChanged -= OnStateChanged;
      NodeDebugEvents.GraphExecutionChanged -= OnGraphExecutionChanged;

      isSubscribed = false;
    }

    private void OnNodeExecuted(INodeExecutionDebugEvent e)
    {
      ExecutionHistory.Insert(0, new NodeExecutionViewModel
      {
        GraphId = e.GraphId,
        NodeId = e.NodeId,
        NodeType = e.NodeType,
        ExecutionTimeMs = e.ExecutionTime.TotalMilliseconds,
        TimeUtc = e.TimeUtc,
        HasError = !string.IsNullOrEmpty(e.ErrorMessage),
        ErrorMessage = e.ErrorMessage
      });

      // Keep only last 200 entries
      if (ExecutionHistory.Count > 200)
        ExecutionHistory.RemoveAt(ExecutionHistory.Count - 1);

      UpdateActiveGraphs();
      UpdateGraphStatistics();
      Repaint();
    }

    private void OnStateChanged(IGraphStateChangeDebugEvent e)
    {
      StateChanges.Insert(0, new StateChangeViewModel
      {
        GraphId = e.GraphId,
        StateKey = e.StateKey,
        OldValue = e.OldValue?.ToString() ?? "null",
        NewValue = e.NewValue?.ToString() ?? "null",
        TimeUtc = e.TimeUtc
      });

      // Keep only last 100 entries
      if (StateChanges.Count > 100)
        StateChanges.RemoveAt(StateChanges.Count - 1);

      Repaint();
    }

    private void OnGraphExecutionChanged(IGraphExecutionDebugEvent e)
    {
      UpdateActiveGraphs();
      Repaint();
    }

    private void UpdateActiveGraphs()
    {
      ActiveGraphs.Clear();

      if (router == null)
      {
        RefreshRouter();
      }

      if (router != null)
      {
        foreach (var runner in router.GetRunners())
        {
          var graphInfo = XNodeDebugState.GetGraphInfo(runner.GraphId);
          var stats = XNodeDebugState.GetGraphStats(runner.GraphId);

          ActiveGraphs.Add(new ActiveGraphViewModel
          {
            GraphId = runner.GraphId,
            Domain = runner.GraphDomain,
            IsExecuting = graphInfo?.IsExecuting ?? false,
            LastExecutedNode = graphInfo?.LastExecutedNodeId ?? "None",
            ExecutionCount = graphInfo?.ExecutionCount ?? 0,
            TotalNodeExecutions = graphInfo?.TotalNodeExecutions ?? 0,
            AverageExecutionTimeMs = stats?.AverageExecutionTime.TotalMilliseconds ?? 0,
            ErrorCount = stats?.ErrorCount ?? 0
          });
        }
      }
    }

    private void UpdateGraphStatistics()
    {
      GraphStatistics.Clear();

      foreach (var kvp in XNodeDebugState.GraphStats)
      {
        var stats = kvp.Value;
        GraphStatistics.Add(new GraphStatsViewModel
        {
          GraphId = stats.GraphId,
          TotalExecutions = stats.TotalExecutions,
          AverageExecutionTimeMs = stats.AverageExecutionTime.TotalMilliseconds,
          MaxExecutionTimeMs = stats.MaxExecutionTime.TotalMilliseconds,
          MinExecutionTimeMs = stats.MinExecutionTime.TotalMilliseconds,
          ErrorCount = stats.ErrorCount
        });
      }
    }

    private void Update()
    {
      if (Application.isPlaying)
      {
        // Periodically refresh active graphs
        UpdateActiveGraphs();
        UpdateGraphStatistics();
      }
    }

    [Serializable]
    public sealed class ActiveGraphViewModel
    {
      [TableColumnWidth(150)]
      public string GraphId = "";

      [TableColumnWidth(100)]
      public string Domain = "";

      [TableColumnWidth(80)]
      public bool IsExecuting;

      [TableColumnWidth(120)]
      public string LastExecutedNode = "";

      [TableColumnWidth(80)]
      public int ExecutionCount;

      [TableColumnWidth(100)]
      public int TotalNodeExecutions;

      [TableColumnWidth(120)]
      [LabelText("Avg (ms)")]
      public double AverageExecutionTimeMs;

      [TableColumnWidth(80)]
      public int ErrorCount;
    }

    [Serializable]
    public sealed class NodeExecutionViewModel
    {
      [TableColumnWidth(120)]
      public string GraphId = "";

      [TableColumnWidth(100)]
      public string NodeId = "";

      [TableColumnWidth(150)]
      public string NodeType = "";

      [TableColumnWidth(100)]
      [LabelText("Time (ms)")]
      public double ExecutionTimeMs;

      [TableColumnWidth(150)]
      public DateTime TimeUtc;

      [TableColumnWidth(60)]
      public bool HasError;

      [TableColumnWidth(200)]
      [ShowIf("HasError")]
      public string? ErrorMessage;
    }

    [Serializable]
    public sealed class StateChangeViewModel
    {
      [TableColumnWidth(120)]
      public string GraphId = "";

      [TableColumnWidth(150)]
      public string StateKey = "";

      [TableColumnWidth(200)]
      public string OldValue = "";

      [TableColumnWidth(200)]
      public string NewValue = "";

      [TableColumnWidth(150)]
      public DateTime TimeUtc;
    }

    [Serializable]
    public sealed class GraphStatsViewModel
    {
      [TableColumnWidth(150)]
      public string GraphId = "";

      [TableColumnWidth(100)]
      public int TotalExecutions;

      [TableColumnWidth(120)]
      [LabelText("Avg (ms)")]
      public double AverageExecutionTimeMs;

      [TableColumnWidth(120)]
      [LabelText("Max (ms)")]
      public double MaxExecutionTimeMs;

      [TableColumnWidth(120)]
      [LabelText("Min (ms)")]
      public double MinExecutionTimeMs;

      [TableColumnWidth(80)]
      public int ErrorCount;
    }

    [Serializable]
    public sealed class ManualEventTriggerViewModel
    {
      [ValueDropdown("GetMessageTypes")]
      [LabelText("Message Type")]
      public string SelectedMessageType = "";

      [MultiLineProperty(5)]
      [LabelText("JSON Data (optional)")]
      [InfoBox("Enter JSON data matching the message type. Leave empty for default constructor.")]
      public string JsonData = "";

      [Button("Trigger Event", ButtonSizes.Medium)]
      [EnableIf("CanTrigger")]
      private void TriggerEvent()
      {
        if (string.IsNullOrEmpty(SelectedMessageType))
          return;

        try
        {
          var messageType = Type.GetType(SelectedMessageType);
          if (messageType == null)
          {
            EditorUtility.DisplayDialog("Error", $"Message type '{SelectedMessageType}' not found.", "OK");
            return;
          }

          if (!typeof(IGameMessage).IsAssignableFrom(messageType))
          {
            EditorUtility.DisplayDialog("Error", $"Type '{SelectedMessageType}' does not implement IGameMessage.", "OK");
            return;
          }

          object message;
          if (string.IsNullOrWhiteSpace(JsonData))
          {
            // Try default constructor
            message = Activator.CreateInstance(messageType);
          }
          else
          {
            // Try to deserialize JSON
            message = JsonUtility.FromJson(JsonData, messageType);
          }

          if (message == null)
          {
            EditorUtility.DisplayDialog("Error", "Failed to create message instance.", "OK");
            return;
          }

          // Publish via GameMessageBroker using reflection
          var publishMethod = typeof(GameMessageBroker)
            .GetMethod(nameof(GameMessageBroker.Publish))
            ?.MakeGenericMethod(messageType);

          if (publishMethod == null)
          {
            EditorUtility.DisplayDialog("Error", "Failed to get Publish method.", "OK");
            return;
          }

          publishMethod.Invoke(null, new[] { message });
          Debug.Log($"Manually triggered event: {SelectedMessageType}");
        }
        catch (Exception ex)
        {
          EditorUtility.DisplayDialog("Error", $"Failed to trigger event: {ex.Message}", "OK");
          Debug.LogException(ex);
        }
      }

      private bool CanTrigger => Application.isPlaying && !string.IsNullOrEmpty(SelectedMessageType);

      private IEnumerable<string> GetMessageTypes()
      {
        // Find all types that implement IGameMessage
        var messageTypes = AppDomain.CurrentDomain.GetAssemblies()
          .SelectMany(a =>
          {
            try
            {
              return a.GetTypes();
            }
            catch
            {
              return Enumerable.Empty<Type>();
            }
          })
          .Where(t => typeof(IGameMessage).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
          .Select(t => t.AssemblyQualifiedName ?? t.FullName ?? t.Name)
          .OrderBy(n => n);

        return messageTypes;
      }
    }

    [Serializable]
    public sealed class StateInspectorViewModel
    {
      private readonly XNodeOdinDebuggerWindow window;

      public StateInspectorViewModel(XNodeOdinDebuggerWindow window)
      {
        this.window = window;
      }

      [ValueDropdown("GetGraphIds")]
      [LabelText("Graph ID")]
      public string SelectedGraphId = "";

      [ShowInInspector, ReadOnly]
      [TableList(IsReadOnly = false)]
      [ShowIf("HasSelectedGraph")]
      private List<StateValueViewModel> StateValues { get; } = new();

      [Button("Refresh State", ButtonSizes.Medium)]
      [EnableIf("CanRefresh")]
      private void RefreshState()
      {
        if (string.IsNullOrEmpty(SelectedGraphId) || window.router == null)
          return;

        window.RefreshRouter();
        if (window.router == null)
          return;

        var runner = window.router.GetRunners().FirstOrDefault(r => r.GraphId == SelectedGraphId);
        if (runner == null)
          return;

        StateValues.Clear();

        var snapshot = runner.ExportState();
        foreach (var kvp in snapshot.Data)
        {
          StateValues.Add(new StateValueViewModel
          {
            Key = kvp.Key,
            Value = kvp.Value?.ToString() ?? "null",
            ValueType = kvp.Value?.GetType().Name ?? "null"
          });
        }
      }

      [Button("Save State Changes", ButtonSizes.Medium)]
      [EnableIf("CanSave")]
      private void SaveStateChanges()
      {
        if (string.IsNullOrEmpty(SelectedGraphId) || window.router == null)
          return;

        window.RefreshRouter();
        if (window.router == null)
          return;

        var runner = window.router.GetRunners().FirstOrDefault(r => r.GraphId == SelectedGraphId);
        if (runner == null)
          return;

        // Note: This is a simplified implementation
        // In a real scenario, you'd need to properly deserialize and set values
        EditorUtility.DisplayDialog("Info", "State editing not fully implemented. Use graph state API directly.", "OK");
      }

      private bool HasSelectedGraph => !string.IsNullOrEmpty(SelectedGraphId);
      private bool CanRefresh => Application.isPlaying && !string.IsNullOrEmpty(SelectedGraphId);
      private bool CanSave => Application.isPlaying && !string.IsNullOrEmpty(SelectedGraphId) && StateValues.Count > 0;

      private IEnumerable<string> GetGraphIds()
      {
        if (window.router == null)
        {
          window.RefreshRouter();
        }

        if (window.router == null)
          return Enumerable.Empty<string>();

        return window.router.GetRunners().Select(r => r.GraphId).OrderBy(id => id);
      }

      [Serializable]
      public sealed class StateValueViewModel
      {
        [TableColumnWidth(200)]
        public string Key = "";

        [TableColumnWidth(100)]
        public string ValueType = "";

        [TableColumnWidth(300)]
        public string Value = "";
      }
    }
  }
}
#endif

