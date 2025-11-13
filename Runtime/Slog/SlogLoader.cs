using UnityEngine;

namespace MToolKit.Runtime.Slog
{
  public static class SlogLoader
  {
    private static SlogConfig loggerConfig;
    public static bool Initialized { get; private set; }

    /// <summary>
    ///   Load Serilog first thing
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnApplicationStart()
    {
      InitializeSerilog();
    }

    /// <summary>
    ///   Initializes the Serilog logger
    /// </summary>
    private static void InitializeSerilog()
    {
      if (Initialized)
        return;

      TryInitializeSerilog();
    }

    /// <summary>
    ///   Only runs once, opens the Serilog config
    /// </summary>
    private static void TryInitializeSerilog()
    {
      if (Initialized)
        return;

      // Create the logger config
      loggerConfig = new SlogConfig();

      // Get or create the flush object
      FlushSlogOnQuit flush = Object.FindFirstObjectByType<FlushSlogOnQuit>();
      if (flush == null)
        CreateFlushSlogOnQuit();

      Initialized = true;
    }

    /// <summary>
    ///   Creates the flush object
    /// </summary>
    private static void CreateFlushSlogOnQuit()
    {
      GameObject flush = new("FlushSlogOnQuit");
      flush.AddComponent<FlushSlogOnQuit>();
    }
  }
}