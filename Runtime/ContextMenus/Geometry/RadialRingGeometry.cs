using System;
using System.Collections.Generic;
using UnityEngine;

namespace MToolKit.ContextMenus.Geometry
{
  /// <summary>
  /// Cached geometry for an independently sized full ring or partial arc. Action identities and
  /// hierarchy depth belong to the menu session; this class only maps local geometry to indices.
  /// </summary>
  public sealed class RadialRingGeometry
  {
    private readonly RadialSegmentGeometry[] _segments;
    private readonly double _innerSquared;
    private readonly double _outerSquared;
    private readonly double _start;
    private readonly double _sweep;
    private readonly double _pitch;
    private readonly int _direction;

    public IReadOnlyList<RadialSegmentGeometry> Segments { get; }

    public RadialRingGeometry(int ringIndex, int segmentCount, float innerRadius,
      float outerRadius, float startAngle, float sweepAngle, float angularGap)
    {
      if (ringIndex < 0) throw new ArgumentOutOfRangeException(nameof(ringIndex), "Ring index must be nonnegative.");
      if (segmentCount <= 0) throw new ArgumentOutOfRangeException(nameof(segmentCount), "A ring needs at least one segment.");
      if (!RadialGeometryMath.IsFinite(innerRadius) || innerRadius < 0)
        throw new ArgumentOutOfRangeException(nameof(innerRadius), "Inner radius must be finite and nonnegative.");
      if (!RadialGeometryMath.IsFinite(outerRadius) || outerRadius <= innerRadius)
        throw new ArgumentOutOfRangeException(nameof(outerRadius), "Outer radius must be finite and larger than inner radius.");
      if (!RadialGeometryMath.IsFinite(startAngle))
        throw new ArgumentOutOfRangeException(nameof(startAngle), "Start angle must be finite.");
      if (!RadialGeometryMath.IsFinite(sweepAngle) || sweepAngle == 0 || Math.Abs(sweepAngle) > 360)
        throw new ArgumentOutOfRangeException(nameof(sweepAngle), "Signed sweep must be nonzero and no larger than one revolution.");

      _sweep = Math.Abs(sweepAngle);
      _pitch = _sweep / segmentCount;
      if (!RadialGeometryMath.IsFinite(angularGap) || angularGap < 0 || angularGap >= _pitch)
        throw new ArgumentOutOfRangeException(nameof(angularGap), "Angular gap must be smaller than a segment's angular span.");

      _innerSquared = (double)innerRadius * innerRadius;
      _outerSquared = (double)outerRadius * outerRadius;
      _start = RadialGeometryMath.Normalize(startAngle);
      _direction = Math.Sign(sweepAngle);
      _segments = new RadialSegmentGeometry[segmentCount];
      for (int i = 0; i < segmentCount; i++)
      {
        double visibleStart = _start + _direction * (_pitch * i + angularGap * .5);
        double visibleSweep = _direction * (_pitch - angularGap);
        _segments[i] = new RadialSegmentGeometry(ringIndex, i, innerRadius, outerRadius, visibleStart, visibleSweep);
      }
      Segments = Array.AsReadOnly(_segments);
    }

    /// <summary>Returns -1 for the center, gaps, arc complement, or outside the radial band.</summary>
    public int HitTest(Vector2 localPoint)
    {
      if (!RadialGeometryMath.IsFinite(localPoint)) return -1;
      double radiusSquared = (double)localPoint.x * localPoint.x + (double)localPoint.y * localPoint.y;
      if (radiusSquared < _innerSquared || radiusSquared >= _outerSquared) return -1;
      double angle = RadialGeometryMath.Angle(localPoint);
      for (int i = 0; i < _segments.Length; i++)
        if (_segments[i].ContainsAngle(angle)) return i;
      return -1;
    }

    /// <summary>
    /// Directional navigation deliberately ignores radial distance. The caller chooses the focused
    /// ring. Snapping fills angular gaps within this arc, never the complement of a partial arc.
    /// </summary>
    public int SelectDirection(Vector2 direction, int previousIndex,
      float hysteresisDegrees = 0, bool snapAcrossGaps = true)
    {
      if (!RadialGeometryMath.IsFinite(hysteresisDegrees) || hysteresisDegrees < 0)
        throw new ArgumentOutOfRangeException(nameof(hysteresisDegrees), "Hysteresis must be finite and nonnegative.");
      if (!RadialGeometryMath.IsFinite(direction) || direction.x == 0 && direction.y == 0) return -1;
      double angle = RadialGeometryMath.Angle(direction);
      double travel = RadialGeometryMath.Travel(angle, _start, _direction);
      if (_sweep < 360 && travel >= _sweep) return -1;

      // Bound the margin relative to pitch so a dense ring cannot lock a previous selection.
      double hysteresis = Math.Min(hysteresisDegrees, _pitch * .499999);
      if (hysteresis > 0 && previousIndex >= 0 && previousIndex < _segments.Length)
      {
        var previous = _segments[previousIndex];
        double halfWidth = snapAcrossGaps ? _pitch * .5 : Math.Abs(previous.SweepAngle) * .5;
        if (RadialGeometryMath.AngularDistance(angle, previous.CenterAngle) < halfWidth + hysteresis)
          return previousIndex;
      }

      if (snapAcrossGaps)
      {
        int index = (int)(travel / _pitch);
        return index < _segments.Length ? index : -1;
      }
      for (int i = 0; i < _segments.Length; i++)
        if (_segments[i].ContainsAngle(angle)) return i;
      return -1;
    }
  }
}
