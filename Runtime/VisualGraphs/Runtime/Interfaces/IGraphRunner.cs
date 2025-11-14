using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.State;

namespace MToolKit.Runtime.VisualGraphs.Runtime.Interfaces
{
  /// <summary>
  ///   Runs a single graph instance with state management.
  ///   Consumes IGameMessage directly from MessagePipe.
  /// </summary>
  public interface IGraphRunner
  {
    /// <summary>Graph identifier</summary>
    string GraphId { get; }

    /// <summary>Graph domain</summary>
    string GraphDomain { get; }

    /// <summary>Runtime graph definition</summary>
    IRuntimeGraphDefinition Definition { get; }

    /// <summary>Check if this runner can handle the message type</summary>
    bool CanHandle(Type messageType, string domain = null);

    /// <summary>Handle a MessagePipe message</summary>
    UniTask HandleMessageAsync(IGameMessage message, string domain = null, CancellationToken ct = default);

    /// <summary>Export current state for saving</summary>
    GraphStateSnapshot ExportState();

    /// <summary>Import saved state (overrides current state)</summary>
    void ImportState(GraphStateSnapshot snapshot);
  }
}