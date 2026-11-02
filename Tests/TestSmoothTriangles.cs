using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace Tests;

[TestClass]
public class TestSmoothTriangles
{
    private static readonly Point Point1 = new (0, 1, 0);
    private static readonly Point Point2 = new (-1, 0, 0);
    private static readonly Point Point3 = new (1, 0, 0);

    private readonly SmoothTriangle _smoothTriangle = new ()
    {
        Point1 = Point1,
        Point2 = Point2,
        Point3 = Point3,
        Normal1 = Directions.Up,
        Normal2 = Directions.Left,
        Normal3 = Directions.Right
    };

    [TestMethod]
    public void TestConstruction()
    {
        Assert.AreSame(Point1, _smoothTriangle.Point1);
        Assert.AreSame(Point2, _smoothTriangle.Point2);
        Assert.AreSame(Point3, _smoothTriangle.Point3);
        Assert.AreSame(Directions.Up, _smoothTriangle.Normal1);
        Assert.AreSame(Directions.Left, _smoothTriangle.Normal2);
        Assert.AreSame(Directions.Right, _smoothTriangle.Normal3);
    }

    [TestMethod]
    public void TestUvIntersectionCreation()
    {
        SmoothTriangleIntersection intersection = new (
            _smoothTriangle, 3.5, 0.2, 0.4);

        Assert.AreSame(_smoothTriangle, intersection.Surface);
        Assert.AreEqual(3.5, intersection.Distance);
        Assert.AreEqual(0.2, intersection.U);
        Assert.AreEqual(0.4, intersection.V);
    }

    [TestMethod]
    public void TestRayIntersectionProducesUv()
    {
        Ray ray = new (new Point(-0.2, 0.3, -2), Directions.In);
        List<Intersection> intersections = new ();

        _smoothTriangle.AddIntersections(ray, intersections);

        Assert.AreEqual(1, intersections.Count);
        Assert.IsTrue(intersections[0] is SmoothTriangleIntersection);

        SmoothTriangleIntersection intersection = (SmoothTriangleIntersection) intersections[0];

        Assert.IsTrue(0.45.Near(intersection.U));
        Assert.IsTrue(0.25.Near(intersection.V));
    }

    [TestMethod]
    public void TestNormalInterpolation()
    {
        SmoothTriangleIntersection intersection = new (
            _smoothTriangle, 1, 0.45, 0.25);
        Vector normal = _smoothTriangle.NormalAt(Point.Zero, intersection);
        Vector expected = new (-0.5547, 0.83205, 0);

        Assert.IsTrue(expected.Matches(normal));
    }

    [TestMethod]
    public void TestNormalAt()
    {
        SmoothTriangleIntersection intersection = new (
            _smoothTriangle, 1, 0.45, 0.25);
        Ray ray = new Ray(new Point(-0.2, 0.3, -2), Directions.In);
        List<Intersection> intersections = [intersection];
        Vector expected = new (-0.5547, 0.83205, 0);

        intersection.PrepareUsing(ray, intersections);

        Assert.IsTrue(expected.Matches(intersection.Normal));
    }

    /// <summary>
    /// A smooth triangle shares <see cref="Triangle.AddIntersections"/> unchanged, so a ray
    /// pointing away from it (see <see cref="TestTriangles.TestRayPointingAwayMisses"/>) must
    /// be rejected here too.
    /// </summary>
    [TestMethod]
    public void TestRayPointingAwayMisses()
    {
        Ray ray = new (new Point(-0.2, 0.3, 2), Directions.In);
        List<Intersection> intersections = new ();

        _smoothTriangle.AddIntersections(ray, intersections);

        // The crossing behind the origin is reported now (a CSG needs it), so a ray pointing
        // away "misses" in the sense that matters: nothing in front of it.
        Assert.IsFalse(
            intersections.Any(intersection => intersection.Distance >= 0),
            "a ray pointing away should have no hit in front of it");
    }

