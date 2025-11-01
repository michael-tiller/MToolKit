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
    }
}