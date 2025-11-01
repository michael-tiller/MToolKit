using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Input;
using R3;
using MToolKit.Runtime.Localization;
using MToolKit.Runtime.Input.Config;

namespace MToolKit.Runtime.Components
{
  public class KeybindListElement : MonoBehaviour
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<KeybindListElement>().ForFeature("Components.KeybindListElement"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

    [field: SerializeField]
    [Required]
    public InputActionReference InputActionReference { get; private set; }

    [field: SerializeField]
    public InputBinding.DisplayStringOptions DisplayStringOptions { get; private set; } = InputBinding.DisplayStringOptions.DontUseShortDisplayNames;

    [field: SerializeField]
    [Required]
    public Button Button { get; private set; }

    [field: SerializeField]
    [Required]
    public TextMeshProUGUI TextKeybind { get; private set; }
    [field: SerializeField]
    [Required]
    public TextMeshProUGUI TextButton { get; private set; }
    [field: SerializeField]
    [Required]
    public Image IconButton { get; private set; }

    [field: SerializeField]
    [Required]
    public Button ButtonAlt { get; private set; }
    [field: SerializeField]
    [Required]
    public TextMeshProUGUI TextButtonAlt { get; private set; }
    [field: SerializeField]
    [Required]
    public Image IconButtonAlt { get; private set; }

    [field: SerializeField]
    [Required]
    public Button ButtonGamepad { get; private set; }
    [field: SerializeField]
    [Required]
    public TextMeshProUGUI TextButtonGamepad { get; private set; }
    [field: SerializeField]
    [Required]
    public Image IconButtonGamepad { get; private set; }

    [field: SerializeField]
    public TextMeshProUGUI TextRebindingHint { get; private set; }

    [field: SerializeField]
    public GameObject RebindingOverlay { get; private set; }

    [field: SerializeField]
    public Color NormalButtonColor { get; private set; } = Color.white;

    [field: SerializeField]
    public Color RebindingButtonColor { get; private set; } = Color.yellow;

    [field: SerializeField]
    public Color ConflictButtonColor { get; private set; } = Color.red;

    [field: SerializeField]
    [Required]
    public GamepadIconsData GamepadIconsData { get; private set; }

    public Subject<(KeybindListElement element, int bindingIndex, string newPath)> OnBindingChanged { get; } = new();
    public Subject<(KeybindListElement element, int bindingIndex)> OnRebindingStarted { get; } = new();
    public Subject<(KeybindListElement element, int bindingIndex, bool completed)> OnRebindingCompleted { get; } = new();

    private InputRebinderService rebinderService;
    private static List<KeybindListElement> s_KeybindListElements;

    public void Initialize(InputActionReference inputActionReference, InputRebinderService rebinderService = null)
    {
      InputActionReference = inputActionReference;
      this.rebinderService = rebinderService;
      RefreshDisplay();
    }

    /// <summary>
    /// Get the action and ensure it has at least 3 bindings for Primary/Secondary/Gamepad slots
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ResolveAction(out InputAction action)
    {
      action = InputActionReference?.action;
      if (action == null)
        return false;

      // Ensure we have at least 3 bindings for Primary/Secondary/Gamepad
      while (action.bindings.Count < 3)
      {
        action.AddBinding();
      }

      return true;
    }

    public void Start()
    {
      Button.onClick.AddListener(() => StartRebinding(0));
      ButtonAlt.onClick.AddListener(() => StartRebinding(1));
      ButtonGamepad.onClick.AddListener(() => StartRebinding(2));
      
      if (InputActionReference != null) Initialize(InputActionReference, rebinderService);
    }

    protected void OnEnable()
    {
      if (s_KeybindListElements == null)
        s_KeybindListElements = new List<KeybindListElement>();
      s_KeybindListElements.Add(this);
      if (s_KeybindListElements.Count == 1)
        InputSystem.onActionChange += OnActionChange;
    }

    protected void OnDisable()
    {
      s_KeybindListElements.Remove(this);
      if (s_KeybindListElements.Count == 0)
      {
        s_KeybindListElements = null;
        InputSystem.onActionChange -= OnActionChange;
      }
    }

    public const string EMPTY_BINDING_DISPLAY = "-";

    // When the action system re-resolves bindings, we want to update our UI in response
    private static void OnActionChange(object obj, InputActionChange change)
    {
      if (change != InputActionChange.BoundControlsChanged)
        return;

      var action = obj as InputAction;
      var actionMap = action?.actionMap ?? obj as InputActionMap;
      var actionAsset = actionMap?.asset ?? obj as InputActionAsset;

      for (var i = 0; i < s_KeybindListElements.Count; ++i)
      {
        var component = s_KeybindListElements[i];
        var referencedAction = component.InputActionReference?.action;
        if (referencedAction == null)
          continue;

        if (referencedAction == action ||
            referencedAction.actionMap == actionMap ||
            referencedAction.actionMap?.asset == actionAsset)
          component.RefreshDisplay();
      }
    }

    public void RefreshDisplay()
    {
      if (InputActionReference?.action == null) return;

      var action = InputActionReference.action;
      
      // Update main display
      TextKeybind.text = LocalizationHelper.GetLocalizedString(action.name);
      
      // Update individual binding displays with icons when available
      UpdateBindingDisplayWithIcons(0, TextButton, IconButton);
      UpdateBindingDisplayWithIcons(1, TextButtonAlt, IconButtonAlt);
      UpdateBindingDisplayWithIcons(2, TextButtonGamepad, IconButtonGamepad);
    }

    private void UpdateBindingDisplay(int bindingIndex, TextMeshProUGUI textComponent)
    {
      if (textComponent == null) return;

      var action = InputActionReference?.action;
      if (action == null || bindingIndex >= action.bindings.Count)
      {
        textComponent.text = EMPTY_BINDING_DISPLAY;
        return;
      }

      var displayString = action.GetBindingDisplayString(bindingIndex, DisplayStringOptions);
      textComponent.text = displayString;
    }

    /// <summary>
    /// Update binding display with gamepad icons when available
    /// </summary>
    /// <param name="bindingIndex">The binding index</param>
    /// <param name="textComponent">The text component to update</param>
    /// <param name="iconComponent">The icon component to update</param>
    private void UpdateBindingDisplayWithIcons(int bindingIndex, TextMeshProUGUI textComponent, Image iconComponent)
    {
      if (textComponent == null || iconComponent == null) return;

      var action = InputActionReference?.action;
      if (action == null || bindingIndex >= action.bindings.Count)
      {
        textComponent.text = EMPTY_BINDING_DISPLAY;
        textComponent.gameObject.SetActive(true);
        iconComponent.gameObject.SetActive(false);
        return;
      }

      var binding = action.bindings[bindingIndex];
      var displayString = action.GetBindingDisplayString(bindingIndex, DisplayStringOptions);
      
      // Extract device layout and control path for icon lookup
      var deviceLayoutName = binding.effectivePath?.Split('/')[0];
      var controlPath = binding.effectivePath?.Split('/').LastOrDefault();
      
      // Try to get gamepad icon
      var icon = GetGamepadIcon(deviceLayoutName, controlPath);
      
      if (icon != null)
      {
        // Hide text and show icon
        textComponent.gameObject.SetActive(false);
        iconComponent.sprite = icon;
        iconComponent.gameObject.SetActive(true);
      }
      else
      {
        // Show text and hide icon
        textComponent.text = displayString;
        textComponent.gameObject.SetActive(true);
        iconComponent.gameObject.SetActive(false);
      }
    }

    private void StartRebinding(int bindingIndex)
    {
      if (!ResolveAction(out var action))
      {
        log.Error("Cannot start rebinding: Could not resolve action");
        return;
      }

      if (rebinderService == null)
      {
        log.Error("Cannot start rebinding: InputRebinderService not found");
        return;
      }

      // bindingIndex: 0=Primary, 1=Secondary, 2=Gamepad
      if (bindingIndex < 0 || bindingIndex >= 3)
      {
        log.Error($"Invalid binding index: {bindingIndex}. Must be 0 (Primary), 1 (Secondary), or 2 (Gamepad)");
        return;
      }

      // If the binding is a composite, we need to rebind each part in turn
      if (action.bindings[bindingIndex].isComposite)
      {
        var firstPartIndex = bindingIndex + 1;
        if (firstPartIndex < action.bindings.Count && action.bindings[firstPartIndex].isPartOfComposite)
          PerformInteractiveRebind(action, firstPartIndex, true);
      }
      else
      {
        PerformInteractiveRebind(action, bindingIndex);
      }
    }

    private void PerformInteractiveRebind(InputAction action, int bindingIndex, bool allCompositeParts = false)
    {
      // Register the action if not already registered
      rebinderService.RegisterAction(action);
      
      // Start interactive rebinding
      if (rebinderService.StartInteractiveRebinding(action, bindingIndex, new[] { "Mouse" }))
      {
        OnRebindingStarted.OnNext((this, bindingIndex));
        ShowRebindingState(true, bindingIndex);
        
        // Subscribe to rebinding completion
        rebinderService.OnRebindingCompleted
          .Where(x => x.action == action && x.bindingIndex == bindingIndex)
          .Take(1)
          .Subscribe(x =>
          {
            OnRebindingCompleted.OnNext((this, bindingIndex, x.completed));
            ShowRebindingState(false, bindingIndex, !x.completed);
            RefreshDisplay();

            // If there's more composite parts we should bind, initiate a rebind for the next part
            if (allCompositeParts && x.completed)
            {
              var nextBindingIndex = bindingIndex + 1;
              if (nextBindingIndex < action.bindings.Count && action.bindings[nextBindingIndex].isPartOfComposite)
                PerformInteractiveRebind(action, nextBindingIndex, true);
            }
          });
      }
    }

    /// <summary>
    /// Remove currently applied binding overrides for all three slots (Primary/Secondary/Gamepad).
    /// </summary>
    public void ResetToDefault()
    {
      if (!ResolveAction(out var action))
        return;

      // Reset all three binding slots
      for (int i = 0; i < 3 && i < action.bindings.Count; i++)
      {
        if (action.bindings[i].isComposite)
        {
          // It's a composite. Remove overrides from part bindings.
          for (var j = i + 1; j < action.bindings.Count && action.bindings[j].isPartOfComposite; ++j)
            action.RemoveBindingOverride(j);
        }
        else
        {
          action.RemoveBindingOverride(i);
        }
      }
      RefreshDisplay();
    }

    /// <summary>
    /// Remove currently applied binding overrides for a specific slot.
    /// </summary>
    /// <param name="slotIndex">0=Primary, 1=Secondary, 2=Gamepad</param>
    public void ResetSlotToDefault(int slotIndex)
    {
      if (!ResolveAction(out var action))
        return;

      if (slotIndex < 0 || slotIndex >= 3 || slotIndex >= action.bindings.Count)
        return;

      if (action.bindings[slotIndex].isComposite)
      {
        // It's a composite. Remove overrides from part bindings.
        for (var i = slotIndex + 1; i < action.bindings.Count && action.bindings[i].isPartOfComposite; ++i)
          action.RemoveBindingOverride(i);
      }
      else
      {
        action.RemoveBindingOverride(slotIndex);
      }
      RefreshDisplay();
    }

    /// <summary>
    /// Show or hide rebinding state UI
    /// </summary>
    /// <param name="isRebinding">Whether rebinding is in progress</param>
    /// <param name="bindingIndex">The binding index being rebound</param>
    /// <param name="hasConflict">Whether there was a conflict</param>
    private void ShowRebindingState(bool isRebinding, int bindingIndex = -1, bool hasConflict = false)
    {
      if (isRebinding)
      {
        // Show rebinding overlay
        if (RebindingOverlay != null)
        {
          RebindingOverlay.SetActive(true);
        }
        
        // Update rebinding hint text
        if (TextRebindingHint != null)
        {
          TextRebindingHint.gameObject.SetActive(true);
          
          var action = InputActionReference?.action;
          var slotName = bindingIndex switch
          {
            0 => "Primary",
            1 => "Secondary", 
            2 => "Gamepad",
            _ => "Unknown"
          };
          
          // If it's a part binding, show the name of the part in the UI like RebindActionUI does
          var partName = "";
          if (action != null && bindingIndex < action.bindings.Count && action.bindings[bindingIndex].isPartOfComposite)
            partName = $"Binding '{action.bindings[bindingIndex].name}'. ";
          
          TextRebindingHint.text = $"{partName}{LocalizationHelper.GetLocalizedString("Press any key to rebind")} {LocalizationHelper.GetLocalizedString(slotName)} {LocalizationHelper.GetLocalizedString("slot")}...";
        }
        
        // Highlight the button being rebound
        SetButtonHighlight(bindingIndex, true);
        
        // Disable all buttons during rebinding
        Button.interactable = false;
        ButtonAlt.interactable = false;
        ButtonGamepad.interactable = false;
      }
      else
      {
        // Hide rebinding overlay
        if (RebindingOverlay != null)
        {
          RebindingOverlay.SetActive(false);
        }
        
        // Hide rebinding hint
        if (TextRebindingHint != null)
        {
          TextRebindingHint.gameObject.SetActive(false);
        }
        
        // Reset button highlights
        SetButtonHighlight(-1, false);
        
        // Re-enable all buttons
        Button.interactable = true;
        ButtonAlt.interactable = true;
        ButtonGamepad.interactable = true;
        
        // Show conflict message if there was a conflict
        if (hasConflict && TextRebindingHint != null)
        {
          TextRebindingHint.gameObject.SetActive(true);
          TextRebindingHint.text = $"{LocalizationHelper.GetLocalizedString("Binding conflict!")} {LocalizationHelper.GetLocalizedString("Key already in use.")}";
          TextRebindingHint.color = ConflictButtonColor;
          
          // Hide the conflict message after 2 seconds
          StartCoroutine(HideConflictMessageAfterDelay(2f));
        }
      }
    }

    /// <summary>
    /// Set visual highlight on a specific button
    /// </summary>
    /// <param name="bindingIndex">The binding index to highlight (-1 for none): 0=Primary, 1=Secondary, 2=Gamepad</param>
    /// <param name="highlight">Whether to highlight or reset</param>
    private void SetButtonHighlight(int bindingIndex, bool highlight)
    {
      var targetColor = highlight ? RebindingButtonColor : NormalButtonColor;
      
      switch (bindingIndex)
      {
        case 0: // Primary
          Button.targetGraphic.color = targetColor;
          break;
        case 1: // Secondary
          ButtonAlt.targetGraphic.color = targetColor;
          break;
        case 2: // Gamepad
          ButtonGamepad.targetGraphic.color = targetColor;
          break;
        default:
          // Reset all buttons
          Button.targetGraphic.color = NormalButtonColor;
          ButtonAlt.targetGraphic.color = NormalButtonColor;
          ButtonGamepad.targetGraphic.color = NormalButtonColor;
          break;
      }
    }

    /// <summary>
    /// Hide conflict message after a delay
    /// </summary>
    /// <param name="delay">Delay in seconds</param>
    private System.Collections.IEnumerator HideConflictMessageAfterDelay(float delay)
    {
      yield return new WaitForSeconds(delay);
      if (TextRebindingHint != null)
      {
        TextRebindingHint.gameObject.SetActive(false);
        TextRebindingHint.color = NormalButtonColor;
      }
    }

    /// <summary>
    /// Get gamepad icon for a given device layout and control path
    /// </summary>
    /// <param name="deviceLayoutName">The device layout name</param>
    /// <param name="controlPath">The control path</param>
    /// <returns>Sprite icon or null if not found</returns>
    private Sprite GetGamepadIcon(string deviceLayoutName, string controlPath)
    {
      if (GamepadIconsData == null || string.IsNullOrEmpty(deviceLayoutName) || string.IsNullOrEmpty(controlPath))
        return null;

      // Use the appropriate icon set based on device layout
      if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "DualShockGamepad"))
        return GamepadIconsData.ps4Icons.GetSprite(controlPath);
      else if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "Gamepad"))
        return GamepadIconsData.xboxIcons.GetSprite(controlPath);

      return null;
    }

    private void OnDestroy()
    {
      OnBindingChanged?.Dispose();
      OnRebindingStarted?.Dispose();
      OnRebindingCompleted?.Dispose();
    }
  }
}