using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Components;
using MToolKit.Runtime.Input;
using MToolKit.Runtime.Settings.Interfaces;
using MToolKit.Runtime.Settings.UI.Abstract;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Settings.Input
{
  public class InputSettingsInitializer : AbstractSettingsInitializer
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<InputSettingsInitializer>().ForFeature("Settings.Input"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    [SerializeField]
    [Required]
    private KeybindListElement keybindListElementPrefab;

    [SerializeField]
    [Required]
    private BindingComponentElement bindingComponentElementPrefab;

    [SerializeField]
    [Required]
    private Transform keybindContainer;

    [SerializeField]
    [Required]
    private InputActionAsset inputActionAsset;

    private readonly List<BindingComponentElement> bindingComponentElements = new();

    private readonly List<KeybindListElement> keybindListElements = new();
    private InputRebinderService inputRebinderService;

    [Inject]
    private ISettingsSystem settingsController;

    public override UniTask ConfigureAsync()
    {
      if (inputActionAsset == null)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("InputActionAsset is not assigned");
        return UniTask.CompletedTask;
      }

      // Create the InputRebinderService instance
      inputRebinderService = new InputRebinderService();

      // Log control schemes for debugging
      List<string> controlSchemes = CompositeBindingHelper.GetControlSchemeNames(inputActionAsset);
      log.ForGameObject(gameObject).ForMethod().Verbose("Available control schemes: {ControlSchemes}", string.Join(", ", controlSchemes));

      // Register all actions from the input action asset
      foreach (InputActionMap actionMap in inputActionAsset.actionMaps)
        foreach (InputAction action in actionMap.actions)
        {
          if (action.name == "Look") continue;
          inputRebinderService.RegisterAction(action);
          log.ForGameObject(gameObject).ForMethod().Verbose("Registered action {ActionName} from {ActionMap}", action.name, actionMap.name);
        }

      // Clear existing elements
      foreach (KeybindListElement element in keybindListElements)
        if (element != null)
          DestroyImmediate(element.gameObject);
      keybindListElements.Clear();

      foreach (BindingComponentElement element in bindingComponentElements)
        if (element != null)
          DestroyImmediate(element.gameObject);
      bindingComponentElements.Clear();

      // Create keybind elements for each registered action
      foreach (InputAction action in inputRebinderService.RegisteredActions)
        // Check if this action has bindings that should be grouped by logical name
        if (CompositeBindingHelper.HasGroupableBindings(action))
        {
          // Create individual components for grouped bindings
          List<CompositeBindingHelper.BindingComponent> components = CompositeBindingHelper.GetGroupedBindingComponents(action);

          foreach (CompositeBindingHelper.BindingComponent component in components)
          {
            BindingComponentElement element = Instantiate(bindingComponentElementPrefab, keybindContainer);
            element.gameObject.SetActive(true);
            bindingComponentElements.Add(element);

            element.Initialize(component, inputRebinderService);
            log.ForGameObject(gameObject).ForMethod().Verbose("Created binding component element for {ActionName}.{ComponentName}",
              action.name, component.DisplayName);
          }
        }
        else
        {
          // Create regular keybind element for actions without groupable bindings
          KeybindListElement element = Instantiate(keybindListElementPrefab, keybindContainer);
          element.gameObject.SetActive(true);
          keybindListElements.Add(element);

          // Find the existing InputActionReference from the asset
          InputAction actionReference = inputActionAsset.FindAction(action.name);
          if (actionReference != null)
          {
            element.Initialize(InputActionReference.Create(action), inputRebinderService);
            log.ForGameObject(gameObject).ForMethod().Debug("Created keybind element for action {ActionName}", action.name);
          }
          else
          {
            log.ForGameObject(gameObject).ForMethod().Warning("Could not find InputActionReference for action {ActionName}", action.name);
          }
        }

      log.ForGameObject(gameObject).ForMethod().Information(
        "Configured {KeybindCount} keybind elements and {ComponentCount} binding component elements from {ActionMaps} action maps",
        keybindListElements.Count, bindingComponentElements.Count, inputActionAsset.actionMaps.Count);
      return UniTask.CompletedTask;
    }
  }
}