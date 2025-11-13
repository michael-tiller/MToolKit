// Navigation/DataStructures/CanvasConfigDict.cs

using System;
using MToolKit.Runtime.Navigation.Config;
using MToolKit.Runtime.Navigation.Enums;
using MToolKit.Runtime.Utilities.SerializableDictionary;

namespace MToolKit.Runtime.Navigation.DataStructures
{
  [Serializable]
  public class CanvasConfigDict : SerializableDictionary<ECanvasType, CanvasConfig> { }
}