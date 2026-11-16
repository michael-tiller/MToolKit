using System;
using System.Collections.Generic;
using MToolKit.ContextMenus.Configuration;
using MToolKit.Theme;
using MToolKit.Runtime.Input.Prompts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MToolKit.ContextMenus.Widgets
{
  /// <summary>The same snapshot binds list rows, radial content, centers and completely custom widgets.</summary>
  public interface IMenuWidget
  {
    void Bind(MenuWidgetState state);
    void Unbind();
  }

  /// <summary>Optional game adapter for existing tooltip/resource/variant data; MTK owns no business rules.</summary>
  public interface IMenuActionDetails
  {
    string CostText { get; }
    string VariantText { get; }
    float? Progress { get; }
  }

  public sealed class MenuWidgetState
  {
    public MenuWidgetState(MenuAction action, MenuActionState availability, bool selected = false,
      string label = null, string description = null, MenuSkin skin = null, IThemeService themeService = null,
      Action activate = null, Action select = null, string bindingHint = null,
      IReadOnlyList<MenuWidgetState> secondaryStates = null, IInputPromptService promptService = null,
      InputAction bindingAction = null, bool isHovered = false, Action<bool> hoverChanged = null, MenuThemeContext? theme = null)
    {
      Action = action;
      Availability = availability;
      IsSelected = selected;
      Label = label ?? action?.Label.ToString() ?? string.Empty;
      Description = description ?? action?.Description.ToString() ?? string.Empty;
      Skin = skin != null ? skin : theme?.Skin;
      ThemeService = themeService ?? theme?.ThemeService;
      Theme = new MenuThemeContext(Skin, theme?.SharedOverrides, theme?.Overrides, ThemeService);
      Activate = activate;
      Select = select;
      BindingHint = bindingHint ?? string.Empty;
      SecondaryStates = secondaryStates ?? Array.Empty<MenuWidgetState>();
      PromptService = promptService;
      BindingAction = bindingAction;
      IsHovered = isHovered;
      HoverChanged = hoverChanged;
    }

    public MenuAction Action { get; }
    public MenuActionState Availability { get; }
    public bool IsSelected { get; }
    public bool IsHovered { get; }
    public Action<bool> HoverChanged { get; }
    public string Label { get; }
    public string Description { get; }
    public string BindingHint { get; }
    public Sprite Icon => Action?.Icon;
    public Sprite Image => Action?.Image;
    public IMenuActionDetails Details => Action?.Metadata as IMenuActionDetails;
    public MenuSkin Skin { get; }
    public IThemeService ThemeService { get; }
    public MenuThemeContext Theme { get; }
    public MenuThemeState ThemeState => MenuThemeContext.SelectState(Availability, IsSelected, IsHovered);
    public Action Activate { get; }
    public Action Select { get; }
    public IReadOnlyList<MenuWidgetState> SecondaryStates { get; }
    public IInputPromptService PromptService { get; }
    public InputAction BindingAction { get; }
    public MenuWidgetState WithLabel(string label)
      => new MenuWidgetState(Action, Availability, IsSelected, label, Description, Skin, ThemeService,
        Activate, Select, BindingHint, SecondaryStates, PromptService, BindingAction, IsHovered, HoverChanged, Theme);
  }
}
