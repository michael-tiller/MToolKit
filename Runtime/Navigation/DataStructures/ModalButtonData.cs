using System;
using MToolKit.Runtime.Settings.Enums;
using UnityEngine;
using UnityEngine.Events;

namespace MToolKit.Runtime.Navigation.DataStructures
{
  [Serializable]
  public struct ModalButtonConfig
  {
    [SerializeField]
    private EModalButtonType type;

    [SerializeField]
    private string text;

    private UnityAction action;

    public EModalButtonType Type => type;
    public string Text => text;
    // ReSharper disable once ConvertToAutoPropertyWithPrivateSetter
    public UnityAction Action => action;

    public ModalButtonConfig(EModalButtonType type, string text, UnityAction action)
    {
      this.type = type;
      this.text = text;
      this.action = action;
    }
  }
}