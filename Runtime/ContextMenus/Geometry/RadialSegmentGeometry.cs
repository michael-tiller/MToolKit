using System;
using UnityEngine;

namespace MToolKit.ContextMenus.Geometry
{
  /// <summary>
  /// One visible annular wedge in the presenter's logical local coordinate system.
  /// Angles start on +X; positive sweeps run counterclockwise. Gaps have already been removed.
  /// </summary>
  public readonly struct RadialSegmentGeometry
  {
    private readonly double _innerSquared;
    private readonly double _outerSquared;

    public int RingIndex { get; }
    public int SegmentIndex { get; }
    public float InnerRadius { get; }
    public float OuterRadius { get; }
    public float StartAngle { get; }
    public float SweepAngle { get; }
    public float CenterAngle { get; }
    public Vector2 ContentAnchor { get; }

    internal RadialSegmentGeometry(int ringIndex, int segmentIndex, float innerRadius,
      float outerRadius, double startAngle, double sweepAngle)
    {
      RingIndex = ringIndex;
      SegmentIndex = segmentIndex;
      InnerRadius = innerRadius;
      OuterRadius = outerRadius;
      StartAngle = RadialGeometryMath.Normalize(startAngle);
      SweepAngle = (float)sweepAngle;
      CenterAngle = RadialGeometryMath.Normalize(startAngle + sweepAngle * .5);
      _innerSquared = (double)innerRadius * innerRadius;
      _outerSquared = (double)outerRadius * outerRadius;
      double centerRadians = CenterAngle * Math.PI / 180;
      double contentRadius = ((double)innerRadius + outerRadius) * .5;
      ContentAnchor = new Vector2((float)(Math.Cos(centerRadians) * contentRadius),
        (float)(Math.Sin(centerRadians) * contentRadius));
    }

    /// <summary>Inner/start boundaries are inclusive; outer/end boundaries are exclusive.</summary>
    public bool Contains(Vector2 localPoint)
    {
      if (!RadialGeometryMath.IsFinite(localPoint)) return false;
      double radiusSquared = (double)localPoint.x * localPoint.x + (double)localPoint.y * localPoint.y;
      if (radiusSquared < _innerSquared || radiusSquared >= _outerSquared) return false;
      return ContainsAngle(RadialGeometryMath.Angle(localPoint));
    }

    internal bool ContainsAngle(double angle)
    {
      double sweep = Math.Abs(SweepAngle);
      if (sweep == 0) return false;
      if (sweep >= 360) return true;
      double travel = RadialGeometryMath.Travel(angle, StartAngle, Math.Sign(SweepAngle));
      return travel < sweep;
    }
  }

  internal static class RadialGeometryMath
  {
    internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    internal static bool IsFinite(Vector2 point) => IsFinite(point.x) && IsFinite(point.y);
    internal static double Angle(Vector2 point) => Math.Atan2(point.y, point.x) * 180 / Math.PI;

    internal static float Normalize(double angle)
    {
      double normalized = angle % 360;
      if (normalized < 0) normalized += 360;
      float result = (float)normalized;
      return result >= 360 ? 0 : result;
    }

    internal static double Travel(double angle, double start, int direction)
    {
      double result = ((angle - start) * direction) % 360;
      return result < 0 ? result + 360 : result;
    }

    internal static double AngularDistance(double first, double second)
    {
      double distance = Travel(first, second, 1);
      return distance > 180 ? 360 - distance : distance;
    }
  }
}
