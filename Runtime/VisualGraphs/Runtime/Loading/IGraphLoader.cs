using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Runtime.Loading
{
  /// <summary>
  ///   Service for dynamically loading and unloading graph runners.
  ///   Supports both direct references and Addressables.
  /// </summary>
  public interface IGraphLoader
  {
    /// <summary>Load a graph by its ID (from registry), returns the initialized runner</summary>
    UniTask<IGraphRunner> LoadGraphAsync(string graphId, CancellationToken ct = default);

    /// <summary>Unload a graph and cleanup resources</summary>
    void UnloadGraph(string graphId);

    /// <summary>Check if a graph is currently loaded</summary>
    bool IsLoaded(string graphId);

    /// <summary>Get a loaded graph runner by ID</summary>
    IGraphRunner GetRunner(string graphId);
  }
}

