namespace MToolKit.Tests.Editor.MessageBus.Support
{
  /// <summary>
  ///   Message types used by the MessageBus fixtures. Deliberately local to this folder rather than reusing
  ///   VisualGraphs' TestMessages: those carry domain semantics the broker tests must not depend on.
  /// </summary>
  public readonly struct BrokerPing
  {
    public readonly int Value;
    public BrokerPing(int value) => Value = value;
  }

  /// <summary>A second registered type, so tests can prove per-type isolation.</summary>
  public readonly struct BrokerPong
  {
    public readonly int Value;
    public BrokerPong(int value) => Value = value;
  }

  /// <summary>Request half of the RequestAsync/CompleteRequest round-trip.</summary>
  public readonly struct BrokerRequest
  {
    public readonly string Id;
    public BrokerRequest(string id) => Id = id;
  }

  /// <summary>Response half. FIFO matching is by response TYPE, so one type covers concurrent requests.</summary>
  public readonly struct BrokerResponse
  {
    public readonly string Id;
    public BrokerResponse(string id) => Id = id;
  }

  /// <summary>Registered with NO broker, to exercise the unregistered-type throw.</summary>
  public readonly struct BrokerUnregistered
  {
  }
}
