using System.Threading;
using Cysharp.Threading.Tasks;

namespace MToolKit.Runtime.Persistence.Interfaces
{
  /// <summary>Controller notified after the entire save batch has produced a durable flush receipt.</summary>
  public interface IDurableSaveCompletionHandler
  {
    UniTask OnDurableSaveCompletedAsync(ES3FlushReceipt receipt, CancellationToken ct = default);
  }
}