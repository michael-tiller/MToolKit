using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MToolKit.ContextMenus.Presentation
{
  [CreateAssetMenu(menuName = "MToolKit/Context Menus/Transitions/Canvas Group Fade", fileName = "MenuFadeTransition")]
  public sealed class CanvasGroupFadeTransition : MenuTransitionAsset
  {
    [field: SerializeField, Min(0)] public float Duration { get; set; } = .12f;
    public override IMenuTransition Create(RectTransform root)
    {
      if (root == null) throw new ArgumentNullException(nameof(root));
      if (!Validate(out var diagnostic)) throw new ArgumentException(diagnostic, nameof(Duration));
      var group = root.GetComponent<CanvasGroup>();
      if (group == null) group = root.gameObject.AddComponent<CanvasGroup>();
      return new Controller(group, Duration);
    }
    public override bool Validate(out string diagnostic)
    {
      bool valid = !float.IsNaN(Duration) && !float.IsInfinity(Duration) && Duration >= 0;
      diagnostic = valid ? null : "CanvasGroupFadeTransition.Duration must be finite and nonnegative.";
      return valid;
    }

    private sealed class Controller : IMenuTransition
    {
      private readonly CanvasGroup group;
      private readonly float authoredAlpha, duration;
      private long generation;
      private bool disposed;
      internal Controller(CanvasGroup group, float duration)
      { this.group = group; authoredAlpha = group.alpha; this.duration = duration; }

      public async UniTask PlayAsync(MenuTransitionKind kind, CancellationToken cancellationToken)
      {
        if (disposed) throw new ObjectDisposedException(nameof(Controller));
        cancellationToken.ThrowIfCancellationRequested();
        long current = ++generation;
        if (group == null) return;
        if (duration <= 0) { group.alpha = authoredAlpha; return; }
        group.alpha = 0;
        double start = Time.realtimeSinceStartupAsDouble;
        while (true)
        {
          await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
          cancellationToken.ThrowIfCancellationRequested();
          if (disposed || generation != current || group == null) return;
          float progress = Mathf.Clamp01((float)((Time.realtimeSinceStartupAsDouble - start) / duration));
          group.alpha = authoredAlpha * (progress * progress * (3 - 2 * progress));
          if (progress >= 1) return;
        }
      }

      public void CompleteImmediately(MenuTransitionKind kind)
      {
        if (disposed) return;
        generation++;
        if (group != null) group.alpha = authoredAlpha;
      }

      public void Dispose()
      {
        if (disposed) return;
        disposed = true; generation++;
        if (group != null) group.alpha = authoredAlpha;
        // Keep an auto-added, restored group on this presenter. Deferred component destruction could
        // otherwise remove a newer binding's group after rapid unbind/rebind in the same frame.
      }
    }
  }
}
