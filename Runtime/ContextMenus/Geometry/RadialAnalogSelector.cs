using System;
using UnityEngine;

namespace MToolKit.ContextMenus.Geometry
{
  /// <summary>Event-driven stick selection; focused-ring ownership is explicit, never inferred from magnitude.</summary>
  public sealed class RadialAnalogSelector
  {
    private readonly double _deadZoneSquared;
    private readonly float _hysteresisDegrees;
    private RadialRingGeometry _focusedRing;

    public int SelectedIndex { get; private set; } = -1;

    public RadialAnalogSelector(float deadZone, float hysteresisDegrees)
    {
      if (!RadialGeometryMath.IsFinite(deadZone) || deadZone < 0 || deadZone >= 1)
        throw new ArgumentOutOfRangeException(nameof(deadZone), "Analog dead zone must be finite, at least zero, and below one.");
      if (!RadialGeometryMath.IsFinite(hysteresisDegrees) || hysteresisDegrees < 0)
        throw new ArgumentOutOfRangeException(nameof(hysteresisDegrees), "Hysteresis must be finite and nonnegative.");
      _deadZoneSquared = (double)deadZone * deadZone;
      _hysteresisDegrees = hysteresisDegrees;
    }

    public int Select(Vector2 stick, RadialRingGeometry focusedRing)
    {
      if (!ReferenceEquals(_focusedRing, focusedRing))
      {
        SelectedIndex = -1;
        _focusedRing = focusedRing;
      }
      double magnitudeSquared = (double)stick.x * stick.x + (double)stick.y * stick.y;
      if (focusedRing == null || !RadialGeometryMath.IsFinite(stick) || magnitudeSquared <= _deadZoneSquared)
        return SelectedIndex = -1;
      return SelectedIndex = focusedRing.SelectDirection(stick, SelectedIndex, _hysteresisDegrees);
    }

    public void Reset()
    {
      SelectedIndex = -1;
      _focusedRing = null;
    }
  }
}
