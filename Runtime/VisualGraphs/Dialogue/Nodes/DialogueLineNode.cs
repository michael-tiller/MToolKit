using MToolKit.Runtime.VisualGraphs.Authoring;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Dialogue.Nodes
{
  /// <summary>
  ///   Displays a dialogue line and waits for acknowledgment before continuing.
  /// </summary>
  [CreateNodeMenu("Dialogue/Line")]
  [NodeTint("#A66B7F")]
  public sealed class DialogueLineNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    [BoxGroup("Speaker")]
    [Tooltip("Speaker ID or name")]
    public string SpeakerId = "NPC";

    [BoxGroup("Content")]
    [TextArea(3, 10)]
    [Tooltip("Dialogue text to display")]
    public string Text = "Hello, traveler!";

    [BoxGroup("Timing")]
    [Tooltip("Minimum time the line should stay on screen before it is allowed to advance (manual or auto)")]
    [MinValue(0)]
    public float MinDisplaySeconds = 0.0f;

    [BoxGroup("Timing")]
    [Tooltip("If true, automatically advance after MinDisplaySeconds + AutoAdvanceDelaySeconds")]
    public bool AutoAdvance = false;

    [BoxGroup("Timing")]
    [Tooltip("Additional delay before auto-advancing (only used if AutoAdvance is true)")]
    [MinValue(0)]
    [ShowIf("AutoAdvance")]
    public float AutoAdvanceDelaySeconds = 0.0f;

    [BoxGroup("Timing")]
    [Tooltip("If true, player can skip this line by clicking Next before MinDisplaySeconds")]
    public bool Skippable = true;

    [HideInInspector]
    public string OriginalLabel = "";

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}