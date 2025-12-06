using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.Analytics.Config
{
  /// <summary>
  ///   Namespace for analytics configuration.
  /// </summary>
  internal sealed class NameSpaceDoc { }

  /// <summary>
  ///   Configuration for analytics services.
  /// </summary>
  [CreateAssetMenu(fileName = "AnalyticsConfig", menuName = "MToolKit/Analytics Config")]
  [InlineEditor]
  public class AnalyticsConfig : ScriptableObject
  {
    [BoxGroup("General")]
    [SerializeField]
    private bool enableOnStartup = true;

    [BoxGroup("General")]
    [SerializeField]
    private bool requireConsentBeforeSending = true;

    [BoxGroup("GameAnalytics")]
    [SerializeField]
    private bool enableGameAnalytics = true;

    [BoxGroup("Privacy")]
    [SerializeField]
    private bool enableAtt = true;

    [BoxGroup("Privacy")]
    [SerializeField]
    private bool enableGdpr = true;

    public bool EnableOnStartup => enableOnStartup;
    public bool RequireConsentBeforeSending => requireConsentBeforeSending;
    public bool EnableGameAnalytics => enableGameAnalytics;
    public bool EnableAtt => enableAtt;
    public bool EnableGdpr => enableGdpr;
  }
}