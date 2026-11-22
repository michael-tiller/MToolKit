using System;
using System.Collections.Generic;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Hosting;
using MToolKit.ContextMenus.Presentation;
using MToolKit.ContextMenus.Widgets;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuPromptObservationTests
  {
    [TestCase(false)]
    [TestCase(true)]
    public void ForeignMenuBindingRefreshesHintAndReleasesObservation(bool sourceOwned)
    {
      using var fixture = new Fixture(sourceOwned);
      var hint = fixture.Presenter.GetComponentInChildren<MenuCenterWidget>().BindingHint;
      Assert.That(hint, Is.Not.Null, "The supplied default center has a real optional hint label.");
      string before = fixture.Prompts.Resolve(fixture.Runtime);
      Assert.That(hint.text, Is.EqualTo(before));
      fixture.Runtime.ApplyBindingOverride(0, "<Keyboard>/q");
      string rebound = fixture.Prompts.Resolve(fixture.Runtime);
      Assert.That(rebound, Is.Not.EqualTo(before), "The actual owned runtime action changed its displayed binding.");
      Assert.That(hint.text, Is.EqualTo(rebound),
        "A foreign profile clone must participate in the prompt service's existing binding observation lifetime.");
      Assert.That(fixture.Authored.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/e"),
        "Displaying a rebound menu must never mutate the authored asset.");
      fixture.Presenter.Unbind();
      int changes = 0;
      using var observer = fixture.Prompts.Changed.Subscribe(_ => changes++);
      fixture.Runtime.ApplyBindingOverride(0, "<Keyboard>/w");
      Assert.That(changes, Is.Zero, "Retiring the menu releases its foreign action observation.");
    }

    private sealed class Source : IInputActionSource
    {
      public Source(InputActionAsset asset) => RuntimeActions = asset;
      public InputActionAsset RuntimeActions { get; }
      public InputAction ResolveRuntimeAction(InputAction action) => action;
    }

    private sealed class Fixture : IDisposable
    {
      private readonly List<Object> objects = new();
      private readonly Keyboard previousKeyboard = Keyboard.current;
      private readonly Keyboard keyboard;
      private readonly MenuProfileActionSource actions;
      private readonly MenuSession session;
      private readonly MenuPresentationContext context;
      public InputAction Authored { get; }
      public InputAction Runtime { get; }
      public InputPromptService Prompts { get; }
      public RadialMenuPresenter Presenter { get; }

      public Fixture(bool sourceOwned)
      {
        try
        {
          keyboard = InputSystem.AddDevice<Keyboard>();
          var owner = Own(ScriptableObject.CreateInstance<InputActionAsset>());
          owner.AddActionMap("Owner").AddAction("Unrelated", InputActionType.Button, "<Keyboard>/b");
          owner.devices = new InputDevice[] { keyboard };
          var foreign = Own(ScriptableObject.CreateInstance<InputActionAsset>());
          Authored = foreign.AddActionMap("Menu").AddAction("Confirm", InputActionType.Button);
          Authored.AddBinding("<Keyboard>/e", groups: "Keyboard&Mouse");
          var reference = Own(InputActionReference.Create(Authored));
          var profile = Own(ScriptableObject.CreateInstance<MenuProfile>());
          profile.InputBindings.Configure(submit: reference);
          actions = new MenuProfileActionSource(); actions.Prepare(profile.InputBindings);
          Runtime = actions.ActiveSource.ResolveRuntimeAction(Authored);
          Assert.That(Runtime, Is.Not.SameAs(Authored));
          Runtime.actionMap.asset.devices = new InputDevice[] { keyboard };
          Runtime.Enable();
          _ = Runtime.controls.Count; // Resolve the real Input System state before observing a rebind.
          Prompts = sourceOwned ? new InputPromptService(actionSource: new Source(owner)) : new InputPromptService(actions: owner);
          Prompts.SetContext(new InputPromptContext(device: keyboard, bindingGroup: "Keyboard&Mouse",
            familyOverride: InputGlyphFamily.KeyboardAndMouse));
          var canvasObject = Own(new GameObject("Foreign prompt canvas", typeof(RectTransform), typeof(Canvas)));
          var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
          ((RectTransform)canvasObject.transform).sizeDelta = new Vector2(1200, 900);
          var root = new GameObject("Prompt presenter", typeof(RectTransform)); root.transform.SetParent(canvas.transform, false);
          Presenter = root.AddComponent<RadialMenuPresenter>();
          session = new MenuSession();
          session.Open(new[] { new MenuAction("command", new MenuLabel("Command"), new MenuCommandBehavior(_ => { })) }, new MenuContext());
          context = new MenuPresentationContext(canvas, Vector2.zero, prompts: Prompts,
            labelOverride: label => label.Fallback, resolveInput: action => actions.ActiveSource.ResolveRuntimeAction(action));
          Presenter.Bind(session, profile, context);
        }
        catch { Dispose(); throw; }
      }

      private T Own<T>(T value) where T : Object { objects.Add(value); return value; }
      public void Dispose()
      {
        if (Presenter != null) Presenter.Unbind();
        context?.Dispose(); session?.Dispose(); actions?.Dispose(); Prompts?.Dispose();
        for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
        objects.Clear();
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
      }
    }
  }
}
