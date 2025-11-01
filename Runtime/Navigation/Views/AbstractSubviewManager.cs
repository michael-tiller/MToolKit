using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.Navigation.Views
{
    public abstract class AbstractSubviewManager<T> : MonoBehaviour where T : Subview
    {
        [SerializeField, Required] protected List<T> subviews = new();

        [SerializeField, Required] protected T defaultSubview;

        [ShowInInspector, ReadOnly]
        protected T lastSubview;
        
        public T LastSubview { get { return lastSubview; } }
        
        public event Action<T> OnSubviewChanged;

        protected virtual void Start()
        {
            if (defaultSubview != null)
                ShowSubview(defaultSubview);
        }

        public virtual void ShowSubview(T subview)
        {
            subviews.ForEach(sv =>
            {
                if (sv != subview)
                    sv.SetVisible(false);
            });

        if (subview != null && subviews.Contains(subview) && lastSubview != subview)
            {
                lastSubview = subview;
                lastSubview.SetVisible(true);
                OnSubviewChanged?.Invoke(lastSubview);
            }
        }
    }
}