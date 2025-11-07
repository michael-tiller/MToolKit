using MToolKit.Runtime.Navigation.Views;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace MToolKit.Runtime.Settings.UI
{
    [RequireComponent(typeof(Button))]
    public class SubviewButton : MonoBehaviour
    {
        [SerializeField, Required]
        private SubviewManager subviewManager;
        [SerializeField, Required]
        private Subview targetSubview;
        [SerializeField, Required]
        private Button button;

        private void Reset()
        {
            button = GetComponent<Button>();
        }

        private void Start()
        {
            button.onClick.RemoveListener(OnClickSetSubview);
            button.onClick.AddListener(OnClickSetSubview);

            subviewManager.OnSubviewChanged -= OnSubviewChangedHandler;
            subviewManager.OnSubviewChanged += OnSubviewChangedHandler;
            OnSubviewChangedHandler(subviewManager.LastSubview);
        }

        private void OnSubviewChangedHandler(Subview subview)
        {
            //button.interactable = subview != targetSubview;
        }

        private void OnDestroy()
        {
            button.onClick.RemoveListener(OnClickSetSubview);
            subviewManager.OnSubviewChanged -= OnSubviewChangedHandler;
        }

        private void OnClickSetSubview()
        {
            subviewManager.ShowSubview(targetSubview);
        }

    }
}
