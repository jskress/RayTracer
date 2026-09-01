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
/// These tests cover the parametric surface: a sheet named by three pieces of arithmetic in two
/// parameters.
/// <para>
/// They lean on the same trick the isosurface tests do, which is that a sheet can be written whose
/// shape is already known.  A unit sphere can be spelled out in <c>u</c> and <c>v</c>, and this ray
/// tracer has an analytic sphere to hold it to -- so the narrowing and solving are checked against a
/// closed-form answer rather than against whatever they happen to produce.
/// </para>
/// </summary>
[TestClass]
public class TestParametric
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
    /// Lowers a term the way the parser does, with <c>u</c> and <c>v</c> standing for the two
    /// parameters.
    /// </summary>
    private static FieldExpression Lower(Term term)
    {
        Variables scope = new ();

        scope.SetValue("u", FieldVariable.X);
        scope.SetValue("v", FieldVariable.Y);

        return term.ToField(scope);
    }

    /// <summary>
    /// Builds a parametric surface and gets it ready to render.
    /// </summary>
    private static Parametric Create(
        Term x, Term y, Term z, double uEnd = Math.Tau, double vEnd = Math.PI)
    {
        Parametric parametric = new ()
        {
            X = Lower(x), Y = Lower(y), Z = Lower(z),
            UDomain = new Interval { Start = 0, End = uEnd },
            VDomain = new Interval { Start = 0, End = vEnd }
        };

        parametric.PrepareForRendering();

        return parametric;
    }

    /// <summary>
    /// The unit sphere, written as a sheet: <c>u</c> goes round it and <c>v</c> from pole to pole.
    /// </summary>
    private static Parametric UnitSphere(double vEnd = Math.PI)
    {
        return Create(
            new BinaryMultiplyOperation(Call("sin", Named("v")), Call("cos", Named("u"))),
            Call("cos", Named("v")),
            new BinaryMultiplyOperation(Call("sin", Named("v")), Call("sin", Named("u"))),
            vEnd: vEnd);
    }

    /// <summary>
    /// This tests that a sheet written as a sphere is crossed where a sphere is, checked against the
    /// analytic sphere rather than against expected numbers, so that the two must agree rather than
    /// merely each look reasonable.
    /// </summary>
    [TestMethod]
    public void TestASphereIsCrossedWhereASphereIs()
    {
        Parametric parametric = UnitSphere();
        Sphere sphere = new ();
        Random random = new (20260831);

        for (int index = 0; index < 40; index++)
        {
            Point origin = new (
                random.NextDouble() * 1.6 - 0.8, random.NextDouble() * 1.6 - 0.8, -5);
            Ray ray = new (origin, new Vector(0, 0, 1));
            List<Intersection> fromTheSheet = [];
            List<Intersection> fromTheSphere = [];

            parametric.AddIntersections(ray, fromTheSheet);
            sphere.AddIntersections(ray, fromTheSphere);

            Assert.AreEqual(fromTheSphere.Count, fromTheSheet.Count,
                $"from ({origin.X:F3}, {origin.Y:F3}, -5) the sphere reports " +
                $"{fromTheSphere.Count} crossings and the sheet {fromTheSheet.Count}");

            foreach ((Intersection expected, Intersection actual) in fromTheSphere
                         .OrderBy(hit => hit.Distance)
                         .Zip(fromTheSheet.OrderBy(hit => hit.Distance)))
            {
                Assert.IsTrue(Math.Abs(expected.Distance - actual.Distance) < 0.001,
                    $"the sphere is crossed at {expected.Distance} and the sheet at {actual.Distance}");
            }
        }
    }

    /// <summary>
    /// This tests that the normal of a sheet written as a sphere points the way that sphere's does --
    /// straight out from the middle.  This is the two slopes, crossed, doing their job.
    /// <para>
    /// The normal is asked for through a crossing, since that is what carries the place on the sheet
    /// the normal belongs to; a bare point does not say where on the sheet it is.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestTheNormalPointsOutOfTheSheet()
    {
        Parametric parametric = UnitSphere();
        Random random = new (20260831);

        for (int index = 0; index < 20; index++)
        {
            Point origin = new (
                random.NextDouble() * 1.4 - 0.7, random.NextDouble() * 1.4 - 0.7, -5);
            Ray ray = new (origin, new Vector(0, 0, 1));
            List<Intersection> found = [];

            parametric.AddIntersections(ray, found);

            Intersection nearest = found.OrderBy(hit => hit.Distance).First();
            Point where = ray.At(nearest.Distance);
            Vector normal = parametric.SurfaceNormalAt(where, nearest);
            Vector expected = new Vector(where.X, where.Y, where.Z).Unit;

            // A sheet has no inside, so which way round the two slopes cross is a matter of how it
            // was written; either way it must lie along the radius.
            Assert.IsTrue(normal.Matches(expected) || normal.Matches(-expected),
                $"at ({where.X:F3}, {where.Y:F3}, {where.Z:F3}) the normal is {normal}, " +
                $"which is not along {expected}");
            Assert.IsTrue(1.0.Near(normal.Magnitude), "a normal must be of unit length");
        }
    }

    /// <summary>
    /// This tests the engine's rule that a surface reports the crossings behind the ray's origin as
    /// well as those ahead of it.  A ray starting within the sheet must see the crossing behind it, or
    /// a CSG walking the sorted crossings reads every one after it the wrong way round.
    /// </summary>
    [TestMethod]
    public void TestRayStartingInsideReportsTheCrossingBehindIt()
    {
        Parametric parametric = UnitSphere();
        Ray ray = new (new Point(0, 0, 0), new Vector(1, 0, 0));
        List<Intersection> found = [];

        parametric.AddIntersections(ray, found);

        Assert.AreEqual(2, found.Count, "a ray through the middle crosses the sheet twice");
        Assert.IsTrue(found.Any(hit => hit.Distance < 0), "the crossing behind the origin is missing");
        Assert.IsTrue(found.Any(hit => hit.Distance > 0), "the crossing ahead of the origin is missing");
    }

    /// <summary>
    /// This tests that the sheet stops where its parameters stop.  Half the range of <c>v</c> is the
    /// top half of the ball, so a ray through where the bottom half would be must find nothing.
    /// <para>
    /// **Both halves are asked about**, since a test that only looked at the half that is there would
    /// pass just as well against a sheet that ignored its domain altogether.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestTheSheetStopsWhereItsParametersDo()
    {
        Parametric half = UnitSphere(Math.PI / 2);
        List<Intersection> above = [];
        List<Intersection> below = [];

        half.AddIntersections(new Ray(new Point(0, 0.5, -5), new Vector(0, 0, 1)), above);
        half.AddIntersections(new Ray(new Point(0, -0.5, -5), new Vector(0, 0, 1)), below);

        Assert.AreEqual(2, above.Count, "the half that is there should be crossed twice");
        Assert.AreEqual(0, below.Count, "the half that was left out should not be crossed at all");
    }

    /// <summary>
    /// This tests that a flat sheet is crossed exactly where arithmetic says it is.  A sphere is a
    /// good test of the narrowing but a poor one of the answer's accuracy, since every point of it is
    /// the same distance out; a tilted flat sheet has a different answer everywhere.
    /// </summary>
    [TestMethod]
    public void TestAFlatSheetIsCrossedWhereItShouldBe()
    {
        // The plane z = u + 2v, over a square of parameters, with x and y the parameters themselves.
        Parametric sheet = Create(
            Named("u"), Named("v"),
            new BinaryPlusOperation(
                Named("u"), new BinaryMultiplyOperation(Number(2), Named("v"))),
            uEnd: 1, vEnd: 1);

        foreach ((double u, double v) in new[] { (0.25, 0.25), (0.8, 0.1), (0.5, 0.9) })
        {
            Ray ray = new (new Point(u, v, -4), new Vector(0, 0, 1));
            List<Intersection> found = [];

            sheet.AddIntersections(ray, found);

            Assert.AreEqual(1, found.Count, $"at ({u}, {v}) a flat sheet is crossed once");
            Assert.IsTrue(Math.Abs(found[0].Distance - (4 + u + 2 * v)) < 0.001,
                $"at ({u}, {v}) the sheet should be crossed at {4 + u + 2 * v}, not {found[0].Distance}");
        }
    }

    /// <summary>
    /// This tests that the sheet's box is the box its arithmetic can reach, which is what lets a group
    /// put a ray down without trying it.
    /// </summary>
    [TestMethod]
    public void TestTheBoxIsTheOneTheSheetFillsOut()
    {
        BoundingBox box = UnitSphere().BoundingBox;

        Assert.IsTrue(box.Minimum.X <= -1 && box.Maximum.X >= 1, "the box must hold the whole sheet");
        Assert.IsTrue(box.Minimum.Y <= -1 && box.Maximum.Y >= 1, "the box must hold the whole sheet");
        Assert.IsTrue(box.Minimum.Z <= -1 && box.Maximum.Z >= 1, "the box must hold the whole sheet");
        Assert.IsTrue(box.Minimum.X > -1.1 && box.Maximum.X < 1.1,
            "the box must be the sheet's own, not a loose one that would prune nothing");
    }

    /// <summary>
    /// This tests that a crossing lands *on* the sheet rather than merely near it.
    /// <para>
    /// **This is not the same test as the one against the analytic sphere**, which allows a
    /// thousandth and so would pass with the crossing sitting a whole ten-thousandth inside the
    /// surface.  That is exactly the fault this catches: a point left inside the sheet is a point
    /// whose shadow ray leaves from under it and meets the sheet it is standing on, and nine pixels
    /// of a rendered ball came out lit by nothing but the ambient because of it.  They looked like
    /// crossings that had been missed, and they were not.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestACrossingLandsOnTheSheetAndNotInsideIt()
    {
        Parametric parametric = UnitSphere();
        Random random = new (20260831);

        for (int index = 0; index < 20; index++)
        {
            Point origin = new (
                random.NextDouble() * 1.4 - 0.7, random.NextDouble() * 1.4 - 0.7, -5);
            Ray ray = new (origin, new Vector(0, 0, 1));
            List<Intersection> found = [];

            parametric.AddIntersections(ray, found);

            foreach (Intersection hit in found)
            {
                Point where = ray.At(hit.Distance);
                double outFromTheMiddle = new Vector(where.X, where.Y, where.Z).Magnitude;

                Assert.IsTrue(Math.Abs(outFromTheMiddle - 1) < 1e-9,
                    $"the crossing sits {Math.Abs(outFromTheMiddle - 1):E2} off the sheet, which is " +
                    "far enough for the point to shadow itself");
            }
        }
    }

    /// <summary>
    /// This tests that no crossing falls between two pieces of the sheet.
    /// <para>
    /// The narrowing halves the parameters, so the joins between pieces fall on the halves, quarters
    /// and eighths of each span.  **Those joins are where a crossing goes missing**: the solving starts
    /// in the middle of a piece, and if it is allowed to step out of that piece and is then refused for
    /// having done so, the piece that owned the crossing may not find it either and it is reported by
    /// nobody.  So these rays are aimed exactly through such joins rather than at random -- forty
    /// random rays never once landed on one, and passed with the fault in place.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestNoCrossingFallsBetweenTwoPieces()
    {
        Parametric parametric = UnitSphere();
        Sphere sphere = new ();
        Point origin = new (0, 0, -5);
        int missing = 0;

        for (int across = 1; across < 16; across++)
        {
            for (int down = 1; down < 16; down++)
            {
                double u = Math.Tau * across / 16;
                double v = Math.PI * down / 16;
                Point on = new (
                    Math.Sin(v) * Math.Cos(u), Math.Cos(v), Math.Sin(v) * Math.Sin(u));
                Ray ray = new (origin, (on - origin).Unit);
                List<Intersection> fromTheSheet = [];
                List<Intersection> fromTheSphere = [];

                parametric.AddIntersections(ray, fromTheSheet);
                sphere.AddIntersections(ray, fromTheSphere);

                if (fromTheSheet.Count < fromTheSphere.Count)
                    missing++;
            }
        }

        Assert.AreEqual(0, missing,
            $"{missing} of 225 rays aimed through the joins between pieces lost a crossing");
    }

    /// <summary>
    /// This tests that a sheet is lit whichever way round its arithmetic was written.
    /// <para>
    /// A sheet's normal is its two slopes crossed, so reversing a sign in any one of the three
    /// expressions reverses it, and the author has no way of knowing which way it came out.  If the
    /// point a ray leaves from is nudged along that normal, then for half of all sheets it is nudged
    /// *behind* the sheet, and the shadow ray sets off from underneath the thing it is standing on
    /// and finds it in the way.  A fluted horn came out solid black for exactly this, and negating
    /// one <c>sin</c> lit it perfectly -- which is not a thing any scene should depend on.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestASheetIsLitWhicheverWayRoundItIsWritten()
    {
        foreach (int winding in new[] { 1, -1 })
        {
            // A ball, and the same ball with its Z reversed -- the same shape, wound the other way.
            Parametric parametric = Create(
                new BinaryMultiplyOperation(Call("sin", Named("v")), Call("cos", Named("u"))),
                Call("cos", Named("v")),
                new BinaryMultiplyOperation(
                    Number(winding),
                    new BinaryMultiplyOperation(Call("sin", Named("v")), Call("sin", Named("u")))));
            Ray ray = new (new Point(0.2, 0.1, -5), new Vector(0, 0, 1));
            List<Intersection> found = [];

            parametric.AddIntersections(ray, found);

            Intersection nearest = found.OrderBy(hit => hit.Distance).First();

            nearest.PrepareUsing(ray, found);

            Assert.IsTrue((nearest.LitPoint - nearest.Point).Dot(nearest.Eye) > 0,
                $"wound {(winding > 0 ? "one way" : "the other")}, the point a ray leaves from is " +
                "behind the sheet, so the sheet shadows itself and comes out black");
            Assert.IsTrue(nearest.Normal.Dot(nearest.Eye) > 0,
                "the normal used for shading must face whoever is looking");
        }
    }
}