    /// <summary>
    /// This is a helper that returns a smooth triangle cut from the unit ball: its corners on the ball and their
    /// normals the ball's there, facing out, or in for the inside of a hollow.
    /// </summary>
    private static SmoothTriangle FromTheBall(bool hollow = false)
    {
        Vector a = new Vector(0.2, 0.3, -1).Unit;
        Vector b = new Vector(-0.35, 0.1, -1).Unit;
        Vector c = new Vector(0.15, -0.3, -1).Unit;
        double facing = hollow ? -1 : 1;

        return new SmoothTriangle
        {
            Point1 = Point.Zero + a, Point2 = Point.Zero + b, Point3 = Point.Zero + c,
            Normal1 = a * facing, Normal2 = b * facing, Normal3 = c * facing
        };
    }

    /// <summary>
    /// This is a helper that returns the hit a ray straight at the middle of a triangle makes, from the side given.
    /// </summary>
    private static Intersection HitInTheMiddleOf(Triangle triangle, double side)
    {
        Point middle = new (
            (triangle.Point1.X + triangle.Point2.X + triangle.Point3.X) / 3,
            (triangle.Point1.Y + triangle.Point2.Y + triangle.Point3.Y) / 3,
            (triangle.Point1.Z + triangle.Point2.Z + triangle.Point3.Z) / 3);
        Ray ray = new (middle + new Vector(0, 0, 4 * side), new Vector(0, 0, -side));
        List<Intersection> intersections = [];

        triangle.PrepareForRendering();
        triangle.Intersect(ray, intersections);

        Intersection hit = intersections.Single(crossing => crossing.Distance > 0);

        hit.PrepareUsing(ray, intersections);

        return hit;
    }

    /// <summary>
    /// This tests that a ray leaving a smooth triangle sets off from about where the round surface its normals
    /// describe would be: out from the flat triangle to the ball it was cut from, and no further outside it than the
    /// flat triangle lies inside (the rounding is Phong tessellation's, which rounds a little past a ball).
    /// </summary>
    [TestMethod]
    public void TestARayLeavingRisesToTheRoundSurface()
    {
        Intersection hit = HitInTheMiddleOf(FromTheBall(), -1);
        double flat = (hit.OverPoint - Point.Zero).Magnitude;
        double risen = (hit.LitPoint - Point.Zero).Magnitude;

        Assert.IsTrue(risen >= 1, $"it set off {risen} from the middle, short of the ball");
        Assert.IsTrue(risen - 1 <= 1 - flat, $"it set off {risen} from the middle, the flat triangle {flat}");
        Assert.AreSame(hit.LitPoint, hit.MirrorPoint, "a mirrored ray must set off from the same place");
    }

    /// <summary>
    /// This tests the places a ray leaving a smooth triangle does not rise from: a triangle whose corners all face as
    /// it does, a hollow (where it is already above every corner's plane), and the side its normals turn from.
    /// </summary>
    [TestMethod]
    public void TestARayLeavingRisesOnlyFromARoundedFace()
    {
        SmoothTriangle flat = new ()
        {
            Point1 = Point1, Point2 = Point2, Point3 = Point3,
            Normal1 = Directions.Out, Normal2 = Directions.Out, Normal3 = Directions.Out
        };
        Intersection onTheFlat = HitInTheMiddleOf(flat, -1);
        Intersection inTheHollow = HitInTheMiddleOf(FromTheBall(hollow: true), 1);
        Intersection fromBehind = HitInTheMiddleOf(FromTheBall(), 1);

        Assert.AreSame(onTheFlat.OverPoint, onTheFlat.LitPoint, "a flat triangle rose");
        Assert.AreSame(inTheHollow.OverPoint, inTheHollow.LitPoint, "a hollow rose");
        Assert.IsTrue(fromBehind.Inside);
        Assert.IsTrue(
            (fromBehind.Point + fromBehind.Eye * fromBehind.SelfOffset).Matches(fromBehind.LitPoint),
            "a triangle met from behind rose");
    }

