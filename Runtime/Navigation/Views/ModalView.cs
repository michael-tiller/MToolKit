
// Navigation/Views/ModalView.cs

using MToolKit.Runtime.Settings.Enums;
using MToolKit.Runtime.Settings.UI;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace MToolKit.Runtime.Navigation.Views
{
    public class ModalView : View
    {
        [SerializeField, Required] protected TextMeshProUGUI titleText;
        [SerializeField, Required] protected TextMeshProUGUI messageText;
        
        [SerializeField, Required] protected ModalButton button1, button2, button3;
        
        public virtual void Initialize(string title, string message, EModalButtonType type1, string text1, UnityAction action1,  EModalButtonType type2 = EModalButtonType.None, string text2 = null, 
             UnityAction action2 = null, EModalButtonType type3 = EModalButtonType.None, string text3 = null, UnityAction action3 = null)
        {
            titleText.text = title;
            messageText.text = message;

            if (type1 != EModalButtonType.None)
            {
                button1.gameObject.SetActive(true);
                button1.Setup(type1,action1, text1);
            }
            else
            {
                button1.gameObject.SetActive(false);
            }
            if (type2 != EModalButtonType.None)
            {
                button2.gameObject.SetActive(true);
                button2.Setup(type2, action2, text2);
            }
            else
            {
                button2.gameObject.SetActive(false);
            }
            if (type3 != EModalButtonType.None)
            {
                button3.gameObject.SetActive(true);
                button3.Setup(type3, action3, text3);
            }
            else
            {
                button3.gameObject.SetActive(false);
            }

            if (button1.gameObject.activeSelf || button2.gameObject.activeSelf || button3.gameObject.activeSelf)
            {
                if (button1.gameObject.activeSelf && button1.Instance != null)
                {
                    EventSystem.current.SetSelectedGameObject(button1.Instance.gameObject);
                }
                else if (button2.gameObject.activeSelf && button2.Instance != null)
                {
                    EventSystem.current.SetSelectedGameObject(button2.Instance.gameObject);
                }
                else if (button3.gameObject.activeSelf && button3.Instance != null)
                {
                    EventSystem.current.SetSelectedGameObject(button3.Instance.gameObject);
                }
            }
            
        }

        public override void Show()
        {
            base.Show();
        }
    }
}
