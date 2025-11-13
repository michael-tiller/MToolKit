using System.Threading;
using Cysharp.Threading.Tasks;

namespace MToolKit.Runtime.Bootstrapper.Interfaces
{
  /// <summary>
  ///   This interface provides a higher-level API for loading content during the bootstrapper.
  /// </summary>
  public interface IGameLoader
  {
    UniTask LoadGameAsync(CancellationToken ct = default);
  }
}