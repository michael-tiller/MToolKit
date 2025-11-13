using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.VisualGraphs.Runtime.State;

namespace MToolKit.Runtime.VisualGraphs.Runtime.Interfaces
{
  /// <summary>
  ///   Runs a single graph instance with state management.
  /// </summary>
  public interface IGraphRunner
  {
    /// <summary>Graph identifier</summary>
    string GraphId { get; }

    /// <summary>Graph domain</summary>
    string GraphDomain { get; }

    /// <summary>Runtime graph definition</summary>
    IRuntimeGraphDefinition Definition { get; }

    /// <summary>Check if this runner can handle the event</summary>
    bool CanHandle(IEventMessage message);

    /// <summary>Handle an event (idempotent, will ignore already-processed sequence IDs)</summary>
    UniTask HandleEventAsync(IEventMessage message, CancellationToken ct = default);

    /// <summary>Export current state for saving</summary>
    GraphStateSnapshot ExportState();

    /// <summary>Import saved state (overrides current state)</summary>
    void ImportState(GraphStateSnapshot snapshot);
  }
}