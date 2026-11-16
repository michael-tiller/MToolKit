using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using MToolKit.ContextMenus.Widgets;
using MToolKit.Theme.Spacing;
using MToolKit.Theme.Typeset;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Serilog;
using Object = UnityEngine.Object;

namespace MToolKit.ContextMenus.Presentation
{
  /// <summary>Owns only a widget root. Custom prefab components retain ownership of all private children.</summary>
  public sealed class MenuWidgetInstance : IDisposable
  {
    private readonly IMenuWidget[] widgets;
    private readonly IMenuSecondaryPrefabReceiver[] secondaryReceivers;
    internal MenuWidgetInstance(GameObject instance, bool defaults)
    {
      GameObject = instance;
      Rect = instance.GetComponent<RectTransform>();
      if (Rect == null) throw new InvalidOperationException("Menu widget root requires a RectTransform: " + instance.name);
      var components = instance.GetComponents<MonoBehaviour>();
      var found = new List<IMenuWidget>();
      var receivers = new List<IMenuSecondaryPrefabReceiver>();
      for (int i = 0; i < components.Length; i++) if (components[i] is IMenuWidget widget) found.Add(widget);
      for (int i = 0; i < components.Length; i++)
        if (components[i] is IMenuSecondaryPrefabReceiver receiver) receivers.Add(receiver);
      if (found.Count == 0) throw new InvalidOperationException("Menu widget root must implement IMenuWidget: " + instance.name);
      widgets = found.ToArray();
      secondaryReceivers = receivers.ToArray();
      DefaultWidget = defaults ? instance.GetComponent<MenuActionWidget>() : null;
    }
    public GameObject GameObject { get; }
    public RectTransform Rect { get; }
    public MenuActionWidget DefaultWidget { get; }
    public MenuWidgetState State { get; private set; }
    public void ConfigureSecondaryPrefab(GameObject prefab)
    {
      if (prefab == null) return;
      for (int i = 0; i < secondaryReceivers.Length; i++)
      {
        if (secondaryReceivers[i] is Object owner && owner == null) continue;
        secondaryReceivers[i].ConfigureSecondaryPrefab(prefab);
      }
    }
    public void Bind(MenuWidgetState state)
    {
      if (GameObject == null) return;
      State = state;
      GameObject.SetActive(state != null && state.Availability.Visible);
      for (int i = 0; i < widgets.Length; i++)
      {
        if (widgets[i] is Object owner && owner == null) continue;
        widgets[i].Bind(state);
      }
    }
    public void Unbind()
    {
      State = null;
      Exception firstFailure = null;
      for (int i = 0; i < widgets.Length; i++)
      {
        if (widgets[i] is Object owner && owner == null) continue;
        try { widgets[i].Unbind(); }
        catch (Exception exception)
        {
          if (firstFailure == null) firstFailure = exception;
          else Log.ForContext<MenuWidgetInstance>().Error(exception,
            "Additional menu widget cleanup failed for {WidgetType}", widgets[i].GetType().FullName);
        }
      }
      try { if (GameObject != null) GameObject.SetActive(false); }
      catch (Exception exception)
      {
        if (firstFailure == null) firstFailure = exception;
        else Log.ForContext<MenuWidgetInstance>().Error(exception, "Menu widget deactivation failed after a cleanup failure");
      }
      if (firstFailure != null) ExceptionDispatchInfo.Capture(firstFailure).Throw();
    }
    public void Dispose()
    {
      try { Unbind(); }
      finally
      {
        if (GameObject != null)
        {
          if (Application.isPlaying) Object.Destroy(GameObject); else Object.DestroyImmediate(GameObject);
        }
      }
    }
  }

  /// <summary>Complete native defaults, used only when a caller did not supply a widget prefab.</summary>
  public static class MenuWidgetFactory
  {
    public static MenuWidgetInstance CreateItem(Transform parent, GameObject prefab = null)
    {
      if (prefab != null) return Custom(parent, prefab);
      var root = Rect("Menu action", parent);
      var image = root.gameObject.AddComponent<Image>();
      var button = root.gameObject.AddComponent<Button>();
      button.targetGraphic = image;
      button.transition = Selectable.Transition.None;
      var content = Rect("Content", root); Stretch(content, Vector2.zero, Vector2.zero);
      var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
      layout.padding = new RectOffset(8, 8, 4, 4); layout.spacing = 10;
      layout.childAlignment = TextAnchor.MiddleCenter;
      layout.childControlWidth = true; layout.childControlHeight = true;
      layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
      var spacing = content.gameObject.AddComponent<SpacingPreset>(); spacing.ConfigureTarget(layout);
      var icon = Rect("Icon", content).gameObject.AddComponent<Image>();
      icon.raycastTarget = false;
      icon.preserveAspect = true;
      icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, .5f);
      icon.rectTransform.anchoredPosition = new Vector2(20, 0);
      icon.rectTransform.sizeDelta = new Vector2(24, 24);
      var iconSize = icon.gameObject.AddComponent<LayoutElement>();
      iconSize.minWidth = iconSize.preferredWidth = 24; iconSize.minHeight = iconSize.preferredHeight = 24;
      var label = Text("Label", content, 18);
      var labelSize = label.gameObject.AddComponent<LayoutElement>(); labelSize.flexibleWidth = 1;
      var marker = Rect("Selected marker", root).gameObject.AddComponent<Image>();
      marker.raycastTarget = false;
      marker.rectTransform.anchorMin = new Vector2(0, 0);
      marker.rectTransform.anchorMax = new Vector2(0, 1);
      marker.rectTransform.pivot = new Vector2(0, .5f);
      marker.rectTransform.sizeDelta = new Vector2(4, 0);
      var check = Rect("Checked marker", root).gameObject.AddComponent<Image>();
      check.raycastTarget = false;
      check.rectTransform.anchorMin = check.rectTransform.anchorMax = new Vector2(1, 1);
      check.rectTransform.anchoredPosition = new Vector2(-8, -8);
      check.rectTransform.sizeDelta = new Vector2(6, 6);
      var widget = root.gameObject.AddComponent<MenuActionWidget>();
      widget.Background = image;
      widget.SpacingTargets = new[] { spacing };
      widget.Configure(label, icon, marker, check, button);
      return new MenuWidgetInstance(root.gameObject, true);
    }

