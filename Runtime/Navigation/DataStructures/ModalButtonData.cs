
using MToolKit.Runtime.Settings.UI;
using MToolKit.Runtime.Settings.Enums;
using UnityEngine.Events;
using System;

namespace MToolKit.Runtime.Navigation.DataStructures
{

  [Serializable]
  public struct ModalButtonConfig
  {
    public EModalButtonType type;
    public string text;
    public UnityAction action;

    public ModalButtonConfig(EModalButtonType type, string text, UnityAction action)
    {
      this.type = type;
      this.text = text;
      this.action = action;
    }
  }
}