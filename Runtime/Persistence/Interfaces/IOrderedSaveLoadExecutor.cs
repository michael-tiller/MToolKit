using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace MToolKit.Runtime.Persistence.Interfaces
{
  /// <summary>
  /// Executes the validated, caller-owned controller order for one load transaction.
  /// This narrow seam keeps application transaction policy testable without exposing a
  /// concrete save backend or permitting callers to fall back to registry insertion order.
  /// </summary>
  public interface IOrderedSaveLoadExecutor
  {
    int LoadGeneration { get; }

    UniTask<IReadOnlyList<string>> LoadAsync(
      IReadOnlyList<string> orderedControllerTypeNames,
      Func<string, bool> isRequired,
      CancellationToken ct = default);
  }
}
