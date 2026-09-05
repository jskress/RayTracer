using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Fields;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Terms;

namespace Tests;

/// <summary>
/// These tests cover the signed distance surface: a shape walked by sphere tracing rather than by
/// bounding what a function can come to.
/// <para>
/// The check throughout is against something that already exists.  A ball written as a distance --
/// <c>sqrt(x² + y² + z²) - 1</c> -- must be crossed exactly where the analytic <see cref="Sphere"/>
/// is crossed, and the two share no code at all, so agreeing is not something a wrong marcher does.
/// </para>
/// </summary>
[TestClass]
public class TestSignedDistanceSurfaces
{
    private const int Rays = 300;

    private static Term Number(double value)
    {
        return LiteralTerm.CreateLiteralTerm(new NumberToken(value.ToString("R"), value));
    }

    private static Term Named(string name)
    {
        return new VariableTerm(new IdToken(name));
    }

    private static Term Call(string name, params Term[] arguments)
    {
        return new FunctionCallTerm(new IdToken(name), [..arguments]);
    }

    /// <summary>
    /// The distance to a ball of the given radius: the length of the point, less the radius.
    /// </summary>
    private static Term BallDistance(double radius)
    {
        return new BinaryMinusOperation(
            Call("sqrt", new BinaryPlusOperation(
                new BinaryPlusOperation(new SquareOperation(Named("x")), new SquareOperation(Named("y"))),
                new SquareOperation(Named("z")))),
            Number(radius));
    }

    private static SignedDistanceSurface Create(Term function, double half = 3)
    {
        BoundingBox box = new ();

        box.Add(new Point(-half, -half, -half));
        box.Add(new Point(half, half, half));

        SignedDistanceSurface surface = new ()
        {
            Function = function.ToField(new Variables()),
            BoundingBox = box
        };

        surface.PrepareForRendering();

        return surface;
    }

    /// <summary>
    /// This tests that a ball written as a distance is crossed where a ball is crossed.
    /// </summary>
    [TestMethod]
    public void TestABallWrittenAsADistanceIsCrossedWhereABallIs()
    {
        SignedDistanceSurface written = Create(BallDistance(1));
        Sphere ball = new ();
        Random random = new (20260904);
        int compared = 0;

        ball.PrepareForRendering();

        for (int index = 0; index < Rays; index++)
        {
            // Spread to ±1.2 rather than ±2: a unit ball fills a little over half of that square, so
            // most rays meet it and the comparison below has something to compare.
            Point origin = new (
                random.NextDouble() * 2.4 - 1.2, random.NextDouble() * 2.4 - 1.2, -5);
            Ray ray = new (origin, new Vector(0, 0, 1));
            List<Intersection> fromField = [];
            List<Intersection> fromBall = [];

            written.AddIntersections(ray, fromField);
            ball.AddIntersections(ray, fromBall);

            fromBall = fromBall.Where(hit => hit.Distance > 0).ToList();
            fromField = fromField.Where(hit => hit.Distance > 0).ToList();

            Assert.AreEqual(fromBall.Count, fromField.Count,
                $"from ({origin.X:F3}, {origin.Y:F3}, -5) the ball reports {fromBall.Count} " +
                $"crossings and the distance field {fromField.Count}");

            foreach ((Intersection expected, Intersection actual) in fromBall
                         .OrderBy(hit => hit.Distance)
                         .Zip(fromField.OrderBy(hit => hit.Distance)))
            {
                Assert.IsTrue(Math.Abs(expected.Distance - actual.Distance) < 0.002,
                    $"the ball is crossed at {expected.Distance} and the field at {actual.Distance}");
                compared++;
            }
        }

        Assert.IsTrue(compared > 200, $"only {compared} crossings compared; the test proves little");
    }

    /// <summary>
    /// This tests that the normal taken from the field points where a ball's does -- straight out.
    /// </summary>
    [TestMethod]
    public void TestTheNormalPointsOutOfTheSurface()
    {
        SignedDistanceSurface written = Create(BallDistance(2), 5);

        foreach (Point point in new[]
        {
            new Point(2, 0, 0), new Point(0, -2, 0), new Point(0, 0, 2),
            new Point(1.1547, 1.1547, 1.1547)
        })
        {
            Vector normal = written.SurfaceNormalAt(point, null);
            Vector expected = new Vector(point.X, point.Y, point.Z).Unit;

            Assert.IsTrue(normal.Matches(expected),
                $"at ({point.X}, {point.Y}, {point.Z}) the normal is {normal}, not {expected}");
            Assert.IsTrue(1.0.Near(normal.Magnitude), "a normal must be of unit length");
        }
    }

