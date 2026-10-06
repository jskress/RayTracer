using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace Tests;

[TestClass]
public class TestTube
{
    /// <summary>
    /// A tube with exactly two control points is just a single segment, so it should behave
    /// identically to a standalone <see cref="TubeSegment"/> built from the same values.
    /// </summary>
    [TestMethod]
    public void TestTwoControlPointTubeMatchesSingleSegment()
    {
        Tube tube = new ()
        {
            Start = new TubeControlPoint { Center = new Point(0, -10, 0), Radius = 2 },
            Segments =
            {
                new TubeSegmentSpec { End = new TubeControlPoint { Center = new Point(0, 10, 0), Radius = 2 } }
            }
        };
        TubeSegment segment = new ()
        {
            Start = new Point(0, -10, 0), StartRadius = 2,
            End = new Point(0, 10, 0), EndRadius = 2
        };
        Ray ray = new (new Point(5, 0, 0), Directions.Left);
        List<Intersection> tubeIntersections = [];
        List<Intersection> segmentIntersections = [];

        tube.PrepareForRendering();
        segment.PrepareForRendering();
        tube.Intersect(ray, tubeIntersections);
        segment.Intersect(ray, segmentIntersections);

        List<double> tubeDistances = tubeIntersections.Select(i => i.Distance).OrderBy(d => d).ToList();
        List<double> segmentDistances = segmentIntersections.Select(i => i.Distance).OrderBy(d => d).ToList();

        Assert.AreEqual(2, tubeDistances.Count);
        Assert.AreEqual(segmentDistances.Count, tubeDistances.Count);

        for (int index = 0; index < tubeDistances.Count; index++)
            Assert.IsTrue(segmentDistances[index].Near(tubeDistances[index], 0.0001));
    }

    /// <summary>
    /// A straight chain of equal-radius control points is just one long capsule, split into
    /// two abutting segments internally.  A ray straight down the shared axis must cross the
    /// union's boundary exactly twice -- once through each outer end cap -- rather than
    /// picking up spurious extra crossings where the two segments meet in the middle.  This
    /// is the key test that CSG union is correctly stripping away each segment's own "bulge"
    /// where it's swallowed by its neighbor, rather than just concatenating both segments'
    /// intersections.
    /// </summary>
    [TestMethod]
    public void TestStraightChainActsAsOneSeamlessCapsule()
    {
        Tube tube = new ()
        {
            Start = new TubeControlPoint { Center = new Point(0, -10, 0), Radius = 2 },
            Segments =
            {
                new TubeSegmentSpec { End = new TubeControlPoint { Center = new Point(0, 0, 0), Radius = 2 } },
                new TubeSegmentSpec { End = new TubeControlPoint { Center = new Point(0, 10, 0), Radius = 2 } }
            }
        };
        Ray ray = new (new Point(0, 20, 0), Directions.Down);
        List<Intersection> intersections = [];

        tube.PrepareForRendering();
        tube.Intersect(ray, intersections);

        List<double> distances = intersections.Select(i => i.Distance).OrderBy(d => d).ToList();

        Assert.AreEqual(2, distances.Count);
        Assert.IsTrue(8.0.Near(distances[0]));
        Assert.IsTrue(32.0.Near(distances[1]));
    }

    /// <summary>
    /// The same straight chain, hit perpendicular to the axis through the middle of its
    /// second segment, should cross the lateral surface at exactly the tube's radius -- the
    /// same result a single long segment would give, confirming the internal joint doesn't
    /// perturb hits away from it.
    /// </summary>
    [TestMethod]
    public void TestStraightChainLateralHitAwayFromJointIsUnaffected()
    {
        Tube tube = new ()
        {
            Start = new TubeControlPoint { Center = new Point(0, -10, 0), Radius = 2 },
            Segments =
            {
                new TubeSegmentSpec { End = new TubeControlPoint { Center = new Point(0, 0, 0), Radius = 2 } },
                new TubeSegmentSpec { End = new TubeControlPoint { Center = new Point(0, 10, 0), Radius = 2 } }
            }
        };
        Ray ray = new (new Point(5, 5, 0), Directions.Left);
        List<Intersection> intersections = [];

        tube.PrepareForRendering();
        tube.Intersect(ray, intersections);

        List<double> distances = intersections.Select(i => i.Distance).OrderBy(d => d).ToList();

        Assert.AreEqual(2, distances.Count);
        Assert.IsTrue(3.0.Near(distances[0]));
        Assert.IsTrue(7.0.Near(distances[1]));
    }

