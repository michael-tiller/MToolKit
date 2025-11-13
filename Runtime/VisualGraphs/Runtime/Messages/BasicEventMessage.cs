using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Runtime.Messages
{
  /// <summary>
  ///   Basic implementation of IEventMessage.
  /// </summary>
  public sealed class BasicEventMessage : IEventMessage
  {
    public BasicEventMessage(
      string type,
      string domain,
      long sequenceId,
      object payload = null,
      IReadOnlyDictionary<string, object> metadata = null)
    {
      Type = type;
      Domain = domain ?? string.Empty;
      SequenceId = sequenceId;
      Payload = payload;
      Metadata = metadata ?? new Dictionary<string, object>();
    }

    public string Type { get; }
    public string Domain { get; }
    public long SequenceId { get; }
    public object Payload { get; }
    public IReadOnlyDictionary<string, object> Metadata { get; }
  }
}