using System;
using MToolKit.ContextMenus.Configuration;

namespace MToolKit.Editor.ContextMenus
{
  [Serializable]
  public struct MenuListLayout
  {
    public float ListWidth;
    public float RowHeight;
    public static MenuListLayout Default => new MenuListLayout { ListWidth = 380, RowHeight = 48 };
    public void ApplyTo(MenuProfile profile)
    {
      if (profile == null) throw new ArgumentNullException(nameof(profile));
      profile.ListWidth = ListWidth; profile.RowHeight = RowHeight;
    }
  }

  [Serializable]
  public struct MenuWheelLayout
  {
    public float InnerRadius;
    public float RingWidth;
    public float StartAngle;
    public bool Clockwise;
    public float AngularGap;
    public bool DrawSegments;
    public static MenuWheelLayout Default => new MenuWheelLayout {
      InnerRadius = 166, RingWidth = 90, StartAngle = 90, Clockwise = true, AngularGap = 3, DrawSegments = true
    };
    public void ApplyTo(MenuProfile profile)
    {
      if (profile == null) throw new ArgumentNullException(nameof(profile));
      profile.InnerRadius = InnerRadius; profile.RingWidth = RingWidth;
      profile.StartAngle = StartAngle; profile.Clockwise = Clockwise;
      profile.AngularGap = AngularGap; profile.DrawSegments = DrawSegments;
    }
  }

  [Serializable]
  public struct MenuNestedWheelLayout
  {
    public MenuWheelLayout InnerRing;
    public float RingGap;
    public float ChildRingWidth;
    public float ChildArcDegrees;
    public static MenuNestedWheelLayout Default => new MenuNestedWheelLayout {
      InnerRing = new MenuWheelLayout { InnerRadius = 166, RingWidth = 60, StartAngle = 90,
        Clockwise = true, AngularGap = 3, DrawSegments = true },
      RingGap = 12, ChildRingWidth = 55, ChildArcDegrees = 120
    };
    public void ApplyTo(MenuProfile profile)
    {
      InnerRing.ApplyTo(profile);
      profile.RingGap = RingGap; profile.ChildRingWidth = ChildRingWidth; profile.ChildArcDegrees = ChildArcDegrees;
    }
  }
}