    /// <summary>
    /// A bent, two-segment chain (an "elbow") must still present a well-formed union
    /// boundary: a ray passing through the outside of the bend should cross it exactly
    /// twice, not four times, even though it comes close to both segments.  The bend here is
    /// a deliberate sharp corner (not a smooth curve), so this tube must be marked
    /// discontinuous to bypass the tangent-continuity check.
    /// </summary>
    [TestMethod]
    public void TestBentChainFormsWellFormedUnion()
    {
        Tube tube = new ()
        {
            Start = new TubeControlPoint { Center = new Point(-10, 0, 0), Radius = 2 },
            Segments =
            {
                new TubeSegmentSpec { End = new TubeControlPoint { Center = new Point(0, 0, 0), Radius = 2 } },
                new TubeSegmentSpec { End = new TubeControlPoint { Center = new Point(0, 10, 0), Radius = 2 } }
            },
            Discontinuous = true
        };
        Ray ray = new (new Point(-5, -5, 0), new Vector(1, 1, 0).Unit);
        List<Intersection> intersections = [];

        tube.PrepareForRendering();
        tube.Intersect(ray, intersections);

        Assert.AreEqual(2, intersections.Count);
    }

    /// <summary>
    /// At a lateral hit on the tube, the normal reported all the way through the composite
    /// (tube -> CSG union -> segment) must still point straight away from the axis, exactly
    /// as it would for a standalone segment.
    /// </summary>
    [TestMethod]
    public void TestNormalPropagatesThroughComposite()
    {
        Tube tube = new ()
        {
            Start = new TubeControlPoint { Center = new Point(0, -10, 0), Radius = 2 },
            Segments =
            {
                new TubeSegmentSpec { End = new TubeControlPoint { Center = new Point(0, 10, 0), Radius = 2 } }
            }
        };
        Ray ray = new (new Point(5, 0, 0), Directions.Left);
        List<Intersection> intersections = [];

        tube.PrepareForRendering();
        tube.Intersect(ray, intersections);

        Intersection hit = intersections.OrderBy(i => i.Distance).First();
        Point point = ray.At(hit.Distance);
        Vector normal = hit.Surface.NormalAt(point, hit);

        Assert.IsTrue(new Vector(1, 0, 0).Matches(normal.Unit));
    }

    /// <summary>
    /// A tube segment can also be a quadratic curve.  Reusing the same "arch" geometry
    /// independently validated in <c>TestTubeQuadSegment</c>, building it through the
    /// <see cref="Tube"/>/<see cref="TubeSegmentSpec"/> composition (rather than a
    /// standalone <see cref="TubeQuadSegment"/>) should give the exact same hits, confirming
    /// the tube correctly builds a curved segment when a spec carries a control point.
    /// </summary>
    [TestMethod]
    public void TestCurvedSegmentMatchesStandaloneQuadSegment()
    {
        Tube tube = new ()
        {
            Start = new TubeControlPoint { Center = new Point(0, 0, 0), Radius = 1 },
            Segments =
            {
                new TubeSegmentSpec
                {
                    Control1 = new TubeControlPoint { Center = new Point(2, 2, 0), Radius = 1 },
                    End = new TubeControlPoint { Center = new Point(4, 0, 0), Radius = 1 }
                }
            }
        };
        Ray ray = new (new Point(2, 5, 0), Directions.Down);
        List<Intersection> intersections = [];

        tube.PrepareForRendering();
        tube.Intersect(ray, intersections);

        List<double> distances = intersections.Select(i => i.Distance).OrderBy(d => d).ToList();

        Assert.AreEqual(2, distances.Count);
        Assert.IsTrue(3.0.Near(distances[0], 0.0001));
        Assert.IsTrue(5.0.Near(distances[1], 0.0001));
    }