    /// <summary>
    /// This tests that a ray leaving a smooth triangle never sets off from behind it: a corner whose normal faces
    /// behind the flat triangle, as one on a crest too sharp for its mesh may, lifts nothing.
    /// </summary>
    [TestMethod]
    public void TestARayLeavingNeverSinksBehindTheFlatTriangle()
    {
        SmoothTriangle twisted = new ()
        {
            Point1 = Point1, Point2 = Point2, Point3 = Point3,
            Normal1 = Directions.Out, Normal2 = Directions.Out, Normal3 = new Vector(1, 0, 0.3)
        };
        Intersection hit = HitInTheMiddleOf(twisted, -1);

        Assert.IsFalse(hit.Inside);
        Assert.IsTrue(hit.LitPoint.Z < 0, $"it set off from {hit.LitPoint}, behind the triangle");
        Assert.AreSame(hit.OverPoint, hit.LitPoint, "the twisted corner lifted it");
    }

    /// <summary>
    /// This is a helper that returns a ball of smooth triangles, as a coarse mesh with its vertex normals would be:
    /// <paramref name="around"/> facets round and half as many from pole to pole.
    /// </summary>
    private static Group CoarseBall(int around)
    {
        int down = around / 2;
        Group ball = new ();

        for (int ring = 0; ring < down; ring++)
        {
            for (int step = 0; step < around; step++)
            {
                Vector a = OnTheBall(ring, step), b = OnTheBall(ring, step + 1);
                Vector c = OnTheBall(ring + 1, step + 1), d = OnTheBall(ring + 1, step);

                if (ring > 0)
                    ball.Add(Facet(a, b, c));

                if (ring < down - 1)
                    ball.Add(Facet(a, c, d));
            }
        }

        ball.PrepareForRendering();

        return ball;

        Vector OnTheBall(int ring, int step)
        {
            double down = Math.PI * ring / (around / 2);
            double round = 2 * Math.PI * step / around;

            return new Vector(Math.Sin(down) * Math.Cos(round), Math.Cos(down), Math.Sin(down) * Math.Sin(round));
        }

        SmoothTriangle Facet(Vector a, Vector b, Vector c) => new ()
        {
            Point1 = Point.Zero + a, Point2 = Point.Zero + b, Point3 = Point.Zero + c,
            Normal1 = a, Normal2 = b, Normal3 = c
        };
    }

    /// <summary>
    /// This tests that a coarse ball lit from the side is lit wherever its shading faces the light: the edge of its
    /// shadow is where the round ball's would be, not a staircase of facets whose shadow rays went into the ball.
    /// The light is off the ball's symmetry, so the edge crosses facets rather than running along their corners
    /// (there, every facet faces the way its corners do, and nothing is in the way).
    /// </summary>
    [TestMethod]
    public void TestACoarseBallIsLitWhereverItFacesTheLight()
    {
        Scene scene = new ();
        Point lamp = new (1000, 270, -360);
        int facing = 0;
        List<string> dark = [];

        scene.Lights.Add(new PointLight { Location = lamp });
        scene.Surfaces.Add(CoarseBall(12));

        for (double x = -0.95; x < 0.95; x += 0.01)
        {
            for (double y = -0.95; y < 0.95; y += 0.01)
            {
                Ray ray = new (new Point(x, y, -5), Directions.In);
                List<Intersection> intersections = scene.Intersect(ray);
                Intersection hit = intersections.Hit();

                if (hit is null)
                    continue;

                hit.PrepareUsing(ray, intersections);

                Vector toward = (lamp - hit.LitPoint).Unit;

                if (hit.Normal.Dot(toward) <= 0)
                    continue;

                facing++;

                if (Colors.Black.Matches(scene.GetLightReaching(hit.LitPoint, toward, (lamp - hit.LitPoint).Magnitude)))
                    dark.Add($"({x:F2}, {y:F2})");
            }
        }

        Assert.IsTrue(facing > 1000, $"only {facing} places faced the light");
        Assert.AreEqual(0, dark.Count,
            $"{dark.Count} of {facing} places facing the light were in shadow, among them {string.Join(", ", dark.Take(5))}");
    }
}
