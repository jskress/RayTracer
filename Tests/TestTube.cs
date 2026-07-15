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
    /// twice, not four times, even though it comes close to both segments.
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
            }
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
        Vector normal = hit.Surface.NormaAt(point, hit);

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
}