    public static MenuWidgetInstance CreateCenter(Transform parent, GameObject prefab = null, GameObject secondaryPrefab = null)
    {
      if (prefab != null)
      {
        var custom = Custom(parent, prefab);
        custom.ConfigureSecondaryPrefab(secondaryPrefab);
        return custom;
      }
      var root = Rect("Menu center", parent);
      var background = root.gameObject.AddComponent<Image>();
      background.raycastTarget = false;
      var content = Rect("Main content", root); Stretch(content, new Vector2(5, 0), new Vector2(5, 0));
      var mainLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
      mainLayout.childAlignment = TextAnchor.MiddleCenter;
      mainLayout.childControlWidth = true; mainLayout.childControlHeight = true;
      mainLayout.childForceExpandWidth = true; mainLayout.childForceExpandHeight = false;
      var mainSpacing = content.gameObject.AddComponent<SpacingPreset>(); mainSpacing.ConfigureTarget(mainLayout);
      var title = Text("Title", content, 19);
      var description = Text("Description", content, 16);
      var unavailable = Text("Availability", content, 16);
      var binding = Text("Binding", content, 16);
      Place(title.rectTransform, new Vector2(0, 48), new Vector2(160, 28));
      Place(description.rectTransform, new Vector2(0, 16), new Vector2(160, 48));
      description.textWrappingMode = TextWrappingModes.Normal;
      title.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
      description.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
      unavailable.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
      binding.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
      Place(unavailable.rectTransform, new Vector2(0, -24), new Vector2(160, 28));
      Place(binding.rectTransform, new Vector2(0, -48), new Vector2(160, 28));
      var secondary = Rect("Secondary actions", root);
      Place(secondary, new Vector2(0, -100), new Vector2(160, 64));
      var layout = secondary.gameObject.AddComponent<VerticalLayoutGroup>();
      layout.childControlHeight = false; layout.childControlWidth = true;
      layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
      layout.spacing = 4;
      var secondarySpacing = secondary.gameObject.AddComponent<SpacingPreset>(); secondarySpacing.ConfigureTarget(layout);
      var widget = root.gameObject.AddComponent<MenuCenterWidget>();
      widget.Background = background;
      widget.SpacingTargets = new[] { mainSpacing, secondarySpacing };
      widget.Configure(title, description, disabledReason: unavailable, bindingHint: binding, secondaryContainer: secondary);
      var result = new MenuWidgetInstance(root.gameObject, true);
      result.ConfigureSecondaryPrefab(secondaryPrefab);
      return result;
    }

    private static MenuWidgetInstance Custom(Transform parent, GameObject prefab)
    {
      var instance = Object.Instantiate(prefab, parent, false);
      try { return new MenuWidgetInstance(instance, false); }
      catch
      {
        if (Application.isPlaying) Object.Destroy(instance); else Object.DestroyImmediate(instance);
        throw;
      }
    }

    public static RectTransform Rect(string name, Transform parent)
    {
      var root = new GameObject(name, typeof(RectTransform));
      root.transform.SetParent(parent, false);
      return (RectTransform)root.transform;
    }

    public static TMP_Text Text(string name, Transform parent, float size = 18)
    {
      var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
      text.fontSize = size;
      text.enableAutoSizing = false;
      text.raycastTarget = false;
      text.alignment = TextAlignmentOptions.Center;
      text.textWrappingMode = TextWrappingModes.NoWrap;
      text.overflowMode = TextOverflowModes.Ellipsis;
      var preset = text.gameObject.AddComponent<TypesetPreset>(); preset.ConfigureTarget(text);
      return text;
    }

    internal static void Place(RectTransform root, Vector2 position, Vector2 size)
    {
      root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
      root.anchoredPosition = position;
      root.sizeDelta = size;
    }

    private static void Stretch(RectTransform root, Vector2 minimum, Vector2 maximum)
    {
      root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
      root.offsetMin = minimum; root.offsetMax = -maximum;
    }
  }
}
