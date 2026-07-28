namespace MToolKit.Runtime.MessageBus
{
  /// <summary>
  ///   Execution band for a message interceptor. Bands run ascending; within a band, registration order is
  ///   execution order.
  ///
  ///   <para>Values are sparse on purpose — a consumer with its own band vocabulary maps onto these with a
  ///   cast rather than a switch.</para>
  /// </summary>
  public enum HandlerPriority
  {
    /// <summary>Runs first. Conventionally defensive interceptors — vetoes, guards, clamps.</summary>
    First = 0,

    /// <summary>Default band. Conventionally neutral interceptors — logging, tracking, observation.</summary>
    Normal = 100,

    /// <summary>Runs last. Conventionally offensive or amplifying interceptors.</summary>
    Last = 200
  }
}
