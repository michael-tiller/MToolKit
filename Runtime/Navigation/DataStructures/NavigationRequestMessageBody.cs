using System;
using MToolKit.Runtime.Navigation.Enums;
using MToolKit.Runtime.Navigation.Views;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.Navigation.DataStructures
{
    [Serializable]
    public struct NavigationRequestMessageBody
    {
        [SerializeField, Required]
        public ECanvasType canvasType;
        [SerializeField, Required]
        public View view;
    }
}