// Navigation/Config/CanvasConfig.cs

using System;
using System.Collections.Generic;
using MToolKit.Runtime.Navigation.Enums;
using MToolKit.Runtime.Navigation.Views;
using Serilog;
using UnityEngine;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Navigation.Config
{
  [Serializable]
  public class CanvasConfig
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<CanvasConfig>().ForFeature("Navigation.Config"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;
    [SerializeField] private View initialViewPrefab;

    [SerializeField] private List<ViewConfig> views = new();


    public View InitialViewPrefab => initialViewPrefab;
    public List<ViewConfig> Views => views;

    /// <summary>
    ///   Method to find a prefab by type
    /// </summary>
    /// <typeparam name="T">The type of View</typeparam>
    /// <returns>The view if found</returns>
    public View GetViewPrefab<T>() where T : View
    {
      foreach (ViewConfig viewConfig in views)
        if (viewConfig.ViewPrefab is T)
          return viewConfig.ViewPrefab;

      log.ForMethod().Error("View of type {0} not found in CanvasConfig.", typeof(T).Name);
      return null;
    }

    /// <summary>
    ///   Validation method to ensure canvasType is not ECanvasType.None.
    /// </summary>
    /// <param name="type">The canvas type to validate.</param>
    /// <returns>True if valid; otherwise, false.</returns>
    private bool ValidateCanvasType(ECanvasType type)
    {
      if (type == ECanvasType.None) return false;
      return true;
    }
  }
}