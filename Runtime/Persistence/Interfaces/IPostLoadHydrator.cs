using System.Threading;
using Cysharp.Threading.Tasks;

namespace MToolKit.Runtime.Persistence.Interfaces
{
  /// <summary>
  ///   Contract for post-load hydrators that participate in
  ///   <see cref="MToolKit.Runtime.Persistence.SaveSystemCoordinator.PostLoadHydrate"/>.
  ///   Implementations run after every <see cref="ISaveDomainController.LoadAsync"/> has completed
  ///   but before the truncation reporter is drained. Hydrators that drop runtime state because
  ///   of cross-reference rot must report each drop to <c>ITruncationReporter</c> using
  ///   <c>TruncationEntry.ReasonLiveReferenceDropped</c>.
  ///   <para>
  ///     Implementing this interface is a marker for the no-op hydrator audit reflection guard;
  ///     it is not a DI resolution target. Plugins must register their hydrators as the
  ///     <i>concrete</i> type only — registering as <c>IPostLoadHydrator</c> would multi-implement
  ///     the interface in DI and make <c>TryResolve&lt;IPostLoadHydrator&gt;</c> ambiguous.
  ///   </para>
  /// </summary>
  public interface IPostLoadHydrator
  {
    UniTask HydrateAsync(CancellationToken ct);
  }

  /// <summary>
  ///   Opt-out marker for <see cref="IPostLoadHydrator"/> implementations that intentionally
  ///   do NOT mutate runtime state (read-only diagnostic or audit hydrators). The no-op hydrator
  ///   audit reflection guard skips implementers of this interface when checking for the
  ///   <c>ITruncationReporter</c> ctor dependency.
  /// </summary>
  public interface INonMutatingHydrator
  {
  }
}
