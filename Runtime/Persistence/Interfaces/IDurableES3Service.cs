using System.Threading;
using Cysharp.Threading.Tasks;

namespace MToolKit.Runtime.Persistence.Interfaces
{
  /// <summary>ES3 backend that exposes save identity and an awaited durable file-flush receipt.</summary>
  public interface IDurableES3Service : IES3Service
  {
    ES3SaveContext GetSaveContext();
    UniTask<ES3FlushReceipt> FlushSaveBatchAsync(CancellationToken ct = default);
  }
}
