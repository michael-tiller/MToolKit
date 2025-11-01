using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace MToolKit.Runtime.Components
{
  public class InputFieldWithText : MonoBehaviour
  {
    [field: SerializeField]
    [Required]
    public TMP_InputField InputField { get; private set; }

    [field: SerializeField]
    [Required]
    public TMP_Text Text { get; private set; }

    private void Reset()
    {
      InputField = GetComponentInChildren<TMP_InputField>();
      Text = GetComponentInChildren<TMP_Text>();
    }
  }
}