using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Settings.Interfaces;
using UnityEngine;

namespace MToolKit.Runtime.Settings.UI.Abstract
{
    
    public abstract class AbstractSettingsInitializer : MonoBehaviour, ISettingsInitializer
    {
        public abstract UniTask ConfigureAsync();
    }
}
