using System;

namespace MToolKit.Runtime.MessageBus
{
  public enum BrokerLifecyclePhase
  {
    PreInit,
    Initialized,
    Shutdown,
  }

  public enum BrokerAccessMode
  {
    Production,
    ReducedTest,
  }

  public readonly struct BrokerLifecycleAccess
  {
    public string Scope { get; }
    public string Operation { get; }
    public string MessageType { get; }
    public BrokerLifecyclePhase Phase { get; }

    public BrokerLifecycleAccess(string scope, string operation, string messageType, BrokerLifecyclePhase phase)
    {
      Scope = scope;
      Operation = operation;
      MessageType = messageType;
      Phase = phase;
    }
  }

  /// <summary>Receives broker lifecycle violations without coupling MToolKit to a consuming game.</summary>
  public interface IBrokerLifecycleReporter
  {
    BrokerAccessMode AccessMode { get; }
    void Report(BrokerLifecycleAccess access);
  }
}
