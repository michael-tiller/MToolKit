using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.AssetLoader.Interfaces;
using MToolKit.Runtime.Core.Abstractions;
using MToolKit.Runtime.Input.Interfaces;
using MToolKit.Runtime.Input.Prompts;
using MToolKit.Theme;
using UnityEngine;
using VContainer;

namespace MToolKit.ContextMenus.Hosting
{
  [AddComponentMenu("MToolKit/Plugins/Context Menus")]
  public sealed class ContextMenuPlugin : ConfigPlugin<ContextMenuHostService, IContextMenuHostService, MenuHostConfig>
  {
    public override IEnumerable<Type> OptionalServices => new[]
    {
      typeof(IRuntimeAssetService), typeof(IInputActionSource), typeof(IInputPromptService),
      typeof(IInputRebinderService), typeof(IThemeService)
    };

    protected override ContextMenuHostService CreateService(IObjectResolver resolver)
    {
      resolver.TryResolve(out IRuntimeAssetService assets);
      resolver.TryResolve(out IInputActionSource actions);
      resolver.TryResolve(out IInputPromptService prompts);
      resolver.TryResolve(out IThemeService themes);
      resolver.TryResolve(out IInputRebinderService rebinder);
      return new ContextMenuHostService(config, assets, actions, prompts, themes, rebinder);
    }

    public override async UniTask PerformRuntimeInitializationAsync(IObjectResolver resolver, CancellationToken ct = default)
    {
      PerformRuntimeInitialization(resolver);
      if (service != null && config.DefaultProfile != null)
        await service.PrewarmAsync(config.DefaultProfile, ct);
    }
  }
}