    /// <summary>
    /// A tube segment can also be a cubic curve, when a spec carries both control points.
    /// Reusing the same "hump" geometry independently validated in
    /// <c>TestTubeCubicSegment</c>, building it through the composition should give the
    /// exact same hits.
    /// </summary>
    [TestMethod]
    public void TestCurvedSegmentMatchesStandaloneCubicSegment()
    {
        Tube tube = new ()
        {
            Start = new TubeControlPoint { Center = new Point(0, 0, 0), Radius = 1 },
            Segments =
            {
                new TubeSegmentSpec
                {
                    Control1 = new TubeControlPoint { Center = new Point(2, 3, 0), Radius = 1 },
                    Control2 = new TubeControlPoint { Center = new Point(4, 3, 0), Radius = 1 },
                    End = new TubeControlPoint { Center = new Point(6, 0, 0), Radius = 1 }
                }
            }
        };
        Ray ray = new (new Point(3, 10, 0), Directions.Down);
        List<Intersection> intersections = [];

        tube.PrepareForRendering();
        tube.Intersect(ray, intersections);

        List<double> distances = intersections.Select(i => i.Distance).OrderBy(d => d).ToList();

        Assert.AreEqual(2, distances.Count);
        Assert.IsTrue(6.75.Near(distances[0], 0.0001));
        Assert.IsTrue(8.75.Near(distances[1], 0.0001));
    }

    /// <summary>
    /// Every intersection a tube produces actually belongs to one of its child segments
    /// (see <see cref="TestNormalPropagatesThroughComposite"/>), so a material set on the
    /// tube itself is useless unless it's propagated down to those segments.  This also
    /// exercises <see cref="SurfaceIterator"/>'s ability to walk into a tube's composite
    /// tree, which the real render pipeline relies on for the same propagation.
    /// </summary>
    [TestMethod]
    public void TestMaterialPropagatesToSegments()
    {
        Material material = new () { Pigment = new SolidPigment(Colors.Red) };
        Tube tube = new ()
        {
            Start = new TubeControlPoint { Center = new Point(0, -10, 0), Radius = 2 },
            Segments =
            {
                new TubeSegmentSpec { End = new TubeControlPoint { Center = new Point(0, 0, 0), Radius = 2 } },
                new TubeSegmentSpec { End = new TubeControlPoint { Center = new Point(0, 10, 0), Radius = 2 } }
            },
            Material = material
        };

        tube.PrepareForRendering();

        List<Surface> segments = new SurfaceIterator(tube).Surfaces
            .Where(surface => surface is TubeSegment)
            .ToList();

        Assert.AreEqual(2, segments.Count);
        Assert.IsTrue(segments.All(segment => segment.Material == material));
    }

