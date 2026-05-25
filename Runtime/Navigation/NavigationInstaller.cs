// Navigation/Installers/NavigationInstaller.cs

using System;
using System.Collections.Generic;
using MessagePipe;
using MToolKit.Runtime.Navigation.Config;
using MToolKit.Runtime.Navigation.DataStructures;
using MToolKit.Runtime.Navigation.Enums;
using MToolKit.Runtime.Navigation.Interfaces;
using MToolKit.Runtime.Navigation.Services;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;


namespace MToolKit.Runtime.Navigation
{
  public class NavigationInstaller : LifetimeScope
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<NavigationInstaller>().ForFeature("Navigation"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    [SerializeField]
    [Required]
    private NavigationCanvasConfig config;

    [SerializeField]
    [Required]
    private CanvasTransformsDict canvasTransformsDict = new();

    public void Install(IContainerBuilder builder)
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Installing Navigation");
      // Invoke this class's override (not LifetimeScope's empty stub) so INavigationService
      // and IModalService land in the parent scope's builder. The prior `base.Configure(builder)`
      // call resolved to the no-op base method and silently dropped both registrations — root
      // cause of the SaveTruncationDialogPresenter never wiring in the game scope.
      Configure(builder);
    }

    protected override void Configure(IContainerBuilder builder)
    {
      // MessagePipeOptions may already be registered by parent scope (GlobalInstaller)
      // Skip re-registration to avoid Singleton conflict

      // Register services
      builder.Register<INavigationService, NavigationService>(Lifetime.Singleton);
      log.ForGameObject(gameObject).ForMethod().Verbose("NavigationInstaller: Registered INavigationService as Singleton.");

      // Register NavigationController as both concrete type and IModalService
      builder.RegisterComponentInHierarchy<NavigationSystem>().AsImplementedInterfaces();

      // Register canvas configs
      if (config == null || config.CanvasConfigDict == null || config.CanvasConfigDict.Count == 0)
      {
        log.ForGameObject(gameObject).ForMethod().Error("canvasConfigs is null or empty during registration.");

        // Still register empty dictionaries to prevent VContainer resolution failures
        CanvasConfigDict emptyCanvasConfigs = new();
        builder.RegisterInstance(emptyCanvasConfigs);
        Dictionary<ECanvasType, Transform> emptyCanvasTransforms = new();
        builder.RegisterInstance(emptyCanvasTransforms);
        return; // Early return to avoid NullReferenceException
      }

      log.ForGameObject(gameObject).ForMethod().Verbose("Registering canvasConfigs with {0} entries.", config.CanvasConfigDict.Count);
      CanvasConfigDict canvasConfigs = config.CanvasConfigDict;
      builder.RegisterInstance(canvasConfigs);
      Dictionary<ECanvasType, Transform> canvasTransforms = new(canvasTransformsDict);
      builder.RegisterInstance(canvasTransforms);
      log.ForGameObject(gameObject).ForMethod().Verbose("NavigationInstaller: Registered canvasConfigs.");
    }

    /// <summary>
    ///   For testing purposes - allows calling Awake behavior without Unity's automatic Awake()
    /// </summary>
    public void TestAwake()
    {
      // Call the base Awake behavior for testing
      base.Awake();
    }
  }
}