using UnityEngine;

namespace MToolKit.Runtime.Core
{
  /// <summary>
  ///   Provides access to the current build environment (dev/stage/prod).
  ///   Environment is determined from ENVIRONMENT or MT_ENVIRONMENT environment variables.
  ///   Note: Unity Editor is always treated as non-production, regardless of environment variable.
  ///   Game code should generally not behave differently based on environment,
  ///   but this helper is available for cases where environment-specific behavior is needed
  ///   (e.g., logging verbosity, debug features, analytics endpoints).
  /// </summary>
  public static class BuildEnvironment
  {
    private static string _environment;

    /// <summary>
    ///   Gets the current environment identifier (dev/stage/prod/default).
    ///   Cached after first access.
    ///   Note: In Unity Editor, this will never return "prod" - it will return "dev" or the actual value.
    /// </summary>
    public static string Environment
    {
      get
      {
        if (_environment == null)
        {
          // Check scripting defines first (set at build time, no .env file needed)
#if MT_ENVIRONMENT_PROD
          _environment = "prod";
#elif MT_ENVIRONMENT_STAGE
          _environment = "stage";
#elif MT_ENVIRONMENT_DEV
          _environment = "dev";
#else
          // Fallback to environment variables (for editor/development)
          string envValue = System.Environment.GetEnvironmentVariable("ENVIRONMENT") ??
                           System.Environment.GetEnvironmentVariable("MT_ENVIRONMENT") ??
                           "default";

          // Unity Editor should never be treated as production
          // Override to "dev" if editor and value is "prod"
#if UNITY_EDITOR
          if (envValue.Equals("prod", System.StringComparison.OrdinalIgnoreCase))
          {
            _environment = "dev";
          }
          else
          {
            _environment = envValue;
          }
#else
          _environment = envValue;
#endif
#endif
        }
        return _environment;
      }
    }

    /// <summary>
    ///   Returns true if the current environment is production.
    ///   Note: Always returns false in Unity Editor, even if ENVIRONMENT=prod.
    /// </summary>
    public static bool IsProduction => !Application.isEditor &&
                                       Environment.Equals("prod", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///   Returns true if the current environment is development.
    /// </summary>
    public static bool IsDevelopment => Environment.Equals("dev", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///   Returns true if the current environment is staging.
    /// </summary>
    public static bool IsStaging => Environment.Equals("stage", System.StringComparison.OrdinalIgnoreCase) ||
                                    Environment.Equals("staging", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///   Returns true if the current environment is not production.
    ///   Useful for enabling debug features, verbose logging, etc.
    /// </summary>
    public static bool IsNonProduction => !IsProduction;
  }
}

