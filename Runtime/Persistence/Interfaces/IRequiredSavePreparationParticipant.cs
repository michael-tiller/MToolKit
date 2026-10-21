using System.Threading;
using Cysharp.Threading.Tasks;

namespace MToolKit.Runtime.Persistence.Interfaces
{
  /// <summary>Required asynchronous preparation before controller save snapshots or writes.</summary>
  public interface IRequiredSavePreparationParticipant
  {
    /// <summary>Stable identifier used to order participants with the same priority.</summary>
    string ParticipantId { get; }

    /// <summary>Lower priorities prepare first, followed by ordinal participant identifier.</summary>
    int Order { get; }

    /// <summary>Quiesces live state and transfers admission leases to the preparation context.</summary>
    UniTask PrepareForSaveAsync(SavePreparationContext context, CancellationToken ct);
  }
}
