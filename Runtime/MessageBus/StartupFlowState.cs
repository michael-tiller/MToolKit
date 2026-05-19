namespace MToolKit.Runtime.MessageBus
{
  public enum StartupFlowKind
  {
    Unknown = 0,
    NewGame = 1,
    Continue = 2,
  }

  /// <summary>
  ///   Static carrier for the pending startup flow kind. Set by menu paths just before
  ///   the gameplay scene load; consumed (and cleared) by the startup profiler when it
  ///   begins its timing run. Static rather than MessageBus so it's robust against
  ///   broker registration ordering and scene-transition lifecycles.
  /// </summary>
  public static class StartupFlowState
  {
    public static StartupFlowKind Pending { get; set; } = StartupFlowKind.Unknown;

    /// <summary>
    ///   Stable string ID of the scenario the player picked on the new-game screen. Set by
    ///   the scenario picker view just before the gameplay scene loads; consumed (and
    ///   cleared) by the orchestrator when it spawns starting colonists. Empty/null falls
    ///   back to the orchestrator's serialized scenario default.
    /// </summary>
    public static string PendingScenarioId { get; set; }
  }
}
