using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Persistence.Interfaces;

namespace MToolKit.Runtime.Persistence.ES3Integration
{
  /// <summary>Orders Save As cloning after the source has an awaited durable checkpoint.</summary>
  public static class DurableSaveAsWorkflow
  {
    public static async UniTask<TClone> SaveBeforeCloneAsync<TClone>(
      Func<CancellationToken, UniTask<ES3FlushReceipt>> saveSource,
      Func<CancellationToken, UniTask<TClone>> clone,
      CancellationToken ct = default)
    {
      if (saveSource == null) throw new ArgumentNullException(nameof(saveSource));
      if (clone == null) throw new ArgumentNullException(nameof(clone));
      ct.ThrowIfCancellationRequested();
      ES3FlushReceipt receipt = await saveSource(ct);
      if (receipt == null) throw new InvalidOperationException("Save As requires a durable source checkpoint receipt.");
      ct.ThrowIfCancellationRequested();
      return await clone(ct);
    }
  }
}