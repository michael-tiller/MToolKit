using System;
using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Authoring;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Dialogue.Nodes
{
  /// <summary>
  ///   Presents dialogue choices to the player and branches based on selection.
  /// </summary>
  [CreateNodeMenu("Dialogue/Choice")]
  [NodeTint("#B8826B")]
  [NodeWidth(350)]
  public sealed class DialogueChoiceNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [BoxGroup("Choices")]
    [ListDrawerSettings(ShowFoldout = true, DraggableItems = true)]
    public List<Choice> Choices = new();

    public override object GetValue(NodePort port)
    {
      return null;
    }

    [Serializable]
    public class Choice
    {
      [TextArea(1, 3)]
      public string Text = "Choice text";

      [Tooltip("Optional localization key")]
      public LocalizedString LocalizationKey;

      [Output(dynamicPortList = true)]
      public NodeConnection Output;
    }
  }
}