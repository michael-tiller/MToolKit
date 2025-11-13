using MToolKit.Runtime.Settings.Game;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MToolKit.Runtime.Analytics.Panels
{
  /// <summary>
  ///   Namespace for analytics panels.
  /// </summary>
  internal sealed class NameSpaceDoc { }

  /// <summary>
  ///   Panel for managing user consent for analytics.
  /// </summary>
  public sealed class ConsentPanel : MonoBehaviour
  {
    [SerializeField]
    private Button acceptButton;

    [Inject]
    private IGameSettings gameSettings;

    private void Awake()
    {
      acceptButton.onClick.AddListener(OnAccept);
    }

    private void OnAccept()
    {
      gameSettings.AnalyticsEnabled.Value = true;
      gameObject.SetActive(false);
    }
  }
}