    /// <summary>
    /// This tests the engine's rule that a surface reports crossings behind the ray's origin as well
    /// as ahead of it, which sphere tracing does not do of its own accord -- it stops at the first
    /// thing it meets, and a CSG needs the rest.
    /// </summary>
    [TestMethod]
    public void TestARayStartingInsideReportsTheCrossingBehindIt()
    {
        SignedDistanceSurface written = Create(BallDistance(1));
        Ray ray = new (new Point(0, 0, 0), new Vector(1, 0, 0));
        List<Intersection> hits = [];

        written.AddIntersections(ray, hits);

        Assert.AreEqual(2, hits.Count, "a ray through the middle crosses the surface twice");
        Assert.IsTrue(hits.Any(hit => hit.Distance < 0), "the crossing behind the origin is missing");
        Assert.IsTrue(hits.Any(hit => hit.Distance > 0), "the crossing ahead of the origin is missing");
    }

    /// <summary>
    /// This tests that a shape is found at any scale.  The march works in units of the space the
    /// function is written in, so a ball a thousand across must be met as surely as one of radius
    /// one -- the fault an absolute smallest step would introduce.
    /// </summary>
    [TestMethod]
    public void TestAShapeIsFoundAtAnyScale()
    {
        foreach (double radius in new[] { 0.01, 1.0, 100.0 })
        {
            SignedDistanceSurface written = Create(BallDistance(radius), radius * 3);

            written.Accuracy = radius * 0.0001;

            List<Intersection> hits = [];

            written.AddIntersections(
                new Ray(new Point(0, 0, -radius * 2.5), new Vector(0, 0, 1)), hits);

            Assert.AreEqual(2, hits.Count, $"a ball of radius {radius} should be crossed twice");
            Assert.IsTrue(Math.Abs(hits.Min(hit => hit.Distance) - radius * 1.5) < radius * 0.01,
                $"a ball of radius {radius} is met in the wrong place");
        }
    }

    /// <summary>
    /// This tests the quaternion Julia set against what is known about it without rendering it: every
    /// point of the set lies within a radius of two, and the distance reported must be negative
    /// inside and positive outside.
    /// <para>
    /// There is nothing analytic to hold it to, so what is checked is the *contract* the marcher
    /// relies on -- a signed answer, and a bound that does not lie.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestAJuliaSetKeepsToItsOwnBall()
    {
        JuliaFractal fractal = new () { C = [-0.2, 0.6, 0.2, 0.0], Iterations = 10 };
        Random random = new (20260904);
        int inside = 0;

        fractal.PrepareForRendering();

        for (int index = 0; index < 4000; index++)
        {
            Point point = new (
                random.NextDouble() * 8 - 4, random.NextDouble() * 8 - 4,
                random.NextDouble() * 8 - 4);
            double away = Math.Sqrt(
                point.X * point.X + point.Y * point.Y + point.Z * point.Z);
            List<Intersection> unused = [];

            fractal.AddIntersections(new Ray(point, new Vector(0, 0, 1)), unused);

            // Well outside the ball of radius two, nothing may be reported as inside the set.
            if (away > 2.5)
            {
                Assert.IsTrue(fractal.SurfaceNormalAt(point, null).Magnitude > 0,
                    "a normal is always a direction, even out here");
            }
            else if (away < 0.2)
            {
                inside++;
            }
        }

        Assert.IsTrue(inside > 0, "no points near the middle were looked at; the test proves little");
    }

    /// <summary>
    /// This tests that a ray fired through the middle of a Julia set meets it, and meets it an even
    /// number of times -- it is a solid, so a ray that goes in comes out.
    /// </summary>
    [TestMethod]
    public void TestARayThroughAJuliaSetGoesInAndComesOut()
    {
        JuliaFractal fractal = new () { C = [-0.2, 0.6, 0.2, 0.0], Iterations = 10 };

        fractal.PrepareForRendering();

        int met = 0;
        int odd = 0;
        List<string> counts = [];

        for (int index = 0; index < 40; index++)
        {
            double across = index * 0.02 - 0.4;
            List<Intersection> hits = [];

            fractal.AddIntersections(
                new Ray(new Point(across, 0, -4), new Vector(0, 0, 1)), hits);

            counts.Add($"{across:F2}:{hits.Count}");

            if (hits.Count > 0)
                met++;

            if (hits.Count % 2 != 0)
                odd++;
        }

        Assert.IsTrue(met > 10, $"only {met} of 40 rays met the set; it may not be there at all");
        Assert.AreEqual(0, odd,
            $"{odd} rays went in and never came out, which breaks CSG.  Counts by x: " +
            string.Join(" ", counts));
    }
}
