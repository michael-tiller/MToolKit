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

    [Output(dynamicPortList = true)]
    public NodeConnection[] ChoiceOutputs;

    [BoxGroup("Choices")]
    [ListDrawerSettings(ShowFoldout = true, DraggableItems = true)]
    [OnValueChanged(nameof(SyncOutputPorts))]
    public List<Choice> Choices = new();

    public override object GetValue(NodePort port)
    {
      return null;
    }

    /// <summary>
    ///   Synchronizes the ChoiceOutputs array with the Choices list.
    ///   xNode's dynamicPortList automatically creates ports based on the array length.
    /// </summary>
    private void SyncOutputPorts()
    {
      if (graph == null) return; // Not initialized yet

      // Resize the ChoiceOutputs array to match the Choices list
      // xNode's [Output(dynamicPortList = true)] will automatically create ports for each array element
      if (ChoiceOutputs == null || ChoiceOutputs.Length != Choices.Count)
      {
        var newOutputs = new NodeConnection[Choices.Count];
        if (ChoiceOutputs != null)
        {
          // Copy existing connections if any
          for (int i = 0; i < Math.Min(ChoiceOutputs.Length, newOutputs.Length); i++)
          {
            newOutputs[i] = ChoiceOutputs[i];
          }
        }
        ChoiceOutputs = newOutputs;
      }
    }

    protected override void Init()
    {
      base.Init();
      SyncOutputPorts();
    }

    public override void OnCreateConnection(NodePort from, NodePort to)
    {
      base.OnCreateConnection(from, to);
    }

    public override void OnRemoveConnection(NodePort port)
    {
      base.OnRemoveConnection(port);
    }

    [Serializable]
    public class Choice
    {
      [TextArea(1, 3)]
      public string Text = "Choice text";
    }
  }
}