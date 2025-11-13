// Navigation/Views/ModalView.cs

using MToolKit.Runtime.Navigation.DataStructures;
using MToolKit.Runtime.Settings.Enums;
using MToolKit.Runtime.Settings.UI;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MToolKit.Runtime.Navigation.Views
{
  public class ModalView : View
  {
    [SerializeField]
    [Required]
    protected TextMeshProUGUI titleText;

    [SerializeField]
    [Required]
    protected TextMeshProUGUI messageText;

    [SerializeField]
    [Required]
    protected ModalButton button1Object, button2Object, button3Object;

    [ShowInInspector]
    [ReadOnly]
    protected ModalButtonConfig button1, button2, button3;

    public virtual void Initialize(string title, string message, ModalButtonConfig button1Config, ModalButtonConfig button2Config = default,
      ModalButtonConfig button3Config = default)
    {
      titleText.text = title;
      messageText.text = message;


      if (button1Config.Type != EModalButtonType.None)
      {
        button1Object.gameObject.SetActive(true);
        button1Object.Setup(button1Config.Type, button1Config.Action, button1Config.Text);
      }
      else
      {
        button1Object.gameObject.SetActive(false);
      }
      if (button2Config.Type != EModalButtonType.None)
      {
        button2Object.gameObject.SetActive(true);
        button2Object.Setup(button2Config.Type, button2Config.Action, button2Config.Text);
      }
      else
      {
        button2Object.gameObject.SetActive(false);
      }
      if (button3Config.Type != EModalButtonType.None)
      {
        button3Object.gameObject.SetActive(true);
        button3Object.Setup(button3Config.Type, button3Config.Action, button3Config.Text);
      }
      else
      {
        button3Object.gameObject.SetActive(false);
      }

      if (button1Object.gameObject.activeSelf || button2Object.gameObject.activeSelf || button3Object.gameObject.activeSelf)
      {
        if (button1Object.gameObject.activeSelf && button1Object.Instance != null)
          EventSystem.current.SetSelectedGameObject(button1Object.gameObject);
        else if (button2Object.gameObject.activeSelf && button2Object.Instance != null)
          EventSystem.current.SetSelectedGameObject(button2Object.gameObject);
        else if (button3Object.gameObject.activeSelf && button3Object.Instance != null)
          EventSystem.current.SetSelectedGameObject(button3Object.gameObject);
      }
    }
  }
}