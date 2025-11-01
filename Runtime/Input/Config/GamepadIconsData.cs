using UnityEngine;

namespace MToolKit.Runtime.Input.Config
{
  /// <summary>
  /// ScriptableObject containing gamepad icon sprites for easy configuration and reuse
  /// </summary>
  [CreateAssetMenu(fileName = "GamepadIconsData", menuName = "MToolKit/Input/Gamepad Icons Data")]
  public class GamepadIconsData : ScriptableObject
  {
    [Header("Xbox Controller Icons")]
    [field: SerializeField] 
    public GamepadIcons xboxIcons { get; private set; }

    [Header("PS4 Controller Icons")]
    [field: SerializeField]
    public GamepadIcons ps4Icons { get; private set; }

    /// <summary>
    /// Get icon for a given device layout and control path
    /// </summary>
    /// <param name="deviceLayoutName">The device layout name</param>
    /// <param name="controlPath">The control path</param>
    /// <returns>Sprite icon or null if not found</returns>
    public Sprite GetIcon(string deviceLayoutName, string controlPath)
    {
      if (string.IsNullOrEmpty(deviceLayoutName) || string.IsNullOrEmpty(controlPath))
        return null;

      if (UnityEngine.InputSystem.InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "DualShockGamepad"))
        return ps4Icons.GetSprite(controlPath);
      else if (UnityEngine.InputSystem.InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "Gamepad"))
        return xboxIcons.GetSprite(controlPath);

      return null;
    }
  }

  /// <summary>
  /// Struct containing gamepad icon sprites for efficient runtime usage
  /// </summary>
  [System.Serializable]
  public struct GamepadIcons
  {
    public Sprite buttonSouth;
    public Sprite buttonNorth;
    public Sprite buttonEast;
    public Sprite buttonWest;
    public Sprite startButton;
    public Sprite selectButton;
    public Sprite leftTrigger;
    public Sprite rightTrigger;
    public Sprite leftShoulder;
    public Sprite rightShoulder;
    public Sprite dpad;
    public Sprite dpadUp;
    public Sprite dpadDown;
    public Sprite dpadLeft;
    public Sprite dpadRight;
    public Sprite leftStick;
    public Sprite rightStick;
    public Sprite leftStickPress;
    public Sprite rightStickPress;

    public Sprite GetSprite(string controlPath)
    {
      // From the input system, we get the path of the control on device. So we can just
      // map from that to the sprites we have for gamepads.
      switch (controlPath)
      {
        case "buttonSouth": return buttonSouth;
        case "buttonNorth": return buttonNorth;
        case "buttonEast": return buttonEast;
        case "buttonWest": return buttonWest;
        case "start": return startButton;
        case "select": return selectButton;
        case "leftTrigger": return leftTrigger;
        case "rightTrigger": return rightTrigger;
        case "leftShoulder": return leftShoulder;
        case "rightShoulder": return rightShoulder;
        case "dpad": return dpad;
        case "dpad/up": return dpadUp;
        case "dpad/down": return dpadDown;
        case "dpad/left": return dpadLeft;
        case "dpad/right": return dpadRight;
        case "leftStick": return leftStick;
        case "rightStick": return rightStick;
        case "leftStickPress": return leftStickPress;
        case "rightStickPress": return rightStickPress;
      }
      return null;
    }
  }
}
