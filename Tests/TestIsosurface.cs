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
/// These tests cover the isosurface: following a ray through a function rather than solving an
/// equation for it.
/// <para>
/// Most of them lean on the same trick, which is that a function can be written whose surface is
/// already known.  <c>x² + y² + z² - 1</c> at nought is a unit sphere, and this ray tracer has an
/// analytic sphere to compare against -- so the marcher can be held to the answer a closed-form
/// solution gives, rather than merely to whatever it happens to produce.
/// </para>
/// </summary>
[TestClass]
public class TestIsosurface
{
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
    /// Builds an isosurface from a term, over a box big enough to hold what these tests draw, and gets
    /// it ready to render.
    /// </summary>
    private static Isosurface Create(Term function, double threshold = 0, double half = 3)
    {
        BoundingBox box = new ();

        box.Add(new Point(-half, -half, -half));
        box.Add(new Point(half, half, half));

        Isosurface isosurface = new ()
        {
            Function = function.ToField(new Variables()),
            Threshold = threshold,
            BoundingBox = box
        };

        isosurface.PrepareForRendering();

        return isosurface;
    }

    /// <summary>
    /// The function of the unit sphere, x² + y² + z², whose surface at nought is that sphere less one.
    /// </summary>
    private static Term SphereFunction(double radius = 1)
    {
        return new BinaryMinusOperation(
            new BinaryPlusOperation(
                new BinaryPlusOperation(new SquareOperation(Named("x")), new SquareOperation(Named("y"))),
                new SquareOperation(Named("z"))),
            Number(radius * radius));
    }

    /// <summary>
    /// This tests that a function whose surface is a sphere is crossed where that sphere is, to the
    /// accuracy asked for -- checked against the analytic sphere rather than against expected numbers,
    /// so that the two must agree rather than merely each look reasonable.
    /// </summary>
    [TestMethod]
    public void TestASphereIsFoundWhereASphereIs()
    {
        Isosurface isosurface = Create(SphereFunction());
        Sphere sphere = new ();
        Random random = new (20260804);

        for (int index = 0; index < 40; index++)
        {
            // Rays from well outside, aimed near enough the middle to hit.
            Point origin = new (
                random.NextDouble() * 4 - 2, random.NextDouble() * 4 - 2, -5);
            Ray ray = new (origin, new Vector(0, 0, 1));
            List<Intersection> fromTheField = [];
            List<Intersection> fromTheSphere = [];

            isosurface.AddIntersections(ray, fromTheField);
            sphere.AddIntersections(ray, fromTheSphere);

            fromTheSphere = fromTheSphere.Where(hit => hit.Distance > 0).ToList();
            fromTheField = fromTheField.Where(hit => hit.Distance > 0).ToList();

            Assert.AreEqual(fromTheSphere.Count, fromTheField.Count,
                $"from ({origin.X:F3}, {origin.Y:F3}, -5) the sphere reports " +
                $"{fromTheSphere.Count} crossings and the field {fromTheField.Count}");

            foreach ((Intersection expected, Intersection actual) in fromTheSphere
                         .OrderBy(hit => hit.Distance)
                         .Zip(fromTheField.OrderBy(hit => hit.Distance)))
            {
                Assert.IsTrue(Math.Abs(expected.Distance - actual.Distance) < 0.001,
                    $"the sphere is crossed at {expected.Distance} and the field at {actual.Distance}");
            }
        }
    }

    /// <summary>
    /// This tests that the normal of a function whose surface is a sphere points the way that sphere's
    /// does -- straight out from the middle.  This is the gradient doing its second job.
    /// </summary>
    [TestMethod]
    public void TestTheNormalPointsOutOfTheSurface()
    {
        Isosurface isosurface = Create(SphereFunction(2));

        foreach (Point point in new[]
        {
            new Point(2, 0, 0), new Point(0, -2, 0), new Point(0, 0, 2),
            new Point(1.1547, 1.1547, 1.1547)
        })
        {
            Vector normal = isosurface.SurfaceNormaAt(point, null);
            Vector expected = new Vector(point.X, point.Y, point.Z).Unit;

            Assert.IsTrue(normal.Matches(expected),
                $"at ({point.X}, {point.Y}, {point.Z}) the normal is {normal}, not {expected}");
            Assert.IsTrue(1.0.Near(normal.Magnitude), "a normal must be of unit length");
        }
    }

