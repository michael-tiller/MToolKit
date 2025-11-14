using MToolKit.Runtime.VisualGraphs.Definitions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Config
{
  /// <summary>
  ///   Configuration for the Visual Graphs plugin system.
  ///   Controls initialization, validation, and runtime behavior.
  /// </summary>
  [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Config", fileName = "VisualGraphConfig", order = 399)]
  [InlineEditor]
  public sealed class VisualGraphConfig : ScriptableObject
  {
    [BoxGroup("Logging")]
    [Tooltip("Enable verbose debug logging for graph operations")]
    public bool EnableVerboseLogging = false;

    [BoxGroup("Execution")]
    [Tooltip("Maximum execution steps per graph (prevents infinite loops)")]
    [MinValue(1)]
    public int MaxExecutionStepsPerGraph = 1024;

    [BoxGroup("Startup")]
    [Tooltip("Validate all graphs during startup (may increase load time)")]
    public bool ValidateGraphsOnStartup = true;

    [BoxGroup("Startup")]
    [Tooltip("Automatically initialize graphs from the default registry")]
    public bool AutoInitializeFromRegistry = true;

    [BoxGroup("Registry")]
    [Required]
    [Tooltip("Default visual graph registry containing all definitions")]
    public VisualGraphRegistry DefaultRegistry;

    [BoxGroup("Registry")]
    [Tooltip("Load all graphs on startup (true) or lazy load on demand (false)")]
    public bool LoadAllOnStartup = true;

    [BoxGroup("Quest System")]
    [Tooltip("Optional quest database for auto-starting campaigns")]
    [InlineEditor]
    public Quest.QuestDatabase QuestDatabase;

    [BoxGroup("Quest System")]
    [ShowIf(nameof(QuestDatabase))]
    [Tooltip("Auto-start the first quest from the database on plugin setup")]
    public bool AutoStartFirstQuest = false;
  }
}

