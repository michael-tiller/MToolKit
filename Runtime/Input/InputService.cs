using System;
using System.Linq;
using Serilog;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Input.Interfaces;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MToolKit.Runtime.Input
{
    /// <summary>
    /// Concrete implementation of IInputService using Unity's Input System
    /// </summary>
    public class InputService : IInputService, IDisposable
    {
        private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<InputService>().ForFeature("Core.Services.InputService"));
        private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

        public event Action OnPausePressed;
        public event Action OnAnyKeyPressed;

#if ENABLE_INPUT_SYSTEM
        private InputActionAsset inputActionAsset;
        private InputActionMap playerActionMap;
        private InputActionMap uiActionMap;
        private InputAction pauseAction;
        private InputAction anyKeyAction;
#else
        private object inputActionAsset;
        private object playerActionMap;
        private object uiActionMap;
        private object pauseAction;
        private object anyKeyAction;
#endif

        private bool isInitialized = false;
        private bool isEnabled = false;

        private static int instanceCounter = 0;
        private static InputService activeInstance = null; // Track the active instance
        private readonly int instanceId;
        
        public InputService()
        {
            instanceCounter++;
            instanceId = instanceCounter;
            
            if (instanceCounter > 1)
            {
                log.ForMethod(nameof(InputService)).Warning("InputService instance #{0} created, but {1} total instance(s) exist. This may indicate multiple registrations or scope issues.", 
                    instanceId, instanceCounter);
            }
        }
        
        public void Initialize(object inputActionAsset)
        {
            log.ForMethod(nameof(Initialize)).Debug("InputService instance #{0} initializing (total instances created: {1})", instanceId, instanceCounter);
            if (isInitialized)
            {
                log.ForMethod(nameof(Initialize)).Warning("InputService already initialized, ignoring duplicate initialization");
                return;
            }

            if (inputActionAsset == null)
            {
                log.ForMethod(nameof(Initialize)).Error("Cannot initialize InputService with null InputActionAsset");
                throw new ArgumentNullException(nameof(inputActionAsset));
            }

#if ENABLE_INPUT_SYSTEM
            // InputService is the single entry point for all input handling.
            // We create our own clone and manage it directly - PlayerInput should not manage actions.
            var sourceAsset = inputActionAsset as InputActionAsset;
            if (sourceAsset == null)
            {
                log.ForMethod(nameof(Initialize)).Error("InputActionAsset is not of type InputActionAsset");
                throw new ArgumentException("InputActionAsset must be of type InputActionAsset", nameof(inputActionAsset));
            }
            
            // Create our own clone - InputService owns and manages all input actions
            this.inputActionAsset = UnityEngine.Object.Instantiate(sourceAsset);
            log.ForMethod(nameof(Initialize)).Debug("InputService: Created InputActionAsset clone (name: {0}). InputService is the single entry point for input.", this.inputActionAsset.name);
            
            // Set PlayerInput components to use our asset instance instead of cloning
            // This allows PlayerInput to work while InputService manages the asset
            var playerInput = UnityEngine.Object.FindFirstObjectByType<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
            {
                playerInput.actions = this.inputActionAsset;
                log.ForMethod(nameof(Initialize)).Debug("InputService: Set PlayerInput.actions to use InputService's managed asset. Component: {0}", playerInput.name);
            }

            // Get action maps from our managed asset
            playerActionMap = this.inputActionAsset.FindActionMap("Player");
            uiActionMap = this.inputActionAsset.FindActionMap("UI");
            
            log.ForMethod(nameof(Initialize)).Debug("InputService: Found Player action map: {0}", playerActionMap != null);
            log.ForMethod(nameof(Initialize)).Debug("InputService: Found UI action map: {0}", uiActionMap != null);

            // Look for Pause action - try UI action map first (to avoid PlayerInput conflicts), then Player map
            if (uiActionMap != null)
            {
                pauseAction = uiActionMap.FindAction("Pause");
                if (pauseAction != null)
                {
                    pauseAction.performed += OnPauseActionPerformed;
                    log.ForMethod(nameof(Initialize)).Debug("InputService: Pause action found and subscribed in UI action map");
                }
            }
            
            // If not found in UI map, try Player map
            if (pauseAction == null && playerActionMap != null)
            {
                pauseAction = playerActionMap.FindAction("Pause");
                
                if (pauseAction != null)
                {
                    pauseAction.performed += OnPauseActionPerformed;
                    log.ForMethod(nameof(Initialize)).Debug("InputService: Pause action found and subscribed in Player action map");
                }
                else
                {
                    log.ForMethod(nameof(Initialize)).Warning("InputService: Pause action not found in Player action map. Available actions: {0}", 
                        string.Join(", ", playerActionMap.actions.Select(a => a.name)));
                }
            }
            
            if (pauseAction == null)
            {
                log.ForMethod(nameof(Initialize)).Warning("InputService: Pause action not found in any action map");
            }

            // Look for AnyKey action in UI action map
            if (uiActionMap != null)
            {
                anyKeyAction = uiActionMap.FindAction("AnyKey");

                if (anyKeyAction != null)
                {
                    anyKeyAction.performed += OnAnyKeyActionPerformed;
                    log.ForMethod(nameof(Initialize)).Debug("InputService: AnyKey action found and subscribed in UI action map");
                }
                else
                {
                    log.ForMethod(nameof(Initialize)).Warning("InputService: AnyKey action not found in UI action map. Available actions: {0}",
                        string.Join(", ", uiActionMap.actions.Select(a => a.name)));
                }
            }
            else
            {
                log.ForMethod(nameof(Initialize)).Warning("InputService: UI action map not found in InputActionAsset");
            }
#else
            this.inputActionAsset = inputActionAsset;
#endif

            isInitialized = true;
            log.ForMethod(nameof(Initialize)).Debug("InputService initialized successfully");
        }

        public void Enable()
        {
            log.ForMethod(nameof(Enable)).Debug("InputService instance #{0} enabling", instanceId);
            if (!isInitialized)
            {
                log.ForMethod(nameof(Enable)).Error("Cannot enable InputService before initialization");
                throw new InvalidOperationException("InputService must be initialized before enabling");
            }

            if (isEnabled)
            {
                log.ForMethod(nameof(Enable)).Verbose("InputService already enabled");
                return;
            }

#if ENABLE_INPUT_SYSTEM
            // Ensure PlayerInput uses our asset instance
            var playerInput = UnityEngine.Object.FindFirstObjectByType<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null && playerInput.actions != inputActionAsset)
            {
                playerInput.actions = inputActionAsset;
                log.ForMethod(nameof(Enable)).Debug("InputService: Updated PlayerInput.actions to use InputService's managed asset");
            }
            
            if (inputActionAsset != null)
            {
                // InputService enables BOTH Player and UI action maps - it's the single entry point
                if (uiActionMap != null)
                {
                    uiActionMap.Enable();
                    log.ForMethod(nameof(Enable)).Debug("InputService: UI action map enabled");
                }
                else
                {
                    log.ForMethod(nameof(Enable)).Warning("InputService: UI action map is null, cannot enable");
                }
                
                if (playerActionMap != null)
                {
                    playerActionMap.Enable();
                    log.ForMethod(nameof(Enable)).Debug("InputService: Player action map enabled");
                }
                else
                {
                    log.ForMethod(nameof(Enable)).Warning("InputService: Player action map is null, cannot enable");
                }
                
                // Log pause action details for debugging
                if (pauseAction != null)
                {
                    log.ForMethod(nameof(Enable)).Debug("InputService: Pause action enabled: {0}, bindings count: {1}, action map: {2}", 
                        pauseAction.enabled, pauseAction.bindings.Count, pauseAction.actionMap?.name ?? "null");
                    foreach (var binding in pauseAction.bindings)
                    {
                        log.ForMethod(nameof(Enable)).Debug("InputService: Pause binding: {0}", binding.path);
                    }
                }
            }
            else
            {
                log.ForMethod(nameof(Enable)).Warning("InputService: InputActionAsset is null, cannot enable");
            }
#endif

            isEnabled = true;
            
            // Track the active instance - if another instance was previously active, log a warning
            if (activeInstance != null && activeInstance != this)
            {
                log.ForMethod(nameof(Enable)).Warning("InputService instance #{0} enabling, but instance #{1} is already active. This may cause input events to go to the wrong instance.", 
                    instanceId, activeInstance.instanceId);
            }
            
            activeInstance = this;
            
            // Final verification - log pause action status
            if (pauseAction != null)
            {
                var subscriberCount = OnPausePressed != null ? OnPausePressed.GetInvocationList().Length : 0;
                log.ForMethod(nameof(Enable)).Debug("InputService instance #{0}: Final status - Action enabled: {1}, Map enabled: {2}, Event subscribers: {3}", 
                    instanceId,
                    pauseAction.enabled,
                    pauseAction.actionMap?.enabled ?? false,
                    subscriberCount);
            }
            
            log.ForMethod(nameof(Enable)).Debug("InputService enabled successfully");
        }

        public void Disable()
        {
            if (!isEnabled)
            {
                log.ForMethod(nameof(Disable)).Verbose("InputService already disabled");
                return;
            }

#if ENABLE_INPUT_SYSTEM
            // Disable both action maps - InputService manages all input
            if (uiActionMap != null)
            {
                uiActionMap.Disable();
                log.ForMethod(nameof(Disable)).Information("InputService: UI action map disabled");
            }
            
            if (playerActionMap != null)
            {
                playerActionMap.Disable();
                log.ForMethod(nameof(Disable)).Information("InputService: Player action map disabled");
            }
#endif

            isEnabled = false;
            log.ForMethod(nameof(Disable)).Verbose("InputService disabled");
        }

        public bool IsActionPressed(object action)
        {
            if (action == null)
            {
                log.ForMethod(nameof(IsActionPressed)).Warning("IsActionPressed called with null action");
                return false;
            }

#if ENABLE_INPUT_SYSTEM
            if (action is InputAction inputAction)
            {
                return inputAction.IsPressed();
            }
#endif

            return false;
        }

        public bool WasActionPressedThisFrame(object action)
        {
            if (action == null)
            {
                log.ForMethod(nameof(WasActionPressedThisFrame)).Warning("WasActionPressedThisFrame called with null action");
                return false;
            }

#if ENABLE_INPUT_SYSTEM
            if (action is InputAction inputAction)
            {
                return inputAction.WasPressedThisFrame();
            }
#endif

            return false;
        }

        public bool WasActionReleasedThisFrame(object action)
        {
            if (action == null)
            {
                log.ForMethod(nameof(WasActionReleasedThisFrame)).Warning("WasActionReleasedThisFrame called with null action");
                return false;
            }

#if ENABLE_INPUT_SYSTEM
            if (action is InputAction inputAction)
            {
                return inputAction.WasReleasedThisFrame();
            }
#endif

            return false;
        }

