using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace MToolKit.Runtime.Input.Config
{
  /// <summary>
  ///   ScriptableObject containing gamepad icon sprites for easy configuration and reuse
  /// </summary>
  [CreateAssetMenu(fileName = "GamepadIconsData", menuName = "MToolKit/Input/Gamepad Icons Data")]
  public class GamepadIconsData : ScriptableObject
  {
    [Header("Xbox Controller Icons")]
    [field: FormerlySerializedAs("<xboxIcons>k__BackingField")]
    [field: SerializeField]
    public GamepadIcons XboxIcons { get; private set; }

    [Header("PS4 Controller Icons")]
    [field: FormerlySerializedAs("<ps4Icons>k__BackingField")]
    [field: SerializeField]
    public GamepadIcons Ps4Icons { get; private set; }

    /// <summary>
    ///   Get icon for a given device layout and control path
    /// </summary>
    /// <param name="deviceLayoutName">The device layout name</param>
    /// <param name="controlPath">The control path</param>
    /// <returns>Sprite icon or null if not found</returns>
    public Sprite GetIcon(string deviceLayoutName, string controlPath)
    {
      if (string.IsNullOrEmpty(deviceLayoutName) || string.IsNullOrEmpty(controlPath))
        return null;

      if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "DualShockGamepad"))
        return Ps4Icons.GetSprite(controlPath);
      if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "Gamepad"))
        return XboxIcons.GetSprite(controlPath);

      return null;
    }
  }

  /// <summary>
  ///   Struct containing gamepad icon sprites for efficient runtime usage
  /// </summary>
  [Serializable]
  public struct GamepadIcons
  {
    [SerializeField]
    private Sprite buttonSouth;

    [SerializeField]
    private Sprite buttonNorth;

    [SerializeField]
    private Sprite buttonEast;

    [SerializeField]
    private Sprite buttonWest;

    [SerializeField]
    private Sprite startButton;

    [SerializeField]
    private Sprite selectButton;

    [SerializeField]
    private Sprite leftTrigger;

    [SerializeField]
    private Sprite rightTrigger;

    [SerializeField]
    private Sprite leftShoulder;

    [SerializeField]
    private Sprite rightShoulder;

    [SerializeField]
    private Sprite dpad;

    [SerializeField]
    private Sprite dpadUp;

    [SerializeField]
    private Sprite dpadDown;

    [SerializeField]
    private Sprite dpadLeft;

    [SerializeField]
    private Sprite dpadRight;

    [SerializeField]
    private Sprite leftStick;

    [SerializeField]
    private Sprite rightStick;

    [SerializeField]
    private Sprite leftStickPress;

    [SerializeField]
    private Sprite rightStickPress;

    public Sprite GetSprite(string controlPath)
    {
      // From the input system, we get the path of the control on a device. So we can just
      // map from that to the sprites we have for gamepads.
      switch (controlPath)
      {
        case "buttonSouth":
          return buttonSouth;
        case "buttonNorth":
          return buttonNorth;
        case "buttonEast":
          return buttonEast;
        case "buttonWest":
          return buttonWest;
        case "start":
          return startButton;
        case "select":
          return selectButton;
        case "leftTrigger":
          return leftTrigger;
        case "rightTrigger":
          return rightTrigger;
        case "leftShoulder":
          return leftShoulder;
        case "rightShoulder":
          return rightShoulder;
        case "dpad":
          return dpad;
        case "dpad/up":
          return dpadUp;
        case "dpad/down":
          return dpadDown;
        case "dpad/left":
          return dpadLeft;
        case "dpad/right":
          return dpadRight;
        case "leftStick":
          return leftStick;
        case "rightStick":
          return rightStick;
        case "leftStickPress":
          return leftStickPress;
        case "rightStickPress":
          return rightStickPress;
      }
      return null;
    }
  }
}