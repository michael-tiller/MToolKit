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
  }
}
