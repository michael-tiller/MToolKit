using System;
using System.Collections.Generic;
using MToolKit.ContextMenus.Hosting;
using MToolKit.ContextMenus.Input;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuProfileActionSourceTests
  {
    [Test]
    public void NoSourceAndNoProfileActionsRequireNoSyntheticInputService()
    {
      using var owner = new MenuProfileActionSource(null);
      owner.Prepare(new MenuInputBindings());
      Assert.That(owner.ActiveSource, Is.Null);
    }

    [Test]
    public void ForeignProfileAssetsAreClonedOnceAndRetainActionIdsWithoutChangingAuthoredBindings()
    {
      using var fixture = new Fixture();
      var authored = fixture.Action("Profile", "Submit", "<Keyboard>/enter");
      var bindings = new MenuInputBindings(); bindings.Configure(submit: fixture.Reference(authored));
      using var owner = new MenuProfileActionSource(null);
      owner.Prepare(bindings);
      var execution = owner.ActiveSource.ResolveRuntimeAction(authored);
      Assert.That(execution, Is.Not.SameAs(authored));
      Assert.That(execution.id, Is.EqualTo(authored.id));
      Assert.That(execution.actionMap.asset, Is.SameAs(owner.ActiveSource.RuntimeActions));
      owner.Prepare(bindings);
      Assert.That(owner.ActiveSource.ResolveRuntimeAction(authored), Is.SameAs(execution), "Profiles in the same host reuse their owned runtime clone.");
      execution.ApplyBindingOverride(0, "<Keyboard>/q");
      Assert.That(authored.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/enter"));
      Assert.That(authored.enabled, Is.False);
    }

    [Test]
    public void GlobalSourceResolvesItsExactAuthoredAssetWithoutAnotherClone()
    {
      using var fixture = new Fixture();
      var authored = fixture.Action("Game", "Submit", "<Keyboard>/enter");
      var runtime = fixture.Clone(authored.actionMap.asset);
      var existing = new ExistingSource(authored.actionMap.asset, runtime);
      var bindings = new MenuInputBindings(); bindings.Configure(submit: fixture.Reference(authored));
      using var owner = new MenuProfileActionSource(existing);
      owner.Prepare(bindings);
      Assert.That(owner.ActiveSource, Is.SameAs(existing));
      Assert.That(owner.ActiveSource.ResolveRuntimeAction(authored), Is.SameAs(runtime.FindAction(authored.id)));
    }

    [Test]
    public void ReferencesToTheProvidedRuntimeAssetRemainTheExactExecutionActions()
    {
      using var fixture = new Fixture();
      var authored = fixture.Action("Game", "Submit", "<Keyboard>/enter");
      var runtime = fixture.Clone(authored.actionMap.asset);
      var execution = runtime.FindAction(authored.id);
      var bindings = new MenuInputBindings(); bindings.Configure(submit: fixture.Reference(execution));
      using var owner = new MenuProfileActionSource(new ExistingSource(authored.actionMap.asset, runtime));
      owner.Prepare(bindings);
      Assert.That(owner.ActiveSource.ResolveRuntimeAction(execution), Is.SameAs(execution));
    }

    [Test]
    public void ForeignMenuResolutionPreservesSharedGameActionsAndUnregisteredForeignIdentity()
    {
      using var fixture = new Fixture();
      var gameplay = fixture.Action("Game", "Attack", "<Keyboard>/space");
      var gameRuntime = fixture.Clone(gameplay.actionMap.asset);
      var existing = new ExistingSource(gameplay.actionMap.asset, gameRuntime);
      var menu = fixture.Action("ForeignMenu", "Submit", "<Keyboard>/enter");
      var unrelated = fixture.Action("OtherSystem", "Attack", "<Keyboard>/f");
      var unregisteredSibling = menu.actionMap.AddAction("OtherWidget", InputActionType.Button, "<Keyboard>/g");
      var bindings = new MenuInputBindings(); bindings.Configure(submit: fixture.Reference(menu));
      using var owner = new MenuProfileActionSource(existing);
      owner.Prepare(bindings);
      Assert.That(owner.ActiveSource.ResolveRuntimeAction(menu), Is.Not.SameAs(menu));
      Assert.That(owner.ActiveSource.ResolveRuntimeAction(gameplay), Is.SameAs(gameRuntime.FindAction(gameplay.id)));
      Assert.That(owner.ActiveSource.ResolveRuntimeAction(unrelated), Is.SameAs(unrelated));
      Assert.That(owner.ActiveSource.ResolveRuntimeAction(unregisteredSibling), Is.SameAs(unregisteredSibling));
      Assert.That(owner.ActiveSource.RuntimeActions, Is.SameAs(gameRuntime));
      Assert.That(gameplay.enabled, Is.False); Assert.That(unrelated.enabled, Is.False);
    }

    [Test]
    public void OneProfileCanResolveTwoForeignAssetsWithoutCloningAnythingElse()
    {
      using var fixture = new Fixture();
      var submit = fixture.Action("ConfirmAsset", "Submit", "<Keyboard>/enter");
      var back = fixture.Action("CancelAsset", "Back", "<Keyboard>/escape");
      var bindings = new MenuInputBindings(); bindings.Configure(submit: fixture.Reference(submit), back: fixture.Reference(back));
      using var owner = new MenuProfileActionSource(null);
      owner.Prepare(bindings);
      var submitRuntime = owner.ActiveSource.ResolveRuntimeAction(submit);
      var backRuntime = owner.ActiveSource.ResolveRuntimeAction(back);
      Assert.That(submitRuntime.id, Is.EqualTo(submit.id)); Assert.That(backRuntime.id, Is.EqualTo(back.id));
      Assert.That(submitRuntime.actionMap.asset, Is.Not.SameAs(backRuntime.actionMap.asset));
      Assert.That(submitRuntime, Is.Not.SameAs(submit)); Assert.That(backRuntime, Is.Not.SameAs(back));
    }

    [Test]
    public void DisposalRetiresOnlyOwnedClonesAndCannotMutateTheSharedGameRuntime()
    {
      using var fixture = new Fixture();
      var game = fixture.Action("Game", "Attack", "<Keyboard>/space");
      var gameRuntime = fixture.Clone(game.actionMap.asset); gameRuntime.FindAction(game.id).Enable();
      var menu = fixture.Action("ForeignMenu", "Submit", "<Keyboard>/enter");
      var bindings = new MenuInputBindings(); bindings.Configure(submit: fixture.Reference(menu));
      var owner = new MenuProfileActionSource(new ExistingSource(game.actionMap.asset, gameRuntime));
      owner.Prepare(bindings);
      var source = owner.ActiveSource;
      var ownedAsset = source.ResolveRuntimeAction(menu).actionMap.asset;
      owner.Dispose(); owner.Dispose();
      Assert.That(ownedAsset == null, Is.True, "EditMode disposal must destroy the host's runtime clone.");
      Assert.That(gameRuntime != null, Is.True);
      Assert.That(gameRuntime.FindAction(game.id).enabled, Is.True);
      Assert.That(menu.actionMap.asset != null, Is.True);
      Assert.That(menu.enabled, Is.False);
      Assert.Throws<ObjectDisposedException>(() => source.ResolveRuntimeAction(menu));
    }

    private sealed class ExistingSource : IInputActionSource
    {
      private readonly InputActionAsset original;
      public ExistingSource(InputActionAsset original, InputActionAsset runtime) { this.original = original; RuntimeActions = runtime; }
      public InputActionAsset RuntimeActions { get; }
      public InputAction ResolveRuntimeAction(InputAction action)
        => action?.actionMap?.asset == original ? RuntimeActions.FindAction(action.id) : action;
    }

    private sealed class Fixture : IDisposable
    {
      private readonly List<InputActionAsset> assets = new();
      private readonly List<InputActionReference> references = new();
      public InputAction Action(string assetName, string actionName, string binding)
      {
        var asset = ScriptableObject.CreateInstance<InputActionAsset>(); asset.name = assetName; assets.Add(asset);
        return asset.AddActionMap("Menu").AddAction(actionName, InputActionType.Button, binding);
      }
      public InputActionAsset Clone(InputActionAsset original) { var clone = Object.Instantiate(original); assets.Add(clone); return clone; }
      public InputActionReference Reference(InputAction action) { var reference = InputActionReference.Create(action); references.Add(reference); return reference; }
      public void Dispose()
      {
        foreach (var reference in references) Object.DestroyImmediate(reference);
        foreach (var asset in assets) if (asset != null) { asset.Disable(); Object.DestroyImmediate(asset); }
      }
    }
  }
}
