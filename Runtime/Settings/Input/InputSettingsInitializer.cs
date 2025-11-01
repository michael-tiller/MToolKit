using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Serilog;
using ILogger = Serilog.ILogger;
using Sirenix.OdinInspector;
using MToolKit.Runtime.Settings.UI.Abstract;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Settings.Interfaces;
using VContainer;
using MToolKit.Runtime.Components;
using MToolKit.Runtime.Input;

namespace MToolKit.Runtime.Settings.Input
{
  public class InputSettingsInitializer : AbstractSettingsInitializer 
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<InputSettingsInitializer>().ForFeature("Settings.Input"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

        [Inject] private ISettingsSystem settingsController;

        [SerializeField, Required] private KeybindListElement keybindListElementPrefab;
        [SerializeField, Required] private BindingComponentElement bindingComponentElementPrefab;
        [SerializeField, Required] private Transform keybindContainer;
        [SerializeField, Required] private InputActionAsset inputActionAsset;

        private List<KeybindListElement> keybindListElements = new List<KeybindListElement>();
        private List<BindingComponentElement> bindingComponentElements = new List<BindingComponentElement>();
        private InputRebinderService inputRebinderService;

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
            var controlSchemes = CompositeBindingHelper.GetControlSchemeNames(inputActionAsset);
            log.ForGameObject(gameObject).ForMethod().Information("Available control schemes: {ControlSchemes}", string.Join(", ", controlSchemes));

            // Register all actions from the input action asset
            foreach (var actionMap in inputActionAsset.actionMaps)
            {
                foreach (var action in actionMap.actions)
                {
                    if (action.name == "Look") continue;
                    inputRebinderService.RegisterAction(action);
                    log.ForGameObject(gameObject).ForMethod().Debug("Registered action {ActionName} from {ActionMap}", action.name, actionMap.name);
                }
            }

            // Clear existing elements
            foreach (var element in keybindListElements)
            {
                if (element != null)
                    DestroyImmediate(element.gameObject);
            }
            keybindListElements.Clear();

            foreach (var element in bindingComponentElements)
            {
                if (element != null)
                    DestroyImmediate(element.gameObject);
            }
            bindingComponentElements.Clear();

            // Create keybind elements for each registered action
            foreach (var action in inputRebinderService.RegisteredActions)
            {
                // Check if this action has bindings that should be grouped by logical name
                if (CompositeBindingHelper.HasGroupableBindings(action))
                {
                    // Create individual components for grouped bindings
                    var components = CompositeBindingHelper.GetGroupedBindingComponents(action);
                    
                    foreach (var component in components)
                    {
                        var element = Instantiate(bindingComponentElementPrefab, keybindContainer);
                        element.gameObject.SetActive(true);
                        bindingComponentElements.Add(element);
                        
                        element.Initialize(component, inputRebinderService);
                        log.ForGameObject(gameObject).ForMethod().Debug("Created binding component element for {ActionName}.{ComponentName}", 
                            action.name, component.DisplayName);
                    }
                }
                else
                {
                    // Create regular keybind element for actions without groupable bindings
                    var element = Instantiate(keybindListElementPrefab, keybindContainer);
                    element.gameObject.SetActive(true);
                    keybindListElements.Add(element);
                    
                    // Find the existing InputActionReference from the asset
                    var actionReference = inputActionAsset.FindAction(action.name);
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
            }

            log.ForGameObject(gameObject).ForMethod().Information("Configured {KeybindCount} keybind elements and {ComponentCount} binding component elements from {ActionMaps} action maps", 
                keybindListElements.Count, bindingComponentElements.Count, inputActionAsset.actionMaps.Count);
            return UniTask.CompletedTask;
        }
    }
}