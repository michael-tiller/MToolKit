using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace MToolKit.Runtime.Components
{
  [RequireComponent(typeof(TextMeshProUGUI))]
  public class VersionLabel : MonoBehaviour
  {
    [SerializeField]
    [Required]
    private TextMeshProUGUI label;

    [SerializeField]
    private string prefix = "V";

    private void Reset()
    {
      label = GetComponent<TextMeshProUGUI>();
    }

    private void Start()
    {
      label.text = $"{prefix} {Application.version}";
    }
  }
}