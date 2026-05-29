using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Persistence.Enums;

namespace MToolKit.Runtime.Persistence.Interfaces
{
  public interface ISaveDomainController
  {
    ESaveDomain Domain { get; }
    UniTask SaveAsync(CancellationToken ct = default);
    UniTask LoadAsync(CancellationToken ct = default);

    // Returns true when persisted state exists for this domain. The coordinator
    // aggregates this across controllers so callers can branch NEW vs LOAD at
    // session boot without coupling to any single domain.
    bool HasSaveData();
  }
}