using System;
using MToolKit.Runtime.Navigation.Enums;
using MToolKit.Runtime.Navigation.Views;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace MToolKit.Runtime.Navigation.DataStructures
{
  [Serializable]
  public struct NavigationRequestMessageBody
  {
    [FormerlySerializedAs("canvasType")]
    [SerializeField]
    [Required]
    public ECanvasType CanvasType;

    [FormerlySerializedAs("view")]
    [SerializeField]
    [Required]
    public View View;
  }
}