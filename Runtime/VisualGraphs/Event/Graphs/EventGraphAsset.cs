using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Authoring;
using MToolKit.Runtime.VisualGraphs.Quest.Graphs;
using MToolKit.Runtime.VisualGraphs.Variables;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MToolKit.Runtime.VisualGraphs.Event.Graphs
{
  /// <summary>
  ///   Generic event graph asset for reactive game logic.
  ///   Can be attached to any game entity (items, setpieces, tiles, etc.)
  ///   to define event-driven behavior via node graphs.
  /// </summary>
  [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Event Graph", fileName = "EventGraph", order = 102)]
  public sealed class EventGraphAsset : NodeGraph
  {
    [BoxGroup("Description")]
    [TextArea(3, 10)]
    [HideLabel]
    public string Description;

    [BoxGroup("Performance")]
    [Tooltip("Max nodes executed per message (prevents infinite loops)")]
    [Range(64, 4096)]
    [InfoBox("Default: 1024. Increase for complex event chains.")]
    public int MaxExecutionSteps = 1024;

    /// <summary>
    ///   Optional declared-variables block: validation/tooling metadata, NOT an init leg.
    ///   Init precedence stays GlobalGraphVariables → restored save state (wins);
    ///   declared defaults reach runtime through typed-accessor fallback, not ApplyTo.
    /// </summary>
    [BoxGroup("Variables")]
    [Tooltip("Optional declared-variable set for this graph. Used by export validation, the variable picker, " +
             "and the text authoring importer. Undeclared keys remain legal at runtime.")]
    public GraphVariableSet DeclaredVariables;

    [BoxGroup("Event Subscriptions")]
    [InfoBox("Graph will ONLY execute when these MessagePipe events are received.")]
    [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
    public List<MessageSubscription> Subscriptions = new();

#if UNITY_EDITOR
    /// <summary>
    ///   Override CopyNode to automatically regenerate GUIDs when duplicating nodes.
    /// </summary>
    public override Node CopyNode(Node original)
    {
      var copied = base.CopyNode(original);

      if (copied is VisualGraphNodeBase vgNode)
      {
        vgNode.RegenerateGuid();
      }

      return copied;
    }

    [BoxGroup("Utilities")]
    [Button("Fix Duplicate GUIDs", ButtonSizes.Medium)]
    [GUIColor(1.0f, 0.6f, 0.3f)]
    [Tooltip("Scans the graph for duplicate GUIDs and regenerates them.")]
    private void FixDuplicateGuids()
    {
      var guidCounts = new Dictionary<string, List<VisualGraphNodeBase>>();

      foreach (var node in nodes)
      {
        if (node is VisualGraphNodeBase vgNode && !string.IsNullOrEmpty(vgNode.Guid))
        {
          if (!guidCounts.ContainsKey(vgNode.Guid))
            guidCounts[vgNode.Guid] = new List<VisualGraphNodeBase>();
          guidCounts[vgNode.Guid].Add(vgNode);
        }
      }

      int fixedCount = 0;
      foreach (var kvp in guidCounts)
      {
        if (kvp.Value.Count > 1)
        {
          for (int i = 1; i < kvp.Value.Count; i++)
          {
            kvp.Value[i].RegenerateGuid();
            fixedCount++;
          }
        }
      }

      if (fixedCount > 0)
      {
        EditorUtility.SetDirty(this);
        Debug.Log($"Fixed {fixedCount} duplicate GUID(s) in event graph '{name}'");
      }
      else
      {
        Debug.Log($"No duplicate GUIDs found in event graph '{name}'");
      }
    }
#endif
  }
}