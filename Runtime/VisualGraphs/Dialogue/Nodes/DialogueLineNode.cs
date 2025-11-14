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

    [BoxGroup("Content")]
    [Tooltip("Optional localization key")]
    public LocalizedString LocalizationKey;

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}