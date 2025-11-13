// Navigation/Views/View.cs

using System;
using MToolKit.Runtime.Navigation.Enums;
using MToolKit.Runtime.Navigation.Interfaces;
using Serilog;
using UnityEngine;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Navigation.Views
{
  public abstract class View : MonoBehaviour, IView
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<View>().ForFeature("Navigation.Views"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public GameObject GameObject => gameObject;

    [field: SerializeField]
    public ECanvasType Canvas { get; private set; } = ECanvasType.None;

    [field: SerializeField]
    public bool SelfDestruct { get; private set; } = true;

    [Inject]
    protected INavigationService navigationService { get; set; }

    public virtual void Show()
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Show() called on {0}, setting active to true", GetType().Name);
      gameObject.SetActive(true);
      log.ForGameObject(gameObject).ForMethod().Verbose("Show() completed on {0}, active state: {1}", GetType().Name, gameObject.activeInHierarchy);
    }

    public virtual void Hide()
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Hide() called on {0}, active state: {1}", GetType().Name, gameObject.activeInHierarchy);
      if (navigationService != null)
        navigationService.Cleanup(Canvas, this);
      if (gameObject != null) log.ForGameObject(gameObject).ForMethod().Verbose("Hide() completed on {0}, active state: {1}", GetType().Name, gameObject.activeInHierarchy);
    }
  }
}