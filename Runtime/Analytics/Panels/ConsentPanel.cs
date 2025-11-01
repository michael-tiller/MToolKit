using UnityEngine;
using UnityEngine.UI;
using VContainer;
using MToolKit.Runtime.Settings.Game;

/// <summary>
/// Namespace for analytics panels.
/// </summary>
namespace MToolKit.Runtime.Analytics.Panels
{

/// <summary>
/// Panel for managing user consent for analytics.
/// </summary>
public sealed class ConsentPanel : MonoBehaviour
{
    [Inject] private IGameSettings gameSettings;
    [SerializeField] private Button acceptButton;

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