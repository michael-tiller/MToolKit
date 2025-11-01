using MToolKit.Runtime.Navigation.Interfaces;
using UnityEngine;

namespace MToolKit.Runtime.Navigation.Views
{

  public class Subview : MonoBehaviour, ISubview
  {

    public string DisplayName = "Subview";
    public virtual void SetVisible(bool visible)
    {
      if (gameObject.activeSelf == visible) return;
      gameObject.SetActive(visible);
    }
  }
}