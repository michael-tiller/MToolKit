using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus.Authoring;
using UnityEngine;

namespace MToolKit.ContextMenus.Samples
{
  [CreateAssetMenu(menuName = "MToolKit/Samples/Action Menus/Dynamic Stock Provider")]
  public sealed class GalleryChildrenProviderSO : MenuChildrenProviderSO
  {
    [field: SerializeField] public string IdPrefix { get; set; } = "sample.dynamic";
    [field: SerializeField] public Sprite Icon { get; set; }
    [field: SerializeField] public Sprite Image { get; set; }
    public override async UniTask<IReadOnlyList<MenuAction>> ResolveAsync(MenuContext context, CancellationToken cancellationToken)
    {
      // A genuine asynchronous boundary demonstrates the framework's stale-result and cancellation guards.
      await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
      cancellationToken.ThrowIfCancellationRequested();
      if (context == null || !context.IsValid || !(context.Target is MenuGalleryState model) || !model.IsValid)
        return Array.Empty<MenuAction>();
      var actions = new List<MenuAction>();
      foreach (var entry in model.Stock)
      {
        if (entry.Remaining <= 0) continue;
        var captured = entry;
        actions.Add(new MenuAction(IdPrefix + "/" + entry.Id, new MenuLabel(entry.Label),
          new MenuCommandBehavior(current => (current.Target as MenuGalleryState)?.Take(captured), MenuActionOutcome.Refresh),
          state: current => new MenuActionState(captured.Remaining > 0,
            current.Target is MenuGalleryState state && state.IsValid && state.CommandsEnabled && captured.Remaining > 0,
            disabledReason: "No stock available or commands disabled"),
          description: new MenuLabel("Take one item. Exhausted choices disappear without retaining a stale selection."),
          icon: Icon, metadata: new MenuGalleryState.Details(model, "Available: " + entry.Remaining), image: Image));
      }
      return actions.AsReadOnly();
    }
  }
}
