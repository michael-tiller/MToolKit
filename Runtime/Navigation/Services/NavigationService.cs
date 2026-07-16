// Navigation/Services/NavigationService.cs

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Navigation.Enums;
using MToolKit.Runtime.Navigation.Interfaces;
using MToolKit.Runtime.Navigation.Views;
using Serilog;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;


namespace MToolKit.Runtime.Navigation.Services
{
  public class NavigationService : INavigationService
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<NavigationService>().ForFeature("Navigation.Services"));
    private static ILogger log => logLazy.Value ?? Logger.None;
    private readonly IObjectResolver container;
    private readonly Dictionary<ECanvasType, Stack<IView>> stacks;
    private readonly Dictionary<ECanvasType, Transform> canvases;

    public NavigationService(IObjectResolver container, Dictionary<ECanvasType, Transform> canvases)
    {
      this.container = container;
      this.canvases = canvases;
      stacks = new Dictionary<ECanvasType, Stack<IView>>();

      foreach (ECanvasType type in Enum.GetValues(typeof(ECanvasType)))
        stacks[type] = new Stack<IView>();
    }

    public async UniTask<T> PushAsync<T>(ECanvasType canvasType, T prefab, CancellationToken token = default)
      where T : View
    {
      // A pushed view can only render if its canvas GameObject is active. Some canvases
      // (e.g. the Overlay canvas) sit dormant during gameplay and must wake when a modal
      // is pushed onto them. Activation only — never deactivate here (other canvas types
      // share this path and may be expected to stay active when their stack is empty).
      var canvasGo = canvases[canvasType]?.gameObject;
      if (canvasGo != null && !canvasGo.activeSelf) canvasGo.SetActive(true);

      // Hide the old top (if any)
      if (stacks[canvasType].Count > 0)
      {
        IView oldTop = stacks[canvasType].Peek();
        oldTop?.Hide();
      }

      string targetName = prefab.name.Replace("(Clone)", "");

      // --- 1) Search the stack for an existing view with same prefab name ---
      // Iterate stack directly to avoid ToArray() allocation in hot path
      // Stack enumerator iterates top-to-bottom (LIFO), which matches ToArray() behavior
      T foundView = null;
      var stack = stacks[canvasType];

      foreach (var item in stack)
      {
        if (item is T candidate)
        {
          string candidateName = candidate.gameObject.name.Replace("(Clone)", "");
          if (candidateName == targetName)
          {
            foundView = candidate;
            break;
          }
        }
      }

      // --- 2) If found, move that existing view to top (pop everything above it) ---
      if (foundView != null)
      {
        Stack<IView> tempStack = new();

        // Pop until we get to our found item
        while ((T)stacks[canvasType].Peek() != foundView)
          tempStack.Push(stacks[canvasType].Pop());

        // Now 'foundView' is on top
        foundView.Show();

        // Optionally re‐push hidden views above it 
        // If you want them truly removed, skip this part
        while (tempStack.Count > 0)
        {
          IView hiddenView = tempStack.Pop();
          hiddenView.Hide();
          stacks[canvasType].Push(hiddenView);
        }

        return foundView;
      }

      // --- 3) Otherwise, instantiate new ---
      T newView = container.Instantiate(prefab, canvases[canvasType].transform);
      newView.name = newView.name.Replace("(Clone)", "");

      container.InjectGameObject(newView.gameObject);

      stacks[canvasType].Push(newView);
      newView.Show();

      await UniTask.Yield(token);
      return newView;
    }

    public async UniTask PopAsync(ECanvasType canvasType, CancellationToken token = default)
    {
      log.ForMethod().Debug("PopAsync called for canvas: {0}", canvasType);

      if (stacks[canvasType].Count == 0)
      {
        log.ForMethod().Warning("Stack is empty for canvas: {0}", canvasType);
        return;
      }

      log.ForMethod().Verbose("Stack count before pop: {0}", stacks[canvasType].Count);

      IView currentView = stacks[canvasType].Peek();
      log.ForMethod().Verbose("Current view to hide: {0}", currentView?.GetType().Name ?? "NULL");

      currentView?.Hide();
      stacks[canvasType].Pop(); // Actually remove the view from the stack

      log.ForMethod().Verbose("Stack count after pop: {0}", stacks[canvasType].Count);

      if (stacks[canvasType].Count > 0)
      {
        IView nextView = stacks[canvasType].Peek();
        log.ForMethod().Verbose("Next view to show: {0}", nextView?.GetType().Name ?? "NULL");
        nextView?.Show();
        log.ForMethod().Verbose("Called Show() on next view");
      }
      else
      {
        log.ForMethod().Verbose("No next view to show - stack {0} is empty", canvasType);
      }

      await UniTask.Yield();
    }

    public async UniTask ClearStackAsync(ECanvasType canvasType, CancellationToken token = default)
    {
      while (stacks[canvasType].Count > 0)
        await PopAsync(canvasType, token);
    }

    public bool TryPeek(ECanvasType canvasType, out IView view)
    {
      if (stacks[canvasType].Count > 0)
      {
        view = stacks[canvasType].Peek();
        return true;
      }

      view = null;
      return false;
    }

    /// <summary>
    ///   Remove an entry from the stack
    /// </summary>
    /// <param name="canvas"></param>
    /// <param name="view"></param>
    public void Cleanup(ECanvasType canvas, View view)
    {
      if (view == null || view.gameObject == null) return;

      if (view.SelfDestruct)
        Object.Destroy(view.gameObject);
      else
        view.gameObject.SetActive(false);
    }
  }
}