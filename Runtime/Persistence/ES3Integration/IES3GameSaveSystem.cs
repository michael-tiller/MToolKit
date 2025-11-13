using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace MToolKit.Runtime.Persistence.ES3Integration
{
    /// <summary>
    ///   Interface for ES3-based game save system.
    ///   Provides reactive save/load operations with domain controller coordination.
    /// </summary>
    public interface IES3GameSaveSystem
  {
        /// <summary>
        ///   Reactive property indicating if a save operation is currently in progress.
        /// </summary>
        ReactiveProperty<bool> IsSaving { get; }

        /// <summary>
        ///   Reactive property indicating if a load operation is currently in progress.
        /// </summary>
        ReactiveProperty<bool> IsLoading { get; }

        /// <summary>
        ///   Reactive property containing the timestamp of the last successful save operation.
        /// </summary>
        ReactiveProperty<string> LastSaveTime { get; }

        /// <summary>
        ///   Reactive property containing the timestamp of the last successful load operation.
        /// </summary>
        ReactiveProperty<string> LastLoadTime { get; }

        /// <summary>
        ///   Reactive property containing the number of times the game has been saved.
        /// </summary>
        ReactiveProperty<int> SaveCounter { get; }

        /// <summary>
        ///   Saves all game data asynchronously using ES3.
        /// </summary>
        /// <param name="ct">Cancellation token for the operation</param>
        /// <returns>UniTask for the save operation</returns>
        UniTask SaveAsync(CancellationToken ct = default);

        /// <summary>
        ///   Loads all game data asynchronously using ES3.
        /// </summary>
        /// <param name="ct">Cancellation token for the operation</param>
        /// <returns>UniTask for the load operation</returns>
        UniTask LoadAsync(CancellationToken ct = default);

        /// <summary>
        ///   Gets the number of registered save domain controllers.
        /// </summary>
        /// <returns>Number of controllers</returns>
        int GetControllerCount();
  }
}