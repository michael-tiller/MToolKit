// Navigation/DataStructures/CanvasTransformsDict.cs

using System;
using MToolKit.Runtime.Navigation.Enums;
using MToolKit.Runtime.Utilities.SerializableDictionary;
using UnityEngine;

namespace MToolKit.Runtime.Navigation.DataStructures
{
    [Serializable]
    public class CanvasTransformsDict : SerializableDictionary<ECanvasType, Transform> { }
}
