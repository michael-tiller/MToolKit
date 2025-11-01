using UnityEngine;
using static UnityEngine.GameObject;

namespace MToolKit.Runtime.Slog
{
  public static class SlogLoader
  {
    public static bool Initialized { get; private set; }

    private static SlogConfig loggerConfig;

    /// <summary>
    /// Load Serilog first thing
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnApplicationStart()
    {
      InitializeSerilog();
    }

    /// <summary>
    /// Initializes the Serilog logger
    /// </summary>
    private static void InitializeSerilog()
    {
      if (Initialized)
        return;

      TryInitializeSerilog();
    }

    /// <summary>
    /// Only runs once, opens the Serilog config
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
    /// Creates the flush object
    /// </summary>
    private static void CreateFlushSlogOnQuit()
    {
      GameObject flush = new GameObject("FlushSlogOnQuit");
      flush.AddComponent<FlushSlogOnQuit>();
    }
  }
}