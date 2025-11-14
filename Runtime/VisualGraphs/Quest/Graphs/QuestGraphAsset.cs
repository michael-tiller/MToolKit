using System;
using System.Collections.Generic;
using System.Linq;
using MToolKit.Runtime.Core.Types;
using MToolKit.Runtime.VisualGraphs.Quest.Nodes;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Quest.Graphs
{
  /// <summary>
  ///   Quest graph asset for authoring quest logic.
  /// </summary>
  [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Quests/Quest Graph", fileName = "QuestGraph", order = 100)]
  public sealed class QuestGraphAsset : NodeGraph
  {
    [BoxGroup("Description")]
    [TextArea(3, 10)]
    [HideLabel]
    public string Description;

    [BoxGroup("Performance")]
    [Tooltip("Max nodes executed per message (prevents infinite loops)")]
    [Range(64, 4096)]
    [InfoBox("Default: 1024. Increase for complex graphs, decrease for simple ones.")]
    public int MaxExecutionSteps = 1024;

    [BoxGroup("Event Subscriptions")]
    [InfoBox("Graph will ONLY execute when these MessagePipe events are received.\n" +
             "Entry nodes must exist for 'Required' subscriptions.")]
    [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
    [ValidateInput(nameof(ValidateSubscriptions), "See validation errors above")]
    public List<MessageSubscription> Subscriptions = new();

#if UNITY_EDITOR
    [BoxGroup("Event Subscriptions")]
    [Button("Auto-Populate from Entry Nodes", ButtonSizes.Medium)]
    [GUIColor(0.3f, 0.8f, 0.3f)]
    private void AutoPopulateSubscriptions()
    {
      Subscriptions.Clear();

      foreach (var node in nodes)
      {
        if (node is QuestOnEventNode entryNode && entryNode.MessageType != null && entryNode.MessageType.IsValid)
        {
          Subscriptions.Add(new MessageSubscription
          {
            MessageType = new MessageTypeReference { Type = entryNode.MessageType.Type },
            Required = true,
            DomainFilter = entryNode.DomainFilter
          });
        }
      }

      UnityEditor.EditorUtility.SetDirty(this);
    }

    [BoxGroup("Event Subscriptions")]
    [Button("Validate Graph", ButtonSizes.Medium)]
    [GUIColor(0.4f, 0.6f, 1.0f)]
    private void ValidateGraph()
    {
      var (isValid, errors) = ValidateSubscriptions(Subscriptions, null);

      if (isValid)
      {
        UnityEditor.EditorUtility.DisplayDialog(
          "Graph Valid ✓",
          "All subscriptions have matching entry nodes!",
          "OK");
      }
      else
      {
        UnityEditor.EditorUtility.DisplayDialog(
          "Graph Invalid ✗",
          string.Join("\n", errors),
          "OK");
      }
    }

    private (bool isValid, string[] errors) ValidateSubscriptions(List<MessageSubscription> subscriptions, string memberName)
    {
      if (subscriptions == null || subscriptions.Count == 0)
        return (true, null); // Empty subscriptions is valid (graph won't run)

      var errors = new List<string>();
      var entryNodes = nodes.OfType<QuestOnEventNode>().ToList();

      // Check 1: Required subscriptions must have entry nodes
      foreach (var subscription in subscriptions.Where(s => s.Required))
      {
        if (subscription?.MessageType == null || !subscription.MessageType.IsValid)
        {
          errors.Add($"⚠ Required subscription has invalid message type");
          continue;
        }

        var hasMatchingEntry = entryNodes.Any(node =>
          node.MessageType != null &&
          node.MessageType.IsValid &&
          node.MessageType.Type == subscription.MessageType.Type &&
          (string.IsNullOrEmpty(subscription.DomainFilter) ||
           node.DomainFilter == subscription.DomainFilter));

        if (!hasMatchingEntry)
        {
          errors.Add($"✗ Required subscription '{subscription.MessageType.Name}' has no entry node" +
                    (string.IsNullOrEmpty(subscription.DomainFilter) ? "" : $" (domain: {subscription.DomainFilter})"));
        }
      }

      // Check 2: Entry nodes should have subscriptions (warning only)
      foreach (var entryNode in entryNodes)
      {
        if (entryNode.MessageType == null || !entryNode.MessageType.IsValid)
          continue;

        var hasSubscription = subscriptions.Any(s =>
          s?.MessageType != null &&
          s.MessageType.IsValid &&
          s.MessageType.Type == entryNode.MessageType.Type &&
          (string.IsNullOrEmpty(s.DomainFilter) || s.DomainFilter == entryNode.DomainFilter));

        if (!hasSubscription)
        {
          errors.Add($"⚠ Entry node '{entryNode.name}' ({entryNode.MessageType.Name}) has no subscription (will never execute)");
        }
      }

      return (errors.Count == 0, errors.ToArray());
    }
#endif
  }

  /// <summary>
  ///   Defines a graph's subscription to a specific MessagePipe message type.
  /// </summary>
  [Serializable]
  public sealed class MessageSubscription
  {
    [BoxGroup("Message Type")]
    [Required]
    [LabelText("Message Type")]
    [Tooltip("MessagePipe message type to subscribe to (must implement IGameMessage)")]
    public MessageTypeReference MessageType = new();

    [BoxGroup("Options")]
    [Tooltip("Export validation fails if no entry node exists for this message type")]
    public bool Required = true;

    [BoxGroup("Options")]
    [Tooltip("Optional filter - only process messages from this domain/context")]
    public string DomainFilter;

    public override string ToString()
    {
      var req = Required ? "[Required]" : "[Optional]";
      var typeName = MessageType?.Name ?? "(No Type)";
      var domain = !string.IsNullOrEmpty(DomainFilter) ? $" ({DomainFilter})" : "";
      return $"{req} {typeName}{domain}";
    }
  }
}
