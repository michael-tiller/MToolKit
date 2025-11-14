using System;
using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Quest.Graphs;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Dialogue.Graphs
{
  /// <summary>
  ///   Dialogue graph asset for authoring dialogue logic.
  /// </summary>
  [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Dialogue Graph", fileName = "DialogueGraph", order = 101)]
  public sealed class DialogueGraphAsset : NodeGraph
  {
    [BoxGroup("Description")]
    [TextArea(3, 10)]
    [HideLabel]
    public string Description;

    [BoxGroup("Dialogue")]
    [Tooltip("Default speaker for this dialogue")]
    public string DefaultSpeaker;

    [BoxGroup("Performance")]
    [Tooltip("Max nodes executed per message (prevents infinite loops)")]
    [Range(64, 4096)]
    [InfoBox("Default: 1024. Increase for complex dialogues with many branches.")]
    public int MaxExecutionSteps = 1024;

    [BoxGroup("Event Subscriptions")]
    [InfoBox("Graph will ONLY execute when these MessagePipe events are received.")]
    [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
    public List<MessageSubscription> Subscriptions = new();
  }
}