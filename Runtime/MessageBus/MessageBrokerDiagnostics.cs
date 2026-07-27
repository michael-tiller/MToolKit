#if UNITY_EDITOR
using System.Collections.Generic;

namespace MToolKit.Runtime.MessageBus
{
  /// <summary>
  ///   Diagnostic information about a message broker (Editor only).
  ///
  ///   <para>Moved verbatim out of <c>GameMessageBroker.cs</c>, which declared these shared DTOs even though
  ///   <c>GlobalAsyncMessageBroker.GetDiagnosticInfo()</c> also returns them. The <c>#if UNITY_EDITOR</c>
  ///   boundary is preserved exactly: exposing them in players would change the shipped API surface.</para>
  /// </summary>
  public class MessageBrokerDiagnosticInfo
  {
    public string BrokerName { get; set; }
    public bool IsInitialized { get; set; }
    public bool HasResolver { get; set; }
    public int PendingRequestCount { get; set; }
    public Dictionary<string, int> PendingRequestsByType { get; set; } = new();
    public List<string> AccessedPublishers { get; set; } = new();
    public List<string> AccessedSubscribers { get; set; } = new();
    public List<MessageTypeInfo> RegisteredMessageTypes { get; set; } = new();
  }

  /// <summary>
  ///   Information about a registered message type (Editor only).
  /// </summary>
  public class MessageTypeInfo
  {
    public string MessageTypeName { get; set; }
    public bool HasPublisher { get; set; }
    public bool HasSubscriber { get; set; }
  }
}
#endif
