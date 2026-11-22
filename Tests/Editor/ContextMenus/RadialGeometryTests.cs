using System;
using MToolKit.ContextMenus.Geometry;
using NUnit.Framework;
using UnityEngine;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class RadialGeometryTests
  {
    private static Vector2 At(float angle, float radius = 150)
    {
      double radians = angle * Math.PI / 180;
      return new Vector2((float)(Math.Cos(radians) * radius), (float)(Math.Sin(radians) * radius));
    }

    private static RadialRingGeometry Ring(int count = 4, float start = 0, float sweep = 360,
      float gap = 0, float inner = 100, float outer = 200, int index = 0)
      => new RadialRingGeometry(index, count, inner, outer, start, sweep, gap);

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(7)]
    [TestCase(32)]
    public void FullRing_EachContentAnchorHitsItsOwnSegment(int count)
    {
      var ring = Ring(count: count, index: 3);
      Assert.That(ring.Segments.Count, Is.EqualTo(count));
      for (int i = 0; i < count; i++)
      {
        var segment = ring.Segments[i];
        Assert.That(segment.RingIndex, Is.EqualTo(3));
        Assert.That(segment.SegmentIndex, Is.EqualTo(i));
        Assert.That(segment.InnerRadius, Is.EqualTo(100));
        Assert.That(segment.OuterRadius, Is.EqualTo(200));
        Assert.That(segment.ContentAnchor.magnitude, Is.EqualTo(150).Within(.001f));
        Assert.That(ring.HitTest(segment.ContentAnchor), Is.EqualTo(i));
        Assert.That(segment.Contains(segment.ContentAnchor), Is.True);
      }
    }

    [TestCase(0, 0)]
    [TestCase(90, 1)]
    [TestCase(180, 2)]
    [TestCase(270, 3)]
    public void SharedCardinalBoundary_BelongsToFollowingSegment(float angle, int expected)
    {
      var ring = Ring();
      // Exact cardinal vectors avoid trigonometric roundoff at a contractual boundary.
      Vector2 point = angle == 0 ? Vector2.right : angle == 90 ? Vector2.up :
        angle == 180 ? Vector2.left : Vector2.down;
      Assert.That(ring.HitTest(point * 150), Is.EqualTo(expected));
    }

    [TestCase(-.01f, 3)]
    [TestCase(.01f, 0)]
    [TestCase(359.99f, 3)]
    [TestCase(360.01f, 0)]
    public void FullRing_ZeroDegreeSeamHasNoOverlapOrLostSector(float angle, int expected)
      => Assert.That(Ring().HitTest(At(angle)), Is.EqualTo(expected));

    [TestCase(-45, -360)]
    [TestCase(45, 360)]
    [TestCase(765, 360)]
    [TestCase(-765, -360)]
    public void StartAngleAndDirection_RotateOrderingWithoutChangingActionIndices(float start, float sweep)
    {
      var ring = Ring(start: start, sweep: sweep);
      for (int i = 0; i < 4; i++)
      {
        float angle = start + sweep / 4 * (i + .5f);
        Assert.That(ring.HitTest(At(angle)), Is.EqualTo(i));
        Assert.That(ring.Segments[i].CenterAngle, Is.InRange(0, 359.99999f));
        Assert.That(ring.Segments[i].StartAngle, Is.InRange(0, 359.99999f));
      }
    }

    [Test]
    public void AngularGap_IsTrimmedFromBothVisibleEdgesAndUsedByHitTesting()
    {
      var ring = Ring(gap: 10);
      var first = ring.Segments[0];
      Assert.That(first.StartAngle, Is.EqualTo(5).Within(.0001));
      Assert.That(first.SweepAngle, Is.EqualTo(80).Within(.0001));
      Assert.That(first.CenterAngle, Is.EqualTo(45).Within(.0001));
      Assert.That(first.ContentAnchor.x, Is.EqualTo(At(45).x).Within(.001f));
      Assert.That(first.ContentAnchor.y, Is.EqualTo(At(45).y).Within(.001f));
      Assert.That(ring.HitTest(At(0)), Is.EqualTo(-1));
      Assert.That(ring.HitTest(At(4.9f)), Is.EqualTo(-1));
      Assert.That(ring.HitTest(At(5.1f)), Is.EqualTo(0));
      Assert.That(ring.HitTest(At(84.9f)), Is.EqualTo(0));
      Assert.That(ring.HitTest(At(85.1f)), Is.EqualTo(-1));
      Assert.That(ring.HitTest(At(90)), Is.EqualTo(-1));
    }

    [Test]
    public void ClockwiseGap_UsesTheSameVisibleRegionAsCounterclockwiseGeometry()
    {
      var ring = Ring(start: 0, sweep: -360, gap: 10);
      Assert.That(ring.Segments[0].StartAngle, Is.EqualTo(355).Within(.0001));
      Assert.That(ring.Segments[0].SweepAngle, Is.EqualTo(-80).Within(.0001));
      Assert.That(ring.Segments[0].CenterAngle, Is.EqualTo(315).Within(.0001));
      Assert.That(ring.HitTest(At(-4.9f)), Is.EqualTo(-1));
      Assert.That(ring.HitTest(At(-5.1f)), Is.EqualTo(0));
      Assert.That(ring.HitTest(At(-84.9f)), Is.EqualTo(0));
      Assert.That(ring.HitTest(At(-85.1f)), Is.EqualTo(-1));
    }

    [TestCase(90)]
    [TestCase(180)]
    [TestCase(270)]
    [TestCase(-90)]
    [TestCase(-180)]
    [TestCase(-270)]
    public void PartialArc_RejectsComplementEvenForSectorsWiderThanHalfCircle(float sweep)
    {
      var ring = Ring(count: 1, start: 350, sweep: sweep);
      float sign = Math.Sign(sweep);
      Assert.That(ring.HitTest(At(350 + sweep * .5f)), Is.EqualTo(0));
      Assert.That(ring.HitTest(At(350 + sign * .01f)), Is.EqualTo(0));
      Assert.That(ring.HitTest(At(350 + sweep - sign * .01f)), Is.EqualTo(0));
      Assert.That(ring.HitTest(At(350 - sign * .01f)), Is.EqualTo(-1));
      Assert.That(ring.HitTest(At(350 + sweep + sign * .01f)), Is.EqualTo(-1));
    }

    [Test]
    public void PartialChildArc_UsesItsOwnCountAndRadiiWhileRetainingParentAssociation()
    {
      var parent = Ring(count: 4);
      var child = Ring(count: 3, start: 15, sweep: 60, gap: 2, inner: 220, outer: 270, index: 1);
      Assert.That(parent.HitTest(At(45, 150)), Is.EqualTo(0));
      Assert.That(child.HitTest(At(45, 150)), Is.EqualTo(-1));
      Assert.That(parent.HitTest(At(45, 210)), Is.EqualTo(-1));
      Assert.That(child.HitTest(At(45, 210)), Is.EqualTo(-1), "Inter-ring spacing is not interactive.");
      Assert.That(parent.HitTest(At(45, 245)), Is.EqualTo(-1));
      Assert.That(child.HitTest(At(25, 245)), Is.EqualTo(0));
      Assert.That(child.HitTest(At(45, 245)), Is.EqualTo(1));
      Assert.That(child.HitTest(At(65, 245)), Is.EqualTo(2));
      Assert.That(child.HitTest(At(90, 245)), Is.EqualTo(-1));
      Assert.That(child.Segments[1].RingIndex, Is.EqualTo(1));
    }

    [TestCase(0, -1)]
    [TestCase(99.99f, -1)]
    [TestCase(100, 0)]
    [TestCase(199.99f, 0)]
    [TestCase(200, -1)]
    [TestCase(250, -1)]
    public void Radius_InnerBoundaryIsInclusiveOuterBoundaryExclusive(float radius, int expected)
      => Assert.That(Ring().HitTest(Vector2.right * radius), Is.EqualTo(expected));

    [Test]
    public void ZeroInnerRadius_SupportsDiskSegmentsWithoutAnArtificialHole()
      => Assert.That(Ring(inner: 0).HitTest(At(45, .01f)), Is.EqualTo(0));

    [Test]
    public void SingleFullCircleSector_AcceptsEveryDirectionWithoutAnAngularSeam()
    {
      var ring = Ring(count: 1);
      for (int degrees = 0; degrees < 360; degrees++)
        Assert.That(ring.HitTest(At(degrees)), Is.EqualTo(0), "Direction " + degrees);
    }

    [Test]
    public void SegmentContainsAndRingHitTest_AgreeThroughoutAllRenderedWedges()
    {
      var ring = Ring(count: 7, start: 325, sweep: -270, gap: 3);
      for (int angle = 0; angle < 360; angle++)
      {
        var point = At(angle);
        int hit = ring.HitTest(point);
        int matches = 0;
        for (int segment = 0; segment < ring.Segments.Count; segment++)
        {
          if (!ring.Segments[segment].Contains(point)) continue;
          matches++;
          Assert.That(hit, Is.EqualTo(segment));
        }
        Assert.That(matches, Is.EqualTo(hit < 0 ? 0 : 1), "Every visible point has one owner.");
      }
    }

    [Test]
    public void DirectionSelection_DoesNotDependOnMouseRingRadiusOrStickMagnitude()
    {
      var ring = Ring(inner: 220, outer: 270);
      Assert.That(ring.SelectDirection(At(45, .5f), -1), Is.EqualTo(0));
      Assert.That(ring.SelectDirection(At(45, 1000), -1), Is.EqualTo(0));
      Assert.That(ring.SelectDirection(Vector2.zero, -1), Is.EqualTo(-1));
    }

    [Test]
    public void AnalogGapSnapping_IsExplicitAndDoesNotChangePointerHitTesting()
    {
      var ring = Ring(gap: 10);
      Assert.That(ring.HitTest(At(2)), Is.EqualTo(-1));
      Assert.That(ring.SelectDirection(At(2, 1), -1, 0, false), Is.EqualTo(-1));
      Assert.That(ring.SelectDirection(At(2, 1), -1, 0, true), Is.EqualTo(0));
      var partial = Ring(count: 2, start: 15, sweep: 60, gap: 2);
      Assert.That(partial.SelectDirection(At(200, 1), -1, 0, true), Is.EqualTo(-1),
        "Snapping fills internal visual gaps, never the complement of a partial arc.");
    }

    [Test]
    public void Hysteresis_RetainsSelectionNearBoundaryThenAllowsDeliberateCrossing()
    {
      var ring = Ring();
      Assert.That(ring.SelectDirection(At(91, 1), 0, 3), Is.EqualTo(0));
      Assert.That(ring.SelectDirection(At(94, 1), 0, 3), Is.EqualTo(1));
      Assert.That(ring.SelectDirection(At(89, 1), 1, 3), Is.EqualTo(1));
      Assert.That(ring.SelectDirection(At(86, 1), 1, 3), Is.EqualTo(0));
      Assert.That(ring.SelectDirection(At(1, 1), 3, 3), Is.EqualTo(3));
      Assert.That(ring.SelectDirection(At(4, 1), 3, 3), Is.EqualTo(0));
    }

    [Test]
    public void StalePreviousSelection_IsDiscardedAfterStructureChanges()
    {
      var ring = Ring(count: 2);
      Assert.That(ring.SelectDirection(At(45, 1), 17, 3), Is.EqualTo(0));
      Assert.That(ring.SelectDirection(At(225, 1), -1, 3), Is.EqualTo(1));
    }

    [Test]
    public void AnalogSelector_ClearsDeadZoneAndNeverRetainsAReleaseConfirmTarget()
    {
      var selector = new RadialAnalogSelector(.25f, 3);
      var ring = Ring();
      Assert.That(selector.SelectedIndex, Is.EqualTo(-1));
      Assert.That(selector.Select(At(45, .249f), ring), Is.EqualTo(-1));
      Assert.That(selector.Select(Vector2.right * .25f, ring), Is.EqualTo(-1));
      Assert.That(selector.Select(At(45, .251f), ring), Is.EqualTo(0));
      Assert.That(selector.Select(At(91, .8f), ring), Is.EqualTo(0));
      Assert.That(selector.Select(At(94, .8f), ring), Is.EqualTo(1));
      Assert.That(selector.Select(Vector2.zero, ring), Is.EqualTo(-1));
      Assert.That(selector.SelectedIndex, Is.EqualTo(-1));
      Assert.That(selector.Select(At(91, .8f), ring), Is.EqualTo(1), "Leaving the center discards old hysteresis.");
      selector.Reset();
      Assert.That(selector.SelectedIndex, Is.EqualTo(-1));
    }

    [Test]
    public void AnalogSelector_ResetsRingOwnershipRatherThanRetainingAnotherRingsIndex()
    {
      var selector = new RadialAnalogSelector(.25f, 3);
      var parent = Ring();
      var child = Ring(count: 2, start: 0, sweep: 90, inner: 220, outer: 270, index: 1);
      Assert.That(selector.Select(At(44, 1), parent), Is.EqualTo(0));
      Assert.That(selector.Select(At(46, 1), child), Is.EqualTo(1),
        "The new ring must not apply parent segment zero's hysteresis.");
    }

    [TestCase(float.NaN, 0)]
    [TestCase(0, float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity, 1)]
    public void InvalidInputPoint_CannotSelectAnAction(float x, float y)
    {
      var point = new Vector2(x, y);
      var ring = Ring();
      Assert.That(ring.HitTest(point), Is.EqualTo(-1));
      Assert.That(ring.Segments[0].Contains(point), Is.False);
      Assert.That(ring.SelectDirection(point, 0, 3), Is.EqualTo(-1));
      var selector = new RadialAnalogSelector(.25f, 3);
      selector.Select(At(45, 1), ring);
      Assert.That(selector.Select(point, ring), Is.EqualTo(-1));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void InvalidSegmentCount_FailsWithUsefulConfigurationError(int count)
      => Assert.Throws<ArgumentOutOfRangeException>(() => Ring(count: count));

    [TestCase(-1, 200)]
    [TestCase(100, 100)]
    [TestCase(200, 100)]
    [TestCase(float.NaN, 200)]
    [TestCase(100, float.PositiveInfinity)]
    public void InvalidRadii_FailBeforeRendering(float inner, float outer)
      => Assert.Throws<ArgumentOutOfRangeException>(() => Ring(inner: inner, outer: outer));

    [TestCase(0)]
    [TestCase(360.1f)]
    [TestCase(-360.1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidSweep_FailsBeforeRendering(float sweep)
      => Assert.Throws<ArgumentOutOfRangeException>(() => Ring(sweep: sweep));

    [TestCase(-1)]
    [TestCase(90)]
    [TestCase(91)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidAngularGap_CannotEraseOrInvertSegments(float gap)
      => Assert.Throws<ArgumentOutOfRangeException>(() => Ring(gap: gap));

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidStartAngle_FailsBeforeRendering(float angle)
      => Assert.Throws<ArgumentOutOfRangeException>(() => Ring(start: angle));

    [TestCase(-.1f)]
    [TestCase(1)]
    [TestCase(float.NaN)]
    public void InvalidAnalogDeadZone_FailsWithConfigurationError(float deadZone)
      => Assert.Throws<ArgumentOutOfRangeException>(() => new RadialAnalogSelector(deadZone, 3));

    [Test]
    public void GeometryStorage_IsReusedForSelectionOnlyChanges()
    {
      var ring = Ring();
      var originalSegments = ring.Segments;
      var originalAnchor = originalSegments[0].ContentAnchor;
      for (int i = 0; i < 100; i++)
      {
        ring.HitTest(At(i));
        ring.SelectDirection(At(i, 1), 0, 3);
      }
      Assert.That(ring.Segments, Is.SameAs(originalSegments));
      Assert.That(ring.Segments[0].ContentAnchor, Is.EqualTo(originalAnchor));
    }
  }
}