    /// <summary>
    /// This tests the engine's rule that a surface reports the crossings behind the ray's origin as well
    /// as those ahead of it.  A CSG walks the sorted crossings from far behind the origin forward,
    /// toggling inside and outside, so a ray that starts within a solid must see the crossing it came in
    /// through or everything after it is read the wrong way round.
    /// </summary>
    [TestMethod]
    public void TestRayStartingInsideReportsTheCrossingBehindIt()
    {
        Isosurface isosurface = Create(SphereFunction());
        Ray ray = new (new Point(0, 0, 0), new Vector(1, 0, 0));
        List<Intersection> intersections = [];

        isosurface.AddIntersections(ray, intersections);

        Assert.IsTrue(intersections.Any(intersection => intersection.Distance < 0),
            "the crossing behind the origin must be reported for a ray starting inside");
        Assert.IsTrue(intersections.Any(intersection => intersection.Distance > 0),
            "the crossing ahead of the origin must be reported");
    }

    /// <summary>
    /// This tests the other half of that rule: a ray starting just <i>outside</i> the surface, as every
    /// shadow and reflection ray cast from it does, must not find the surface it has just left behind
    /// it.  Looking backward only when the origin is genuinely inside is what keeps those apart.
    /// </summary>
    [TestMethod]
    public void TestRayLeavingTheSurfaceDoesNotFindItBehind()
    {
        Isosurface isosurface = Create(SphereFunction());
        // Just outside the sphere at its top, heading up and away, exactly as a shadow ray would.
        Ray ray = new (new Point(0, 1 + 1e-5, 0), new Vector(0, 1, 0));
        List<Intersection> intersections = [];

        isosurface.AddIntersections(ray, intersections);

        Assert.IsEmpty(intersections.Where(intersection => intersection.Distance < 0).ToList(),
            "a ray leaving the surface must not report the surface it left as being behind it");
    }

    /// <summary>
    /// This tests that a ray nowhere near the surface reports nothing, and in particular that the
    /// bounding is what makes that cheap rather than a search that finds nothing.
    /// </summary>
    [TestMethod]
    public void TestRayMissesTheSurface()
    {
        Isosurface isosurface = Create(SphereFunction());
        List<Intersection> intersections = [];

        // Through the box, but well past the sphere inside it.
        isosurface.AddIntersections(
            new Ray(new Point(2.5, 2.5, -5), new Vector(0, 0, 1)), intersections);

        Assert.IsEmpty(intersections);

        // And nowhere near the box at all.
        isosurface.AddIntersections(
            new Ray(new Point(50, 50, 50), new Vector(1, 0, 0)), intersections);

        Assert.IsEmpty(intersections);
    }

    /// <summary>
    /// This tests that the value the surface is drawn at is honoured, so that one function can make
    /// shells of different sizes.  This is the "iso" of the name: the same field, a different level.
    /// </summary>
    [TestMethod]
    public void TestTheThresholdChoosesWhichSurface()
    {
        Term distanceSquared = new BinaryPlusOperation(
            new BinaryPlusOperation(new SquareOperation(Named("x")), new SquareOperation(Named("y"))),
            new SquareOperation(Named("z")));

        foreach (double radius in new[] { 0.5, 1.0, 2.0 })
        {
            Isosurface isosurface = Create(distanceSquared, radius * radius);
            List<Intersection> intersections = [];

            isosurface.AddIntersections(
                new Ray(new Point(0, 0, -5), new Vector(0, 0, 1)), intersections);

            double nearest = intersections.Where(hit => hit.Distance > 0).Min(hit => hit.Distance);

            Assert.IsTrue(Math.Abs(5 - radius - nearest) < 0.001,
                $"at a threshold of r² = {radius * radius} the surface should be {5 - radius} away, " +
                $"and it is {nearest}");
        }
    }

