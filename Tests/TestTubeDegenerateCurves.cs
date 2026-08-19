using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

/// <summary>
/// These tests cover a curved tube segment whose control points leave it geometrically straight.
/// <para>
/// This is worth its own file because of how it failed.  The envelope solve for a curved segment
/// works from the differences of its control points; when those collapse, the polynomial it samples
/// drops degree and the solve finds no hits, so the segment rendered as its two end spheres and
/// nothing between them -- with no error, no warning, and no clue.  A scene computing a bend that
/// happened to work out to nought simply lost that piece of geometry.  The trees library did exactly
/// that: a limb whose random landed on a half vanished.
/// </para>
/// </summary>
[TestClass]
public class TestTubeDegenerateCurves
{
    private static Tube Quad(Point start, Point control, Point end)
    {
        Tube tube = new ()
        {
            Start = new TubeControlPoint { Center = start, Radius = 0.1 }
        };

        tube.Segments.Add(new TubeSegmentSpec
        {
            Control1 = new TubeControlPoint { Center = control, Radius = 0.08 },
            End = new TubeControlPoint { Center = end, Radius = 0.05 }
        });

        tube.PrepareForRendering();

        return tube;
    }

    /// <summary>
    /// Counts how many times a ray fired across the middle of a segment actually hits it.  A tube
    /// that has lost its body still has its end spheres, so asking "did anything render" is not
    /// enough -- the ray has to cross the middle, where only the body can be.
    /// </summary>
    private static int HitsAcrossTheMiddle(Tube tube, Point start, Point end)
    {
        Point middle = new (
            (start.X + end.X) / 2, (start.Y + end.Y) / 2, (start.Z + end.Z) / 2);
        Ray ray = new (new Point(middle.X, middle.Y, middle.Z - 5), Directions.In);
        List<Intersection> intersections = [];

        tube.AddIntersections(ray, intersections);

        return intersections.Count;
    }

    [TestMethod]
    public void TestAQuadWhoseControlIsTheMidpointStillHasABody()
    {
        // The exact case that broke: a control point halfway between the ends makes the second
        // difference nought, and the curve is a line.  It must draw as one rather than disappear.
        Point start = new (0, 0, 0);
        Point end = new (0, 2, 0);
        Tube tube = Quad(start, new Point(0, 1, 0), end);

        Assert.IsTrue(HitsAcrossTheMiddle(tube, start, end) > 0,
            "a quad whose control point is the midpoint rendered no body at all");
    }

    [TestMethod]
    public void TestAQuadThatReallyBendsIsStillCurved()
    {
        // The fix must not flatten a genuine bend.  This control point is well off the line, so the
        // segment has to stay a curve -- and a ray fired where the curve is, but the straight line
        // is not, has to hit it.
        Tube tube = Quad(new Point(0, 0, 0), new Point(1.2, 1, 0), new Point(0, 2, 0));
        Ray ray = new (new Point(0.55, 1, -5), Directions.In);
        List<Intersection> intersections = [];

        tube.AddIntersections(ray, intersections);

        Assert.IsTrue(intersections.Count > 0,
            "a genuinely bent quad was flattened to a straight line");
    }

    [TestMethod]
    public void TestACollinearControlThatIsNotTheMidpointStillHasABody()
    {
        // These always worked, and must go on working: the control sits on the line but not halfway,
        // so the second difference is not nought and the curve solve copes.
        foreach (double fraction in new[] { 0.25, 0.75 })
        {
            Point start = new (0, 0, 0);
            Point end = new (0, 2, 0);
            Tube tube = Quad(start, new Point(0, 2 * fraction, 0), end);

            Assert.IsTrue(HitsAcrossTheMiddle(tube, start, end) > 0,
                $"a quad with its control at {fraction} of the way rendered no body");
        }
    }

    [TestMethod]
    public void TestASegmentOfNoLengthIsNamedForWhatItIs()
    {
        // A segment whose points all coincide has no direction to travel in, so its tangent is the
        // zero vector.  The continuity check used to take the angle between that and its neighbour,
        // which is an arc-cosine of a NaN -- so it refused the tube while reporting that its
        // segments "bend by about NaN degrees", naming neither the place nor the problem.
        Tube tube = new ()
        {
            Start = new TubeControlPoint { Center = new Point(0, 0, 0), Radius = 0.1 }
        };

        tube.Segments.Add(new TubeSegmentSpec
        {
            End = new TubeControlPoint { Center = new Point(0, 1, 0), Radius = 0.1 }
        });
        tube.Segments.Add(new TubeSegmentSpec
        {
            Control1 = new TubeControlPoint { Center = new Point(0, 1, 0), Radius = 0.1 },
            End = new TubeControlPoint { Center = new Point(0, 1, 0), Radius = 0.1 }
        });

        Exception exception = Assert.ThrowsExactly<Exception>(() => tube.PrepareForRendering());

        StringAssert.Contains(exception.Message, "segment of no length");
        Assert.IsFalse(exception.Message.Contains("NaN"), "the message should not report a NaN");
    }

    [TestMethod]
    public void TestDiscontinuousDoesNotExcuseASegmentOfNoLength()
    {
        // "discontinuous" says a chain's kinks are meant.  A segment with no length is not a kink,
        // so it must still be refused -- it used to sail straight through, and the tube then
        // rendered with a NaN-tangent segment in the middle of it.
        Tube tube = new ()
        {
            Discontinuous = true,
            Start = new TubeControlPoint { Center = new Point(0, 0, 0), Radius = 0.1 }
        };

        tube.Segments.Add(new TubeSegmentSpec
        {
            End = new TubeControlPoint { Center = new Point(0, 1, 0), Radius = 0.1 }
        });
        tube.Segments.Add(new TubeSegmentSpec
        {
            Control1 = new TubeControlPoint { Center = new Point(0, 1, 0), Radius = 0.1 },
            End = new TubeControlPoint { Center = new Point(0, 1, 0), Radius = 0.1 }
        });

        Exception exception = Assert.ThrowsExactly<Exception>(() => tube.PrepareForRendering());

        StringAssert.Contains(exception.Message, "segment of no length");
    }

    [TestMethod]
    public void TestDiscontinuousStillExcusesAnActualKink()
    {
        // The other side of that: a real kink, which is what the word is for, must go on being
        // allowed.  Otherwise the fix above would have made "discontinuous" useless.
        Tube tube = new ()
        {
            Discontinuous = true,
            Start = new TubeControlPoint { Center = new Point(0, 0, 0), Radius = 0.1 }
        };

        tube.Segments.Add(new TubeSegmentSpec
        {
            End = new TubeControlPoint { Center = new Point(0, 1, 0), Radius = 0.1 }
        });
        tube.Segments.Add(new TubeSegmentSpec
        {
            End = new TubeControlPoint { Center = new Point(1, 1, 0), Radius = 0.1 }
        });

        tube.PrepareForRendering();
    }
}
