using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MToolKit.ContextMenus.Presentation
{
  public enum MenuTransitionKind { Open, Navigation }

  /// <summary>Per-presenter visual ownership. Implementations observe cancellation and restore owned state on disposal.</summary>
  public interface IMenuTransition : IDisposable
  {
    UniTask PlayAsync(MenuTransitionKind kind, CancellationToken cancellationToken);
    void CompleteImmediately(MenuTransitionKind kind);
  }

  /// <summary>Reusable transition configuration; mutable visual state belongs to the created controller.</summary>
  public abstract class MenuTransitionAsset : ScriptableObject
  {
    public abstract IMenuTransition Create(RectTransform root);
    public virtual bool Validate(out string diagnostic) { diagnostic = null; return true; }
  }
}