    /// <summary>
    /// A ray aimed entirely away from the tube must report no intersections.
    /// </summary>
    [TestMethod]
    public void TestMissesEverything()
    {
        Tube tube = new ()
        {
            Start = new TubeControlPoint { Center = new Point(0, -2, 0), Radius = 1 },
            Segments =
            {
                new TubeSegmentSpec { End = new TubeControlPoint { Center = new Point(0, 2, 0), Radius = 1 } }
            }
        };
        Ray ray = new (new Point(50, 50, 50), Directions.Up);
        List<Intersection> intersections = [];

        tube.PrepareForRendering();
        tube.Intersect(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    /// <summary>
    /// A tube whose two segments meet at a sharp corner (not flowing smoothly into each
    /// other) must fail to prepare, by default, with a clear error -- since a rotation-
    /// tracking loft can't absorb a real kink without a sudden, surprising twist right at
    /// that seam.
    /// </summary>
    [TestMethod]
    public void TestDiscontinuousTubeThrowsByDefault()
    {
        Tube tube = new ()
        {
            Start = new TubeControlPoint { Center = new Point(-10, 0, 0), Radius = 2 },
            Segments =
            {
                new TubeSegmentSpec { End = new TubeControlPoint { Center = new Point(0, 0, 0), Radius = 2 } },
                new TubeSegmentSpec { End = new TubeControlPoint { Center = new Point(0, 10, 0), Radius = 2 } }
            }
        };

        Assert.ThrowsExactly<Exception>(tube.PrepareForRendering);
    }

    /// <summary>
    /// Marking a kinked tube <see cref="Tube.Discontinuous"/> must suppress the
    /// tangent-continuity check, since the kink is intentional there.
    /// </summary>
    [TestMethod]
    public void TestDiscontinuousFlagSuppressesTheCheck()
    {
        Tube tube = new ()
        {
            Start = new TubeControlPoint { Center = new Point(-10, 0, 0), Radius = 2 },
            Segments =
            {
                new TubeSegmentSpec { End = new TubeControlPoint { Center = new Point(0, 0, 0), Radius = 2 } },
                new TubeSegmentSpec { End = new TubeControlPoint { Center = new Point(0, 10, 0), Radius = 2 } }
            },
            Discontinuous = true
        };

        tube.PrepareForRendering();
    }

    /// <summary>
    /// Where a curved segment's side wall meets one of its end spheres, the two are tangent round a
    /// circle, and a ray crossing near it crosses the boundary once.  Both used to claim that one
    /// crossing in a thin band just past the circle -- a quarter of the rays aimed within a
    /// thousandth of it -- which left the line with an odd number of crossings, so anything counting
    /// them to tell inside from out (the tube's own union, and any CSG it stands in) got the rest
    /// of the line backwards.  Rays are aimed through each seam of a cubic segment, the one on the
    /// Enterprise's hull where it showed, a whisker either side of the circle and from every
    /// direction; a solid seen whole must be crossed an even number of times along every line.
    /// </summary>
    [TestMethod]
    public void TestACubicSegmentCountsACrossingAtItsSeamOnce()
    {
        TubeCubicSegment segment = new ()
        {
            Start = new Point(12.3156, -3.9287, 0), StartRadius = 1.0607,
            Control1 = new Point(12.7437, -3.9452, 0), Control1Radius = 1.0451,
            Control2 = new Point(13.1719, -4.0107, 0), Control2Radius = 1.0786,
            End = new Point(13.6000, -4.0272, 0), EndRadius = 1.0631
        };

        segment.PrepareForRendering();

        Assert.AreEqual(0, OddLinesThroughSeam(
            segment, segment.Start, segment.StartRadius,
            (segment.Control1 - segment.Start) * 3, (segment.Control1Radius - segment.StartRadius) * 3));
        Assert.AreEqual(0, OddLinesThroughSeam(
            segment, segment.End, segment.EndRadius,
            (segment.End - segment.Control2) * 3, (segment.EndRadius - segment.Control2Radius) * 3));
    }

    /// <summary>
    /// The quadratic segment decides its seams the same way the cubic does, and doubled a crossing
    /// there as often, so it is held to the same count.
    /// </summary>
    [TestMethod]
    public void TestAQuadSegmentCountsACrossingAtItsSeamOnce()
    {
        TubeQuadSegment segment = new ()
        {
            Start = new Point(0, 0, 0), StartRadius = 1,
            Control = new Point(1.5, 0.6, 0), ControlRadius = 0.8,
            End = new Point(3, 0, 0), EndRadius = 0.9
        };

        segment.PrepareForRendering();

        Assert.AreEqual(0, OddLinesThroughSeam(
            segment, segment.Start, segment.StartRadius,
            (segment.Control - segment.Start) * 2, (segment.ControlRadius - segment.StartRadius) * 2));
        Assert.AreEqual(0, OddLinesThroughSeam(
            segment, segment.End, segment.EndRadius,
            (segment.End - segment.Control) * 2, (segment.EndRadius - segment.ControlRadius) * 2));
    }

    /// <summary>
    /// The case that showed: a speck of shade on the Enterprise's lit hull.  The hull is its tube
    /// cut to the outlines of its drawings, and the shadow ray from a point on it near a seam,
    /// toward the light, found the plan's outline beyond the point standing in its way -- the tube
    /// had counted the seam twice, so the ray took itself to be still inside the hull.  Nothing
    /// may stand between that point and the light.
    /// </summary>
    [TestMethod]
    public void TestAShadowRayOffTheHullBesideASeamIsNotStopped()
    {
        Tube tube = new ()
        {
            Discontinuous = true,
            Start = Control(5.6564, -4.0113, 1.5271),
            Segments =
            {
                new TubeSegmentSpec { Control1 = Control(6.2020, -4.0718, 1.5647), Control2 = Control(6.7476, -4.0381, 1.5706), End = Control(7.2932, -4.0417, 1.5426) },
                new TubeSegmentSpec { Control1 = Control(7.9250, -4.0459, 1.5102), Control2 = Control(8.5567, -4.0140, 1.4210), End = Control(9.1885, -3.9834, 1.3465) },
                new TubeSegmentSpec { Control1 = Control(9.8202, -3.9529, 1.2720), Control2 = Control(10.4520, -3.9224, 1.1901), End = Control(11.0837, -3.9303, 1.1543) },
                new TubeSegmentSpec { Control1 = Control(11.4943, -3.9461, 1.1394), Control2 = Control(11.9050, -3.9129, 1.0755), End = Control(12.3156, -3.9287, 1.0607) },
                new TubeSegmentSpec { Control1 = Control(12.7437, -3.9452, 1.0451), Control2 = Control(13.1719, -4.0107, 1.0786), End = Control(13.6000, -4.0272, 1.0631) },
                new TubeSegmentSpec { End = Control(14.6158, -4.0663, 1.0263) }
            }
        };
        Extrusion plan = new ()
        {
            Path = new GeneralPath()
                .MoveTo(5.0534, -1.8952)
                .LineTo(14.2884, -1.8952)
                .LineTo(14.2884, -1.0407)
                .CubicTo(14.3225, -0.8832, 14.3368, -0.6902, 14.5055, -0.5720)
                .LineTo(14.5055, 0.5720)
                .CubicTo(14.3368, 0.6902, 14.3225, 0.8832, 14.2884, 1.0407)
                .LineTo(14.2884, 1.8952)
                .LineTo(5.0534, 1.8952)
                .ClosePath(),
            MinimumY = -7,
            MaximumY = -1
        };
        CsgSurface hull = new () { Operation = CsgOperation.Intersection, Left = tube, Right = plan };

        hull.PrepareForRendering();

        Point from = new (12.687572394057451, -3.8831892247826443, 1.054827247586551);
        Vector toLight = new Point(20, 10, 40) - from;
        Ray ray = new (from, toLight.Unit);
        List<Intersection> tubeHits = [];
        List<Intersection> hullHits = [];

        tube.Intersect(ray, tubeHits);
        hull.Intersect(ray, hullHits);

        Assert.AreEqual(0, tubeHits.Count % 2, "the tube must be crossed an even number of times");
        Assert.IsFalse(
            hullHits.Any(hit => hit.Distance > 0 && hit.Distance < toLight.Magnitude),
            "nothing of the hull stands between the point and the light");
    }

    private static TubeControlPoint Control(double x, double y, double radius)
    {
        return new TubeControlPoint { Center = new Point(x, y, 0), Radius = radius };
    }

    /// <summary>
    /// This method aims rays through the circle where a curved segment's side wall meets one of
    /// its end spheres and counts the lines crossed an odd number of times.  The circle lies on
    /// the end sphere, across the curve, set back from the sphere's center by r r' / |c'| (where
    /// the end sphere is tangent to the family), and each ray is aimed at a point of it nudged
    /// along the curve by anything from a thousandth to a billionth, either way, from a random
    /// direction, starting well outside.
    /// </summary>
    private static int OddLinesThroughSeam(
        Surface segment, Point center, double radius, Vector slope, double radiusSlope)
    {
        Vector axis = slope.Unit;
        double back = -radius * radiusSlope / slope.Magnitude;
        double ring = Math.Sqrt(radius * radius - back * back);
        Vector across = axis.Cross(Math.Abs(axis.Y) < 0.9 ? Directions.Up : Directions.Right).Unit;
        Vector around = axis.Cross(across).Unit;
        Random random = new (12345);
        int odd = 0;

        for (int index = 0; index < 4000; index++)
        {
            double angle = random.NextDouble() * 2 * Math.PI;
            double nudge = (random.NextDouble() * 2 - 1) * Math.Pow(10, -3 - 6 * random.NextDouble());
            Point aim = center + axis * (back + nudge) + (across * Math.Cos(angle) + around * Math.Sin(angle)) * ring;
            Vector direction = new Vector(
                random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1).Unit;
            List<Intersection> hits = [];

            segment.Intersect(new Ray(aim - direction * 20, direction), hits);

            if (hits.Count % 2 == 1)
                odd++;
        }

        return odd;
    }
}
