using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace Tests;

/// <summary>
/// These tests cover the ribbon: a flat strand of a given width running through a series of points.
/// <para>
/// A ribbon standing up the Y axis is laid out predictably, and the tests lean on it: a vertical
/// tangent makes the frame reach for +X to start from, so the strip spans X and faces -Z.  That is
/// worth stating because it is the one thing about a ribbon an author does not choose directly --
/// which way it faces is worked out from the path, and turned from there with `twist`.
/// </para>
/// </summary>
[TestClass]
public class TestRibbon
{
    private static Ribbon Upright(double width, double twist = 0)
    {
        Ribbon ribbon = new () { Twist = twist };

        ribbon.Points.Add(new RibbonControlPoint { Position = new Point(0, 0, 0), Width = width });
        ribbon.Points.Add(new RibbonControlPoint { Position = new Point(0, 0.5, 0), Width = width });
        ribbon.Points.Add(new RibbonControlPoint { Position = new Point(0, 1, 0), Width = width });

        ribbon.PrepareForRendering();

        return ribbon;
    }

    /// <summary>
    /// The strip is exactly as wide as it is asked to be, and no wider: rays fired at it from in
    /// front land on it within half its width of the middle and miss beyond.
    /// </summary>
    [TestMethod]
    public void TestARibbonIsAsWideAsItIsAsked()
    {
        Ribbon ribbon = Upright(0.4);

        foreach ((double x, bool hits) in new[]
                 { (0.0, true), (0.15, true), (-0.15, true), (0.25, false), (-0.25, false) })
        {
            Ray ray = new Ray(new Point(x, 0.5, -3), Directions.In);
            List<Intersection> intersections = [];

            ribbon.AddIntersections(ray, intersections);

            Assert.AreEqual(hits, intersections.Count > 0,
                $"At x={x} the ribbon was {(intersections.Count > 0 ? "hit" : "missed")}.");

            if (hits)
            {
                Assert.IsTrue(intersections[0].Distance.Near(3),
                    $"At x={x} the crossing was at {intersections[0].Distance}, wanted 3.");
            }
        }
    }

    /// <summary>
    /// It is flat, and it faces the reader.  A rod of the same width would be hit at a distance that
    /// changed across it, and its normal would turn with the point; a ribbon's does neither.
    /// </summary>
    [TestMethod]
    public void TestARibbonIsFlatAndFacesOneWay()
    {
        Ribbon ribbon = Upright(0.4);

        foreach (double x in new[] { -0.15, 0.0, 0.15 })
        {
            Ray ray = new Ray(new Point(x, 0.35, -3), Directions.In);
            List<Intersection> intersections = [];

            ribbon.AddIntersections(ray, intersections);

            Assert.AreEqual(1, intersections.Count, $"At x={x} the ribbon was not crossed once.");
            Assert.IsTrue(intersections[0].Distance.Near(3),
                $"At x={x} the crossing was at {intersections[0].Distance}: the strip is not flat.");

            Vector normal = intersections[0].Surface.NormalAt(
                ray.At(intersections[0].Distance), intersections[0]);

            Assert.IsTrue(Math.Abs(normal.Z).Near(1),
                $"At x={x} the normal was {normal}, which is not square to the strip.");
        }
    }

    /// <summary>
    /// A twist turns the ribbon about its own length, so a face that met the reader at the foot is
    /// edge-on at the top.  Both halves are checked: a twist that turned nothing would pass the first
    /// and fail the second, and one that turned everything the other way about would do the reverse.
    /// </summary>
    [TestMethod]
    public void TestATwistTurnsTheRibbonAlongItsLength()
    {
        Ribbon ribbon = Upright(0.4, Math.PI / 2);
        List<Intersection> atTheFoot = [];
        List<Intersection> atTheTop = [];

        ribbon.AddIntersections(new Ray(new Point(0.15, 0.05, -3), Directions.In), atTheFoot);
        ribbon.AddIntersections(new Ray(new Point(0.15, 0.95, -3), Directions.In), atTheTop);

        Assert.IsTrue(atTheFoot.Count > 0, "The foot of a twisted ribbon should still face the front.");
        Assert.AreEqual(0, atTheTop.Count, "The top of a quarter-turned ribbon should be edge-on.");

        // And it is still there, seen from the side it has turned towards.
        List<Intersection> fromTheSide = [];

        ribbon.AddIntersections(new Ray(new Point(-3, 0.95, 0.15), Directions.Right), fromTheSide);

        Assert.IsTrue(fromTheSide.Count > 0, "The top should be broadside from the side it turned to.");
    }

    /// <summary>
    /// The join between two patches must not show.  Each is handed the normals of the points it
    /// spans, so the shading runs on through: sampled either side of a joint, the two normals must
    /// very nearly agree, where a strip of plain patches would step.
    /// <para>
    /// **The path has to curve out of its own face for this to say anything**, which took two goes to
    /// see.  A ribbon bending within one plane keeps its face pointing the same way the whole length,
    /// so both patches agree exactly whether they were given normals or not, and the test passed on a
    /// ribbon built without any.  This path turns in X as well, so the face really does swing: with
    /// the normals the two sides lie 0.999 along one another, and without them 0.59 -- a crease
    /// anyone would see.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestTheJoinsDoNotShow()
    {
        Ribbon ribbon = new ();

        // A ribbon with a real bend in it, so the two patches meeting at the middle genuinely differ.
        // It bends in Y/Z, so the frame lays the strip across that plane and turns its face to X --
        // which is why it is looked at from along X rather than from the front.
        ribbon.Points.Add(new RibbonControlPoint { Position = new Point(0, 0, 0), Width = 0.4 });
        ribbon.Points.Add(new RibbonControlPoint { Position = new Point(0.3, 0.5, 0.3), Width = 0.4 });
        ribbon.Points.Add(new RibbonControlPoint { Position = new Point(0, 0.9, 0.75), Width = 0.4 });

        ribbon.PrepareForRendering();

        Vector Sample(double y)
        {
            Ray ray = new Ray(new Point(-3, y, y * 0.62), Directions.Right);
            List<Intersection> intersections = [];

            ribbon.AddIntersections(ray, intersections);

            Assert.IsTrue(intersections.Count > 0, $"Nothing was crossed at y={y}.");

            Intersection nearest = intersections.MinBy(crossing => crossing.Distance);

            return nearest.Surface.NormalAt(ray.At(nearest.Distance), nearest);
        }

        Vector below = Sample(0.48);
        Vector above = Sample(0.52);
        double agreement = Math.Abs(below.Dot(above));

        Assert.IsTrue(agreement > 0.99,
            $"Across the join the normals lie {agreement} along one another: {below} against {above}.");
    }
}