#if ENABLE_INPUT_SYSTEM
        private void OnPauseActionPerformed(InputAction.CallbackContext context)
        {
            log.ForMethod(nameof(OnPauseActionPerformed)).Information("InputService instance #{0}: Pause action performed! Context phase: {1}, Event subscribers: {2}, Action enabled: {3}, Action map enabled: {4}", 
                instanceId, context.phase, OnPausePressed?.GetInvocationList().Length ?? 0, 
                pauseAction?.enabled ?? false, pauseAction?.actionMap?.enabled ?? false);
            
            if (OnPausePressed == null)
            {
                log.ForMethod(nameof(OnPauseActionPerformed)).Warning("InputService instance #{0}: OnPausePressed event has no subscribers!", instanceId);
            }
            else
            {
                log.ForMethod(nameof(OnPauseActionPerformed)).Information("InputService instance #{0}: Invoking OnPausePressed event with {1} subscriber(s)", 
                    instanceId, OnPausePressed?.GetInvocationList().Length ?? 0);
                OnPausePressed?.Invoke();
            }
        }

        private void OnAnyKeyActionPerformed(InputAction.CallbackContext context)
        {
            log.ForMethod(nameof(OnAnyKeyActionPerformed)).Debug("AnyKey action performed");
            OnAnyKeyPressed?.Invoke();
        }
#endif

        public void Dispose()
        {
            if (isInitialized)
            {
#if ENABLE_INPUT_SYSTEM
                if (pauseAction != null)
                {
                    pauseAction.performed -= OnPauseActionPerformed;
                }

                if (anyKeyAction != null)
                {
                    anyKeyAction.performed -= OnAnyKeyActionPerformed;
                }

                if (inputActionAsset != null && isEnabled)
                {
                    inputActionAsset.Disable();
                }
#endif

                isInitialized = false;
                isEnabled = false;
                
                // Clear active instance if this was the active one
                if (activeInstance == this)
                {
                    activeInstance = null;
                }
                
                log.ForMethod(nameof(Dispose)).Verbose("InputService disposed");
            }
        }
    }
}
