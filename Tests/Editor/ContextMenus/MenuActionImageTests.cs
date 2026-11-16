using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Authoring;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using ThemeAsset = MToolKit.Theme.Theme;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuActionImageTests
  {
    private readonly List<Object> owned = new List<Object>();
    private readonly List<MenuActionWidget> widgets = new List<MenuActionWidget>();

    [TearDown]
    public void Cleanup()
    {
      foreach (var widget in widgets) if (widget != null) widget.Unbind();
      widgets.Clear();
      for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
      owned.Clear();
    }
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private T Asset<T>() where T : ScriptableObject => Own(ScriptableObject.CreateInstance<T>());
    private Sprite Sprite(string name)
    {
      var texture = Own(new Texture2D(2, 2));
      var sprite = Own(UnityEngine.Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f)));
      sprite.name = name; return sprite;
    }
    private Image Graphic(Transform parent, string name)
    {
      var value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
      value.transform.SetParent(parent, false); return value.GetComponent<Image>();
    }
    private MenuActionWidget Widget(bool targets = true)
    {
      var root = Own(new GameObject("Image action widget", typeof(RectTransform)));
      var widget = root.AddComponent<MenuActionWidget>(); widgets.Add(widget);
      if (targets)
      {
        widget.Icon = Graphic(root.transform, "Compact icon");
        SetImageTarget(widget, Graphic(root.transform, "Full color artwork"));
      }
      return widget;
    }

    // Reflection keeps these RED tests compilable before the additive public API exists.
    private static MenuAction Action(string id, Sprite icon = null, Sprite image = null)
    {
      var constructor = typeof(MenuAction).GetConstructors().Single();
      var parameters = constructor.GetParameters();
      Assert.That(parameters.Last().Name, Is.EqualTo("image"), "Append the independent optional image argument after existing constructor parameters.");
      Assert.That(parameters.Last().ParameterType, Is.EqualTo(typeof(Sprite)));
      Assert.That(parameters.Last().HasDefaultValue, Is.True);
      Assert.That(parameters.Last().DefaultValue, Is.Null);
      var values = parameters.Select(parameter => parameter.HasDefaultValue ? parameter.DefaultValue : null).ToArray();
      values[0] = new MenuActionId(id); values[1] = new MenuLabel(id);
      values[Array.FindIndex(parameters, parameter => parameter.Name == "icon")] = icon;
      values[values.Length - 1] = image;
      return (MenuAction)constructor.Invoke(values);
    }
    private static Sprite ImageOf(object value)
    {
      var property = value.GetType().GetProperty("Image");
      Assert.That(property, Is.Not.Null, value.GetType().Name + " must expose the independent Image sprite.");
      Assert.That(property.PropertyType, Is.EqualTo(typeof(Sprite)));
      return (Sprite)property.GetValue(value);
    }
    private static PropertyInfo ImageTargetProperty()
    {
      var property = typeof(MenuActionWidget).GetProperty("ActionImage");
      Assert.That(property, Is.Not.Null, "A full-color artwork target must be separate from the themed Icon target.");
      Assert.That(property.PropertyType, Is.EqualTo(typeof(Image)));
      return property;
    }
    private static void SetImageTarget(MenuActionWidget widget, Image target) => ImageTargetProperty().SetValue(widget, target);
    private static Image ImageTarget(MenuActionWidget widget) => (Image)ImageTargetProperty().GetValue(widget);
    private static void SetAuthoredImage(MenuActionDefinitionSO definition, Sprite image)
    {
      var field = typeof(MenuActionDefinitionSO).GetField("Image");
      Assert.That(field, Is.Not.Null, "Authored definitions must provide their own optional image without replacing Icon.");
      Assert.That(field.FieldType, Is.EqualTo(typeof(Sprite))); field.SetValue(definition, image);
    }
    private ImageDefinition Definition(string id, MenuAction source)
    {
      var definition = Asset<ImageDefinition>(); definition.Id = id; definition.Label = id; definition.Source = source;
      return definition;
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void IconAndImageAreIndependentAcrossDescriptorsStateAndThemedWidgets(bool withIcon, bool withImage)
    {
      var icon = withIcon ? Sprite("Compact") : null; var artwork = withImage ? Sprite("Artwork") : null;
      var action = Action("action", icon, artwork);
      var widget = Widget(); var target = ImageTarget(widget);
      var authoredTint = new Color(.25f, .7f, .45f, .8f); target.color = authoredTint;
      var skin = Asset<MenuSkin>(); skin.SetColor(MenuVisualRole.Icon, Color.red);
      using var service = new MenuThemeLayerTests.FakeThemeService(Asset<ThemeAsset>());
      var state = new MenuWidgetState(action, MenuActionState.Available, skin: skin, themeService: service);

      widget.Bind(state);

      Assert.That(action.Icon, Is.SameAs(icon)); Assert.That(ImageOf(action), Is.SameAs(artwork));
      Assert.That(state.Icon, Is.SameAs(icon)); Assert.That(ImageOf(state), Is.SameAs(artwork));
      Assert.That(ImageOf(state.WithLabel("Relocalized")), Is.SameAs(artwork));
      Assert.That(widget.Icon.sprite, Is.SameAs(icon)); Assert.That(widget.Icon.enabled, Is.EqualTo(withIcon));
      Assert.That(target.sprite, Is.SameAs(artwork)); Assert.That(target.enabled, Is.EqualTo(withImage));
      Assert.That(target.gameObject.activeSelf, Is.EqualTo(withImage), "An absent artwork child must not reserve layout space.");
      Assert.That(widget.Icon.color, Is.EqualTo(Color.red));
      Assert.That(target.color, Is.EqualTo(authoredTint), "Artwork retains its authored tint rather than receiving the semantic Icon color.");
      skin.SetColor(MenuVisualRole.Icon, Color.blue); service.SetTheme(Asset<ThemeAsset>());
      Assert.That(widget.Icon.color, Is.EqualTo(Color.blue)); Assert.That(target.color, Is.EqualTo(authoredTint));
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void AuthoredIconAndImageOverridesFallBackIndependently(bool overrideIcon, bool overrideImage)
    {
      var sourceIcon = Sprite("Source icon"); var sourceImage = Sprite("Source image");
      var authoredIcon = Sprite("Authored icon"); var authoredImage = Sprite("Authored image");
      var definition = Definition("authored", Action("source", sourceIcon, sourceImage));
      definition.Icon = overrideIcon ? authoredIcon : null;
      SetAuthoredImage(definition, overrideImage ? authoredImage : null);

      var action = definition.Create(new MenuContext());

      Assert.That(action.Icon, Is.SameAs(overrideIcon ? authoredIcon : sourceIcon));
      Assert.That(ImageOf(action), Is.SameAs(overrideImage ? authoredImage : sourceImage));
    }

    [TestCase("static")]
    [TestCase("dynamic")]
    [TestCase("secondary")]
    public void ScopedActionCopiesRetainBothSprites(string placement)
    {
      var icon = Sprite("Scoped icon"); var image = Sprite("Scoped artwork");
      var leaf = Definition("leaf", Action("source", icon, image));
      var parent = Asset<MenuSubmenuDefinitionSO>(); parent.Id = "root"; parent.Label = "Root";
      if (placement == "static") parent.Children = new[] { leaf };
      else if (placement == "secondary") parent.SecondaryActions = new[] { leaf };
      else
      {
        var provider = Asset<ImageProvider>(); provider.Action = Action("leaf", icon, image); parent.Provider = provider;
      }
      var context = new MenuContext(); var root = parent.Create(context);
      var copied = placement == "static" ? root.Children[0] : placement == "secondary" ? root.SecondaryActions[0] :
        root.ChildrenProvider.ResolveAsync(context, default).GetAwaiter().GetResult()[0];

      Assert.That(copied.Id.Value, Does.StartWith("root/"));
      Assert.That(copied.Icon, Is.SameAs(icon)); Assert.That(ImageOf(copied), Is.SameAs(image));
    }

    [Test]
    public void RebindAndUnbindClearMissingImagesWithoutRetainingPreviousSprites()
    {
      var icon = Sprite("Icon"); var image = Sprite("Artwork"); var widget = Widget();
      var target = ImageTarget(widget);
      widget.Bind(new MenuWidgetState(Action("both", icon, image), MenuActionState.Available));
      widget.Bind(new MenuWidgetState(Action("icon", icon), MenuActionState.Available));
      Assert.That(widget.Icon.sprite, Is.SameAs(icon));
      Assert.That(target.sprite, Is.Null); Assert.That(target.enabled, Is.False);
      Assert.That(target.gameObject.activeSelf, Is.False);
      widget.Bind(new MenuWidgetState(Action("image", image: image), MenuActionState.Available));
      Assert.That(widget.Icon.sprite, Is.Null); Assert.That(widget.Icon.enabled, Is.False);
      Assert.That(target.sprite, Is.SameAs(image));

      widget.Unbind();

      Assert.That(widget.Icon.sprite, Is.Null); Assert.That(widget.Icon.enabled, Is.False);
      Assert.That(target.sprite, Is.Null); Assert.That(target.enabled, Is.False);
      Assert.That(target.gameObject.activeSelf, Is.False);
      widget.Bind(new MenuWidgetState(Action("neither"), MenuActionState.Available));
      Assert.That(widget.Icon.sprite, Is.Null); Assert.That(target.sprite, Is.Null);
    }

    [Test]
    public void MissingOptionalVisualTargetsStillBindAndUnbindBothSpriteChannels()
    {
      var state = new MenuWidgetState(Action("both", Sprite("Icon"), Sprite("Artwork")), MenuActionState.Available);
      var widget = Widget(false);
      Assert.That(ImageTarget(widget), Is.Null);
      Assert.DoesNotThrow(() => widget.Bind(state)); Assert.DoesNotThrow(widget.Unbind);
      var rootTarget = widget.gameObject.AddComponent<Image>(); SetImageTarget(widget, rootTarget);
      widget.Bind(state);
      Assert.That(widget.gameObject.activeSelf, Is.True);
      widget.Bind(new MenuWidgetState(Action("empty"), MenuActionState.Available));
      Assert.That(rootTarget.enabled, Is.False);
      Assert.That(widget.gameObject.activeSelf, Is.True, "Hiding artwork on the widget root must not disable or unbind the whole widget.");
      widget.Unbind(); Assert.That(widget.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void CenterConfigurationForwardsTheOptionalIndependentArtworkTarget()
    {
      var root = Own(new GameObject("Image center", typeof(RectTransform)));
      var center = root.AddComponent<MenuCenterWidget>(); widgets.Add(center);
      var icon = Graphic(root.transform, "Icon"); var artwork = Graphic(root.transform, "Artwork");
      var configure = typeof(MenuCenterWidget).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Single(method => method.Name == "Configure");
      var parameters = configure.GetParameters();
      Assert.That(parameters.Last().Name, Is.EqualTo("actionImage"));
      Assert.That(parameters.Last().HasDefaultValue, Is.True);
      var values = parameters.Select(parameter => parameter.HasDefaultValue ? parameter.DefaultValue : null).ToArray();
      values[Array.FindIndex(parameters, parameter => parameter.Name == "icon")] = icon;
      values[values.Length - 1] = artwork;
      configure.Invoke(center, values);
      var sprite = Sprite("Center artwork");

      center.Bind(new MenuWidgetState(Action("center", image: sprite), MenuActionState.Available));

      Assert.That(center.Icon, Is.SameAs(icon)); Assert.That(ImageTarget(center), Is.SameAs(artwork));
      Assert.That(artwork.sprite, Is.SameAs(sprite)); Assert.That(icon.enabled, Is.False);
    }

    public sealed class ImageDefinition : MenuActionDefinitionSO
    {
      public MenuAction Source;
      protected override MenuAction CreateAction(MenuContext context) => Source;
    }
    public sealed class ImageProvider : MenuChildrenProviderSO
    {
      public MenuAction Action;
      public override UniTask<IReadOnlyList<MenuAction>> ResolveAsync(MenuContext context, CancellationToken cancellationToken)
        => UniTask.FromResult<IReadOnlyList<MenuAction>>(new[] { Action });
    }
  }
}
