using System;
using MToolKit.Runtime.Settings.Enums;
using Serilog;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Settings.UI
{
  public class ModalButton : MonoBehaviour
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<ModalButton>().ForFeature("Settings.UI"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;
    [SerializeField][Required] private Button primaryButtonPrefab;
    [SerializeField][Required] private Button secondaryButtonPrefab;
    [SerializeField][Required] private Button positiveButtonPrefab;
    [SerializeField][Required] private Button negativeButtonPrefab;

    [ShowInInspector][ReadOnly] private Button instance;

    public Button Instance => instance;
    
    [ShowInInspector][ReadOnly] private EModalButtonType type;

    public void Setup(EModalButtonType modalButtonType, UnityAction onClick, string text)
    {
      Button buttonPrefab = GetButtonPrefab(modalButtonType);
      type = modalButtonType;
      instance = Instantiate(buttonPrefab, transform);
      instance.onClick.AddListener(onClick);
      if (instance.GetComponentInChildren<TextMeshProUGUI>() is TextMeshProUGUI label)
        label.SetText(text);
    }

    private Button GetButtonPrefab(EModalButtonType modalButtonType)
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Lookup: {0}", modalButtonType);
      switch (modalButtonType)
      {
        case EModalButtonType.Primary: return primaryButtonPrefab;
        case EModalButtonType.Secondary: return secondaryButtonPrefab;
        case EModalButtonType.Positive: return positiveButtonPrefab;
        case EModalButtonType.Negative: return negativeButtonPrefab;
        default:
          log.ForGameObject(gameObject).ForMethod().Error("Unexpected ModalButtonType: {0}", modalButtonType);
          return null;
      }
    }
  }
}