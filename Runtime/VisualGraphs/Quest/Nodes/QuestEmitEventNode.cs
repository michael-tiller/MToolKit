using System;
using System.Collections.Generic;
using MToolKit.Runtime.Core.Types;
using MToolKit.Runtime.VisualGraphs.Authoring;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Quest.Nodes
{
  /// <summary>
  ///   Node that emits a MessagePipe event/message.
  /// </summary>
  [CreateNodeMenu("Quest/Emit Event")]
  [NodeTint("#A66B7F")]
  public sealed class QuestEmitEventNode : VisualGraphNodeBase
  {
    [Input] public NodePort Input;
    [Output] public NodePort Output;

    [BoxGroup("Message Type")]
    [Required]
    [LabelText("Message Type")]
    [Tooltip("MessagePipe message type to emit (must implement IGameMessage)")]
    public MessageTypeReference MessageType = new();

    [BoxGroup("Payload")]
    [InfoBox("Define payload data to include with the message")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<MessagePayloadParameter> Payload = new();

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }

  /// <summary>
  ///   Defines a parameter to include in the message payload.
  /// </summary>
  [Serializable]
  public sealed class MessagePayloadParameter
  {
    [HorizontalGroup]
    [LabelWidth(100)]
    public string ParameterName;

    [HorizontalGroup]
    [HideLabel]
    public ParameterValueType ValueType;

    [ShowIf(nameof(ValueType), ParameterValueType.String)]
    public string StringValue;

    [ShowIf(nameof(ValueType), ParameterValueType.Int)]
    public int IntValue;

    [ShowIf(nameof(ValueType), ParameterValueType.Float)]
    public float FloatValue;

    [ShowIf(nameof(ValueType), ParameterValueType.Bool)]
    public bool BoolValue;

    [ShowIf(nameof(ValueType), ParameterValueType.Variable)]
    [Tooltip("Reference a graph variable by name")]
    public string VariableName;
  }

  public enum ParameterValueType
  {
    String,
    Int,
    Float,
    Bool,
    Variable // Reference to graph variable
  }
}