    /// <summary>
    /// This tests a function that is not a sphere, and one whose surface an analytic shape can also be
    /// compared against: a torus, written the way anyone would write one.
    /// </summary>
    [TestMethod]
    public void TestATorusIsFoundWhereATorusIs()
    {
        // (√(x² + z²) - 2)² + y² - 0.25, which is a torus of major radius 2 and minor radius 0.5.
        Term fromTheRing = new BinaryMinusOperation(
            Call("sqrt", new BinaryPlusOperation(
                new SquareOperation(Named("x")), new SquareOperation(Named("z")))),
            Number(2));
        Isosurface isosurface = Create(new BinaryMinusOperation(
            new BinaryPlusOperation(new SquareOperation(fromTheRing), new SquareOperation(Named("y"))),
            Number(0.25)));
        Torus torus = new () { MajorRadius = 2, MinorRadius = 0.5 };

        torus.PrepareForRendering();

        // Down the middle of the ring's outer edge, which crosses the tube twice.
        Ray ray = new (new Point(-5, 0, 0), new Vector(1, 0, 0));
        List<Intersection> fromTheField = [];
        List<Intersection> fromTheTorus = [];

        isosurface.AddIntersections(ray, fromTheField);
        torus.AddIntersections(ray, fromTheTorus);

        List<double> field = fromTheField
            .Select(hit => hit.Distance).Where(distance => distance > 0).Order().ToList();
        List<double> analytic = fromTheTorus
            .Select(hit => hit.Distance).Where(distance => distance > 0).Order().ToList();

        Assert.HasCount(4, analytic, "the analytic torus should be crossed four times here");
        Assert.AreEqual(analytic.Count, field.Count,
            $"the torus reports {string.Join(", ", analytic)} and the field {string.Join(", ", field)}");

        foreach ((double expected, double actual) in analytic.Zip(field))
            Assert.IsTrue(Math.Abs(expected - actual) < 0.001, $"{expected} against {actual}");
    }

    /// <summary>
    /// This tests what happens to a ray that touches the surface without passing through it.  It is
    /// reported as a miss, since a crossing here is a change of sign and a touch is not one, and that is
    /// deliberate: a tangent ray grazes the silhouette where neighbouring rays settle the pixel, and a
    /// touch is two crossings rather than one, so leaving both out keeps a CSG's inside and outside
    /// count right.  A ray a whisker further in finds both.
    /// </summary>
    [TestMethod]
    public void TestARayThatMerelyTouchesTheSurface()
    {
        Isosurface isosurface = Create(SphereFunction());
        List<Intersection> touching = [];
        List<Intersection> justInside = [];

        // Exactly tangent to the top of the unit sphere.
        isosurface.AddIntersections(
            new Ray(new Point(0, 1, -5), new Vector(0, 0, 1)), touching);

        Assert.IsEmpty(touching, "a ray that only touches the surface reports no crossing");

        // A whisker inside it, where there really are two crossings to find.
        isosurface.AddIntersections(
            new Ray(new Point(0, 0.999, -5), new Vector(0, 0, 1)), justInside);

        Assert.HasCount(2, justInside.Where(hit => hit.Distance > 0).ToList());
    }

    /// <summary>
    /// This tests that a surface built from a function that has no slope cannot be prepared, and says so
    /// rather than rendering something with nonsense for its normals.
    /// </summary>
    [TestMethod]
    public void TestAFunctionWithNoSlopeIsRefused()
    {
        Isosurface isosurface = new ()
        {
            Function = Call("smoothstep", Number(0), Number(1), Named("x")).ToField(new Variables())
        };

        Assert.ThrowsExactly<Lex.Parser.TokenException>(() => isosurface.PrepareForRendering());
    }
}
