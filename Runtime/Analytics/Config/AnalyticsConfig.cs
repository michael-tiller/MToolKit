using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Namespace for analytics configuration.
/// </summary>
namespace MToolKit.Runtime.Analytics.Config
{

[CreateAssetMenu(fileName = "AnalyticsConfig", menuName = "MToolKit/Analytics Config")]
[InlineEditor]
/// <summary>
/// Configuration for analytics services.
/// </summary>
public class AnalyticsConfig : ScriptableObject
{
    [BoxGroup("General")]
    [SerializeField] private bool enableOnStartup = true;
    
    [BoxGroup("General")]
    [SerializeField] private bool requireConsentBeforeSending = true;
    
    [BoxGroup("GameAnalytics")]
    [SerializeField] private bool enableGameAnalytics = true;
    
    [BoxGroup("Privacy")]
    [SerializeField] private bool enableATT = true;
    
    [BoxGroup("Privacy")]
    [SerializeField] private bool enableGDPR = true;

    public bool EnableOnStartup => enableOnStartup;
    public bool RequireConsentBeforeSending => requireConsentBeforeSending;
    public bool EnableGameAnalytics => enableGameAnalytics;
    public bool EnableATT => enableATT;
    public bool EnableGDPR => enableGDPR;
    
    // Keys loaded from environment variables at runtime
    public string GameKey => System.Environment.GetEnvironmentVariable("GA_GAME_KEY") ?? "";
        public string SecretKey => System.Environment.GetEnvironmentVariable("GA_SECRET_KEY") ?? "";
    }
}
