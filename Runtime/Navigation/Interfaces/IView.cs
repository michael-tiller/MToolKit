// Navigation/Interfaces/IView.cs

using UnityEngine;

namespace MToolKit.Runtime.Navigation.Interfaces
{
  public interface IView
  {
    void Show();
    void Hide();
    GameObject GameObject { get; }
  }
}