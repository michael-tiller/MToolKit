
// Navigation/Views/ModalView.cs

using MToolKit.Runtime.Settings.Enums;
using MToolKit.Runtime.Settings.UI;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using MToolKit.Runtime.Navigation.DataStructures;

namespace MToolKit.Runtime.Navigation.Views
{
    public class ModalView : View
    {
        [SerializeField, Required] protected TextMeshProUGUI titleText;
        [SerializeField, Required] protected TextMeshProUGUI messageText;

        [SerializeField, Required] protected ModalButton button1Object, button2Object, button3Object;

        [ShowInInspector, ReadOnly] protected ModalButtonConfig button1, button2, button3;

        public virtual void Initialize(string title, string message, ModalButtonConfig button1, ModalButtonConfig button2 = default, ModalButtonConfig button3 = default)
        {
            titleText.text = title;
            messageText.text = message;


            if (button1.type != EModalButtonType.None)
            {
                button1Object.gameObject.SetActive(true);
                button1Object.Setup(button1.type, button1.action, button1.text);
            }
            else
            {
                button1Object.gameObject.SetActive(false);
            }
            if (button2.type != EModalButtonType.None)
            {
                button2Object.gameObject.SetActive(true);
                button2Object.Setup(button2.type, button2.action, button2.text);
            }
            else
            {
                button2Object.gameObject.SetActive(false);
            }
            if (button3.type != EModalButtonType.None)
            {
                button3Object.gameObject.SetActive(true);
                button3Object.Setup(button3.type, button3.action, button3.text);
            }
            else
            {
                button3Object.gameObject.SetActive(false);
            }

            if (button1Object.gameObject.activeSelf || button2Object.gameObject.activeSelf || button3Object.gameObject.activeSelf)
            {
                if (button1Object.gameObject.activeSelf && button1Object.Instance != null)
                {
                    EventSystem.current.SetSelectedGameObject(button1Object.gameObject);
                }
                else if (button2Object.gameObject.activeSelf && button2Object.Instance != null)
                {
                    EventSystem.current.SetSelectedGameObject(button2Object.gameObject);
                }
                else if (button3Object.gameObject.activeSelf && button3Object.Instance != null)
                {
                    EventSystem.current.SetSelectedGameObject(button3Object.gameObject);
                }
            }

        }

        public override void Show()
        {
            base.Show();
        }
    }
}
