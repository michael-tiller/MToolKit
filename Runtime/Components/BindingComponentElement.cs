using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MToolKit.Runtime.Input;
using MToolKit.Runtime.Input.Config;
using MToolKit.Runtime.Localization;
using R3;
using Serilog;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Components
{
  /// <summary>
  ///   UI element for displaying and rebinding a single component of a composite binding
  /// </summary>
  public class BindingComponentElement : MonoBehaviour
  {
    private const string EMPTY_BINDING_DISPLAY = "-";
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<BindingComponentElement>().ForFeature("Components.BindingComponentElement"));
    private static List<BindingComponentElement> bindingComponentElements;
    private static ILogger log => logLazy.Value ?? Logger.None;

    [field: SerializeField]
    public InputBinding.DisplayStringOptions DisplayStringOptions { get; private set; } = InputBinding.DisplayStringOptions.DontUseShortDisplayNames;

    [field: SerializeField]
    [Required]
    public GamepadIconsData GamepadIconsData { get; private set; }

    [field: SerializeField]
    [Required]
    public TextMeshProUGUI TextComponentName { get; private set; }

    [field: SerializeField]
    [Required]
    public Button ButtonPrimary { get; private set; }

    [field: SerializeField]
    [Required]
    public TextMeshProUGUI TextPrimary { get; private set; }

    [field: SerializeField]
    [Required]
    public Image IconPrimary { get; private set; }

    [field: SerializeField]
    [Required]
    public Button ButtonSecondary { get; private set; }

    [field: SerializeField]
    [Required]
    public TextMeshProUGUI TextSecondary { get; private set; }

    [field: SerializeField]
    [Required]
    public Image IconSecondary { get; private set; }

    [field: SerializeField]
    [Required]
    public Button ButtonGamepad { get; private set; }

    [field: SerializeField]
    [Required]
    public TextMeshProUGUI TextGamepad { get; private set; }

    [field: SerializeField]
    [Required]
    public Image IconGamepad { get; private set; }

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

    private CompositeBindingHelper.BindingComponent bindingComponent;

    private InputRebinderService rebinderService;

    // When the action system re-resolves bindings, we want to update our UI in response
    private static void OnActionChange(object obj, InputActionChange change)
    {
      if (change != InputActionChange.BoundControlsChanged)
        return;

      InputAction action = obj as InputAction;
      InputActionMap actionMap = action?.actionMap ?? obj as InputActionMap;
      InputActionAsset actionAsset = actionMap?.asset ?? obj as InputActionAsset;

      foreach (BindingComponentElement component in bindingComponentElements)
      {
        InputAction referencedAction = component.bindingComponent?.Action;
        if (referencedAction == null)
          continue;

        if (referencedAction == action ||
            referencedAction.actionMap == actionMap ||
            referencedAction.actionMap?.asset == actionAsset)
          component.RefreshDisplay();
      }
    }

    public Subject<(BindingComponentElement element, int slotIndex, string newPath)> OnBindingChanged { get; } = new();
    public Subject<(BindingComponentElement element, int slotIndex)> OnRebindingStarted { get; } = new();
    public Subject<(BindingComponentElement element, int slotIndex, bool completed)> OnRebindingCompleted { get; } = new();

    public void Start()
    {
      ButtonPrimary.onClick.AddListener(() => StartRebinding(0));
      ButtonSecondary.onClick.AddListener(() => StartRebinding(1));
      ButtonGamepad.onClick.AddListener(() => StartRebinding(2));
    }

    protected void OnEnable()
    {
      bindingComponentElements ??= new List<BindingComponentElement>();
      bindingComponentElements.Add(this);
      if (bindingComponentElements.Count == 1)
        InputSystem.onActionChange += OnActionChange;
    }

    protected void OnDisable()
    {
      bindingComponentElements.Remove(this);
      if (bindingComponentElements.Count == 0)
      {
        bindingComponentElements = null;
        InputSystem.onActionChange -= OnActionChange;
      }
    }

    private void OnDestroy()
    {
      OnBindingChanged?.Dispose();
      OnRebindingStarted?.Dispose();
      OnRebindingCompleted?.Dispose();
    }

    public void Initialize(CompositeBindingHelper.BindingComponent component, InputRebinderService rebinderService = null)
    {
      bindingComponent = component;
      this.rebinderService = rebinderService;
      RefreshDisplay();
    }

    private void RefreshDisplay()
    {
      if (bindingComponent?.Action == null) return;

      // Update component name display
      TextComponentName.text = LocalizationHelper.GetLocalizedString(bindingComponent.DisplayName);

      // Update individual binding displays based on grouped slots
      UpdateSlotDisplay(0, TextPrimary); // Primary slot
      UpdateSlotDisplay(1, TextSecondary); // Secondary slot  
      UpdateSlotDisplay(2, TextGamepad); // Gamepad slot
    }

    private void UpdateSlotDisplay(int slotIndex, TextMeshProUGUI textComponent)
    {
      if (textComponent == null) return;

      // Find the appropriate slot for this index
      CompositeBindingHelper.BindingSlot slot = null;

      if (slotIndex < bindingComponent.Slots.Count)
      {
        // Try to find the right slot based on device type
        if (slotIndex == 0) // Primary - prefer keyboard/mouse
          slot = bindingComponent.Slots.FirstOrDefault(s => !s.IsGamepad);
        else if (slotIndex == 1) // Secondary - prefer other non-gamepad
          slot = bindingComponent.Slots.Skip(1).FirstOrDefault(s => !s.IsGamepad);
        else if (slotIndex == 2) // Gamepad
          slot = bindingComponent.Slots.FirstOrDefault(s => s.IsGamepad);

        // Fallback to slot by index if not found by type
        if (slot == null && slotIndex < bindingComponent.Slots.Count)
          slot = bindingComponent.Slots[slotIndex];
      }

      if (slot != null)
      {
        string displayString = bindingComponent.Action.GetBindingDisplayString(slot.BindingIndex, DisplayStringOptions);
        textComponent.text = displayString;

        // Try to get device layout and control path for icon display
        string deviceLayoutName = default;
        string controlPath = default;
        bindingComponent.Action.GetBindingDisplayString(slot.BindingIndex, out deviceLayoutName, out controlPath, DisplayStringOptions);

        // Update display with icons if available
        UpdateBindingDisplayWithIcons(slotIndex, textComponent, deviceLayoutName, controlPath);
      }
      else
      {
        textComponent.text = EMPTY_BINDING_DISPLAY;
        // Hide any existing icons
        Transform iconTransform = textComponent.transform.parent.Find("ActionBindingIcon");
        if (iconTransform != null)
          iconTransform.gameObject.SetActive(false);
      }
    }

    private void StartRebinding(int slotIndex)
    {
      if (bindingComponent?.Action == null)
      {
        log.Error("Cannot start rebinding: Binding component is null");
        return;
      }

      if (rebinderService == null)
      {
        log.Error("Cannot start rebinding: InputRebinderService not found");
        return;
      }

      // slotIndex: 0=Primary, 1=Secondary, 2=Gamepad
      if (slotIndex < 0 || slotIndex >= 3)
      {
        log.Error($"Invalid slot index: {slotIndex}. Must be 0 (Primary), 1 (Secondary), or 2 (Gamepad)");
        return;
      }

      InputAction action = bindingComponent.Action;

      // Find the appropriate slot for this index
      CompositeBindingHelper.BindingSlot slot = null;

      if (slotIndex == 0) // Primary - prefer keyboard/mouse
        slot = bindingComponent.Slots.FirstOrDefault(s => !s.IsGamepad);
      else if (slotIndex == 1) // Secondary - prefer other non-gamepad
        slot = bindingComponent.Slots.Skip(1).FirstOrDefault(s => !s.IsGamepad);
      else if (slotIndex == 2) // Gamepad
        slot = bindingComponent.Slots.FirstOrDefault(s => s.IsGamepad);

      // Fallback to slot by index if not found by type
      if (slot == null && slotIndex < bindingComponent.Slots.Count)
        slot = bindingComponent.Slots[slotIndex];

      // If no existing slot, create a new binding
      if (slot == null)
      {
        action.AddBinding();
        slot = new CompositeBindingHelper.BindingSlot
        {
          BindingIndex = action.bindings.Count - 1,
          Binding = action.bindings[action.bindings.Count - 1],
          DeviceType = slotIndex == 2 ? "Gamepad" : "Keyboard",
          IsGamepad = slotIndex == 2
        };
        bindingComponent.Slots.Add(slot);
      }

      // Start interactive rebinding for this specific slot
      PerformInteractiveRebind(action, slot.BindingIndex);
    }

    private void PerformInteractiveRebind(InputAction action, int bindingIndex)
    {
      // Register the action if not already registered
      rebinderService.RegisterAction(action);

      // Find which slot this binding index corresponds to
      int slotIndex = GetSlotIndexForBindingIndex(bindingIndex);

      // Start interactive rebinding
      if (rebinderService.StartInteractiveRebinding(action, bindingIndex, new[] { "Mouse" }))
      {
        OnRebindingStarted.OnNext((this, slotIndex));
        ShowRebindingState(true, slotIndex);

        // Subscribe to rebinding completion
        rebinderService.OnRebindingCompleted
          .Where(x => x.action == action && x.bindingIndex == bindingIndex)
          .Take(1)
          .Subscribe(x =>
          {
            OnRebindingCompleted.OnNext((this, slotIndex, x.completed));
            ShowRebindingState(false, slotIndex, !x.completed);
            RefreshDisplay();
          });
      }
    }

    /// <summary>
    ///   Get the slot index (0=Primary, 1=Secondary, 2=Gamepad) for a given binding index
    /// </summary>
    /// <param name="bindingIndex">The binding index to find</param>
    /// <returns>The slot index, or -1 if not found</returns>
    private int GetSlotIndexForBindingIndex(int bindingIndex)
    {
      for (int i = 0; i < bindingComponent.Slots.Count; i++)
        if (bindingComponent.Slots[i].BindingIndex == bindingIndex)
        {
          // Determine which UI slot this corresponds to
          CompositeBindingHelper.BindingSlot slot = bindingComponent.Slots[i];

          if (slot.IsGamepad)
            return 2; // Gamepad slot

          // For non-gamepad slots, determine if it's primary or secondary
          List<CompositeBindingHelper.BindingSlot> nonGamepadSlots = bindingComponent.Slots.Where(s => !s.IsGamepad).OrderBy(s => s.BindingIndex).ToList();
          int slotPosition = nonGamepadSlots.IndexOf(slot);

          if (slotPosition == 0)
            return 0; // Primary slot
          if (slotPosition == 1)
            return 1; // Secondary slot
          return 0; // Default to primary for additional slots
        }

      return -1; // Not found
    }

    /// <summary>
    ///   Remove currently applied binding overrides for all slots
    /// </summary>
    public void ResetToDefault()
    {
      if (bindingComponent?.Action == null) return;

      InputAction action = bindingComponent.Action;

      // Reset all slots for this component
      foreach (CompositeBindingHelper.BindingSlot slot in bindingComponent.Slots)
        if (slot.BindingIndex < action.bindings.Count)
          action.RemoveBindingOverride(slot.BindingIndex);
      RefreshDisplay();
    }

    /// <summary>
    ///   Remove currently applied binding overrides for a specific slot
    /// </summary>
    /// <param name="slotIndex">0=Primary, 1=Secondary, 2=Gamepad</param>
    public void ResetSlotToDefault(int slotIndex)
    {
      if (bindingComponent?.Action == null) return;

      InputAction action = bindingComponent.Action;

      // Find the appropriate slot for this index
      CompositeBindingHelper.BindingSlot slot = null;

      if (slotIndex == 0) // Primary - prefer keyboard/mouse
        slot = bindingComponent.Slots.FirstOrDefault(s => !s.IsGamepad);
      else if (slotIndex == 1) // Secondary - prefer other non-gamepad
        slot = bindingComponent.Slots.Skip(1).FirstOrDefault(s => !s.IsGamepad);
      else if (slotIndex == 2) // Gamepad
        slot = bindingComponent.Slots.FirstOrDefault(s => s.IsGamepad);

      // Fallback to slot by index if not found by type
      if (slot == null && slotIndex < bindingComponent.Slots.Count)
        slot = bindingComponent.Slots[slotIndex];

      if (slot != null && slot.BindingIndex < action.bindings.Count)
        action.RemoveBindingOverride(slot.BindingIndex);
      RefreshDisplay();
    }

    /// <summary>
    ///   Show or hide rebinding state UI
    /// </summary>
    /// <param name="isRebinding">Whether rebinding is in progress</param>
    /// <param name="slotIndex">The slot index being rebound (0=Primary, 1=Secondary, 2=Gamepad)</param>
    /// <param name="hasConflict">Whether there was a conflict</param>
    private void ShowRebindingState(bool isRebinding, int slotIndex = -1, bool hasConflict = false)
    {
      if (isRebinding)
      {
        // Show rebinding overlay
        if (RebindingOverlay != null)
          RebindingOverlay.SetActive(true);

        // Update rebinding hint text
        if (TextRebindingHint != null)
        {
          TextRebindingHint.gameObject.SetActive(true);

          string slotName = slotIndex switch
          {
            0 => "Primary",
            1 => "Secondary",
            2 => "Gamepad",
            _ => "Unknown"
            };

          TextRebindingHint.text =
            $"{LocalizationHelper.GetLocalizedString("Press any key to rebind")} {bindingComponent.DisplayName} {LocalizationHelper.GetLocalizedString(slotName)} {LocalizationHelper.GetLocalizedString("slot")}...";
        }

        // Highlight the button being rebound
        SetButtonHighlight(slotIndex, true);

        // Disable all buttons during rebinding
        ButtonPrimary.interactable = false;
        ButtonSecondary.interactable = false;
        ButtonGamepad.interactable = false;
      }
      else
      {
        // Hide rebinding overlay
        if (RebindingOverlay != null)
          RebindingOverlay.SetActive(false);

        // Hide rebinding hint
        if (TextRebindingHint != null)
          TextRebindingHint.gameObject.SetActive(false);

        // Reset button highlights
        SetButtonHighlight(-1, false);

        // Re-enable all buttons
        ButtonPrimary.interactable = true;
        ButtonSecondary.interactable = true;
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
    ///   Set visual highlight on a specific button
    /// </summary>
    /// <param name="slotIndex">The slot index to highlight (-1 for none): 0=Primary, 1=Secondary, 2=Gamepad</param>
    /// <param name="highlight">Whether to highlight or reset</param>
    private void SetButtonHighlight(int slotIndex, bool highlight)
    {
      Color targetColor = highlight ? RebindingButtonColor : NormalButtonColor;

      switch (slotIndex)
      {
        case 0: // Primary
          ButtonPrimary.targetGraphic.color = targetColor;
          break;
        case 1: // Secondary
          ButtonSecondary.targetGraphic.color = targetColor;
          break;
        case 2: // Gamepad
          ButtonGamepad.targetGraphic.color = targetColor;
          break;
        default:
          // Reset all buttons
          ButtonPrimary.targetGraphic.color = NormalButtonColor;
          ButtonSecondary.targetGraphic.color = NormalButtonColor;
          ButtonGamepad.targetGraphic.color = NormalButtonColor;
          break;
      }
    }

    /// <summary>
    ///   Hide conflict message after a delay
    /// </summary>
    /// <param name="delay">Delay in seconds</param>
    private IEnumerator HideConflictMessageAfterDelay(float delay)
    {
      yield return new WaitForSeconds(delay);
      if (TextRebindingHint != null)
      {
        TextRebindingHint.gameObject.SetActive(false);
        TextRebindingHint.color = NormalButtonColor;
      }
    }

    /// <summary>
    ///   Update binding display with gamepad icons when available
    /// </summary>
    /// <param name="slotIndex">The slot index (0=Primary, 1=Secondary, 2=Gamepad)</param>
    /// <param name="textComponent">The text component to update</param>
    /// <param name="deviceLayoutName">The device layout name</param>
    /// <param name="controlPath">The control path</param>
    private void UpdateBindingDisplayWithIcons(int slotIndex, TextMeshProUGUI textComponent, string deviceLayoutName, string controlPath)
    {
      if (textComponent == null) return;

      // Get the appropriate icon component for this slot
      Image iconComponent = slotIndex switch
      {
        0 => IconPrimary,
        1 => IconSecondary,
        2 => IconGamepad,
        _ => null
        };

      if (iconComponent == null) return;

      // Try to get gamepad icon
      Sprite icon = GetGamepadIcon(deviceLayoutName, controlPath);

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
        textComponent.gameObject.SetActive(true);
        iconComponent.gameObject.SetActive(false);
      }
    }

    /// <summary>
    ///   Get gamepad icon for a given device layout and control path
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
        return GamepadIconsData.Ps4Icons.GetSprite(controlPath);
      if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "Gamepad"))
        return GamepadIconsData.XboxIcons.GetSprite(controlPath);

      return null;
    }
  }
}