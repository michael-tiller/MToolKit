using System;
using MToolKit.Runtime.Navigation.Views;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Settings.UI
{
  [RequireComponent(typeof(Button))]
  public class SubviewButton : MonoBehaviour
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<SubviewButton>().ForFeature("Settings.UI"));

    private static ILogger log => logLazy.Value ?? Logger.None;
    
    [SerializeField]
    [Required]
    private SubviewManager subviewManager;

    [SerializeField]
    private Subview targetSubview;

    [SerializeField]
    [Required]
    private Button button;

    private void Reset()
    {
      button = GetComponent<Button>();
    }

    private void Start()
    {
      button.onClick.RemoveListener(OnClickSetSubview);
      button.onClick.AddListener(OnClickSetSubview);

      subviewManager.OnSubviewChanged -= OnSubviewChangedHandler;
      subviewManager.OnSubviewChanged += OnSubviewChangedHandler;
      OnSubviewChangedHandler(subviewManager.LastSubview);
    }

    private void OnDestroy()
    {
      button.onClick.RemoveListener(OnClickSetSubview);
      subviewManager.OnSubviewChanged -= OnSubviewChangedHandler;
    }

    private void OnSubviewChangedHandler(Subview subview)
    {
      //button.interactable = subview != targetSubview;
    }

    private void OnClickSetSubview()
    {
      if (targetSubview == null)
      {
        log.Warning("Target subview is null");
        return;
      }
      subviewManager.ShowSubview(targetSubview);
    }
  }
}