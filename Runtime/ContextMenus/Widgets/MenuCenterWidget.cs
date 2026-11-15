using System.Collections.Generic;
using MToolKit.ContextMenus.Configuration;
using Serilog;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MToolKit.ContextMenus.Widgets
{
  /// <summary>Opt-in root contract for a center whose secondary widget composition accepts profile overrides.</summary>
  public interface IMenuSecondaryPrefabReceiver
  {
    void ConfigureSecondaryPrefab(GameObject prefab);
  }

  /// <summary>Optional center information, including pooled secondary widgets bound to the same shared state.</summary>
  public class MenuCenterWidget : MenuActionWidget, IMenuSecondaryPrefabReceiver
  {
    [field: SerializeField] public TMP_Text DisabledReason { get; set; }
    [field: SerializeField] public RectTransform SecondaryContainer { get; set; }
    [field: SerializeField] public GameObject SecondaryPrefab { get; set; }

    private readonly List<GameObject> secondaryInstances = new List<GameObject>();
    private readonly List<IMenuWidget[]> secondaryWidgets = new List<IMenuWidget[]>();
    private GameObject pooledPrefab;

    public void ConfigureSecondaryPrefab(GameObject prefab)
    {
      // A missing profile override retains the composition authored on this prefab.
      if (prefab != null) SecondaryPrefab = prefab;
    }

    public void Configure(TMP_Text title, TMP_Text description, Image icon = null,
      TMP_Text disabledReason = null, TMP_Text bindingHint = null,
      RectTransform secondaryContainer = null, GameObject secondaryPrefab = null)
    {
      base.Configure(title, icon, null, null, null, description);
      DisabledReason = disabledReason; BindingHint = bindingHint;
      SecondaryContainer = secondaryContainer; SecondaryPrefab = secondaryPrefab;
    }

    public override void Bind(MenuWidgetState state)
    {
      base.Bind(state);
      if (state == null || SecondaryPrefab == null || SecondaryContainer == null) return;
      if (pooledPrefab != SecondaryPrefab)
      {
        ClearPool();
        pooledPrefab = SecondaryPrefab;
      }
      for (int i = 0; i < state.SecondaryStates.Count; i++)
      {
        if (i >= secondaryInstances.Count)
        {
          var instance = Instantiate(SecondaryPrefab, SecondaryContainer);
          var components = instance.GetComponents<MonoBehaviour>();
          var widgets = new List<IMenuWidget>();
          for (int component = 0; component < components.Length; component++)
            if (components[component] is IMenuWidget widget) widgets.Add(widget);
          if (widgets.Count == 0)
            Log.Logger.ForContext<MenuCenterWidget>().Warning("Menu secondary prefab {Prefab} has no IMenuWidget on its root", SecondaryPrefab.name);
          secondaryInstances.Add(instance);
          secondaryWidgets.Add(widgets.ToArray());
        }
        var secondary = state.SecondaryStates[i];
        secondaryInstances[i].SetActive(secondary != null && secondary.Availability.Visible);
        for (int widget = 0; widget < secondaryWidgets[i].Length; widget++) secondaryWidgets[i][widget].Bind(secondary);
      }
    }

    public override void Unbind()
    {
      for (int i = 0; i < secondaryInstances.Count; i++)
      {
        for (int widget = 0; widget < secondaryWidgets[i].Length; widget++) secondaryWidgets[i][widget].Unbind();
        if (secondaryInstances[i] != null) secondaryInstances[i].SetActive(false);
      }
      base.Unbind();
    }

    protected override void ApplyVisuals()
    {
      base.ApplyVisuals();
      if (State == null) return;
      if (DisabledReason != null) DisabledReason.text = State.Availability.DisabledReason ?? string.Empty;
      if (UseSkin)
      {
        Tint(Background, MenuVisualRole.Center);
        Tint(DisabledReason, MenuVisualRole.Description);
      }
    }

    private void ClearPool()
    {
      for (int i = 0; i < secondaryInstances.Count; i++)
      {
        if (secondaryInstances[i] == null) continue;
        if (Application.isPlaying) Destroy(secondaryInstances[i]); else DestroyImmediate(secondaryInstances[i]);
      }
      secondaryInstances.Clear();
      secondaryWidgets.Clear();
    }

    protected override void OnDestroy()
    {
      base.OnDestroy();
      ClearPool();
    }
  }
}
