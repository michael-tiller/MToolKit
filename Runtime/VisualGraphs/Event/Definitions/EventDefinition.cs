using MToolKit.Runtime.Utilities;
using MToolKit.Runtime.VisualGraphs.Event.Graphs;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MToolKit.Runtime.VisualGraphs.Event.Definitions
{
  /// <summary>
  ///   Event definition asset linking an event ID to a graph.
  ///   Generic — can be attached to items, setpieces, tiles, or any game entity
  ///   that needs reactive event-driven behavior.
  /// </summary>
  [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Event Definition", fileName = "EventDef", order = 302)]
  public sealed class EventDefinition : GuidScriptableObject
  {
    [BoxGroup("Identity")]
    [Tooltip("Unique event identifier")]
    public string EventId => $"event_{Guid}";

    [ReadOnly]
    [ShowInInspector]
    [PropertyOrder(-1)]
    public string Id => $"event_{Guid}";

    [BoxGroup("Graph")]
    [Required]
    [Tooltip("Event graph asset to execute. Required — events cannot function without a graph.")]
    public EventGraphAsset GraphAsset;

#if UNITY_EDITOR
    [BoxGroup("Graph")]
    [Button("Create & Link Event Graph", ButtonSizes.Medium)]
    [GUIColor(0.3f, 0.8f, 0.3f)]
    [ShowIf("@GraphAsset == null")]
    [Tooltip("Links to existing EventGraphAsset if found, otherwise creates a new one in the same folder with '_Graph' suffix")]
    private void CreateAndLinkEventGraph()
    {
      var assetPath = AssetDatabase.GetAssetPath(this);
      if (string.IsNullOrEmpty(assetPath))
      {
        Debug.LogError("Cannot create graph: Asset must be saved to disk first.");
        return;
      }

      var folderPath = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
      var assetName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
      var graphName = $"{assetName}_Graph";
      var graphPath = $"{folderPath}/{graphName}.asset";

      // Check if graph already exists — if so, just link it
      var existingGraph = AssetDatabase.LoadAssetAtPath<EventGraphAsset>(graphPath);
      if (existingGraph != null)
      {
        GraphAsset = existingGraph;
        EditorUtility.SetDirty(this);
        Debug.Log($"Linked to existing event graph: {graphPath}");
        return;
      }

      // Create new graph asset
      var graphAsset = ScriptableObject.CreateInstance<EventGraphAsset>();
      graphAsset.name = graphName;

      AssetDatabase.CreateAsset(graphAsset, graphPath);
      AssetDatabase.SaveAssets();
      AssetDatabase.Refresh();

      // Link it
      GraphAsset = graphAsset;
      EditorUtility.SetDirty(this);

      Debug.Log($"Created and linked event graph: {graphPath}");
    }
#endif
  }
}