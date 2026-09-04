using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace Tests;

/// <summary>
/// These tests cover the named quadrics — the surfaces whose equation is of the second degree, so a
/// ray meets them where a quadratic says and nowhere else.
/// <para>
/// The check that matters is not "how many crossings" but **"is the crossing on the surface"**: every
/// hit point is put back into the surface's own equation, which cannot be satisfied by an answer that
/// merely looks plausible.  Counting alone would pass a solver that found the right number of wrong
/// places.
/// </para>
/// </summary>
[TestClass]
public class TestQuadricSurfaces
{
    private const int Rays = 500;

    /// <summary>
    /// This tests that every crossing of a paraboloid lies on the paraboloid.
    /// </summary>
    [TestMethod]
    public void TestEveryCrossingOfABowlLiesOnIt()
    {
        Paraboloid bowl = new () { MinimumY = 0, MaximumY = 4, Closed = false };
        Random random = new (20260904);
        int found = 0;

        bowl.PrepareForRendering();

        for (int index = 0; index < Rays; index++)
        {
            // Aimed at a point inside the bowl's own box rather than thrown in its general
            // direction, so that most rays actually meet it and the test says something.
            Point origin = new (
                random.NextDouble() * 12 - 6, random.NextDouble() * 12 - 4, -8);
            Point at = new (
                random.NextDouble() * 4 - 2, random.NextDouble() * 4, random.NextDouble() * 4 - 2);
            Vector direction = (at - origin).Unit;
            List<Intersection> hits = [];

            bowl.AddIntersections(new Ray(origin, direction), hits);

            foreach (Intersection hit in hits)
            {
                Point where = new Ray(origin, direction).At(hit.Distance);

                Assert.IsTrue(
                    Math.Abs(where.X * where.X + where.Z * where.Z - where.Y) < 1e-9,
                    $"a crossing at ({where.X:F4}, {where.Y:F4}, {where.Z:F4}) is not on x² + z² = y");
                found++;
            }
        }

        Assert.IsTrue(found > 300, $"only {found} crossings in {Rays} rays; the test proves little");
    }

    /// <summary>
    /// This tests the case that separates a bowl from a cylinder: a ray straight down the axis.
    /// <para>
    /// A cylinder gives up when the squared term vanishes, because a ray parallel to its wall never
    /// meets it.  A paraboloid is closed at the nose, so the same ray meets it once and the equation
    /// left is a straight line.  Treating it as a miss puts a hole through the middle of every dish
    /// seen face on — which is exactly where a viewer looks.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestARayDownTheAxisMeetsTheNose()
    {
        Paraboloid bowl = new () { MinimumY = 0, MaximumY = 4, Closed = false };

        bowl.PrepareForRendering();

        Ray ray = new (new Point(0, 9, 0), new Vector(0, -1, 0));
        List<Intersection> hits = [];

        bowl.AddIntersections(ray, hits);

        Assert.AreEqual(1, hits.Count, "a ray down the axis should meet the nose exactly once");
        Assert.IsTrue(Math.Abs(hits[0].Distance - 9) < 1e-9,
            $"the nose is at the origin, nine along the ray, not at {hits[0].Distance}");

        // And a little off the axis, where the arithmetic is nearly but not quite degenerate.
        foreach (double lean in new[] { 1e-3, 1e-5, 1e-7 })
        {
            List<Intersection> nearly = [];

            bowl.AddIntersections(
                new Ray(new Point(0, 9, 0), new Vector(lean, -1, 0)), nearly);

            Assert.IsTrue(nearly.Count > 0,
                $"a ray leaning {lean} off the axis lost the nose altogether");
        }
    }

    /// <summary>
    /// This tests that the normal is the slope of the bowl's own equation, checked against the
    /// surface rather than against numbers worked out by hand.
    /// </summary>
    [TestMethod]
    public void TestTheNormalIsTheSlopeOfTheEquation()
    {
        Paraboloid bowl = new () { MinimumY = 0, MaximumY = 4, Closed = false };

        bowl.PrepareForRendering();

        foreach ((double x, double z) in new[] { (0.0, 0.0), (1.0, 0.0), (0.0, -1.5), (0.8, 1.2) })
        {
            Point where = new (x, x * x + z * z, z);
            Vector normal = bowl.SurfaceNormalAt(where, null);
            Vector expected = new Vector(2 * x, -1, 2 * z).Unit;

            Assert.IsTrue(normal.Unit.Matches(expected),
                $"at ({x}, {z}) the normal is {normal.Unit}, not {expected}");
        }
    }

    /// <summary>
    /// This tests that every crossing of a hyperboloid lies on the hyperboloid.
    /// </summary>
    [TestMethod]
    public void TestEveryCrossingOfAWaistedSurfaceLiesOnIt()
    {
        Hyperboloid waisted = new () { MinimumY = -2, MaximumY = 2, Closed = false };
        Random random = new (20260904);
        int found = 0;

        waisted.PrepareForRendering();

        for (int index = 0; index < Rays; index++)
        {
            Point origin = new (
                random.NextDouble() * 12 - 6, random.NextDouble() * 10 - 5, -8);
            Point at = new (
                random.NextDouble() * 4 - 2, random.NextDouble() * 4 - 2,
                random.NextDouble() * 4 - 2);
            Vector direction = (at - origin).Unit;
            List<Intersection> hits = [];

            waisted.AddIntersections(new Ray(origin, direction), hits);

            foreach (Intersection hit in hits)
            {
                Point where = new Ray(origin, direction).At(hit.Distance);

                Assert.IsTrue(
                    Math.Abs(where.X * where.X + where.Z * where.Z - where.Y * where.Y - 1) < 1e-9,
                    $"a crossing at ({where.X:F4}, {where.Y:F4}, {where.Z:F4}) is not on " +
                    "x² + z² - y² = 1");
                found++;
            }
        }

        Assert.IsTrue(found > 300, $"only {found} crossings in {Rays} rays; the test proves little");
    }

    /// <summary>
    /// This tests the hyperboloid's own degenerate ray: one running along an asymptote, at
    /// forty-five degrees to the axis, where the squared term falls out because the ray widens as
    /// fast as the surface does.  It still meets the surface once.
    /// </summary>
    [TestMethod]
    public void TestARayAlongAnAsymptoteMeetsTheSurfaceOnce()
    {
        Hyperboloid waisted = new () { MinimumY = -4, MaximumY = 4, Closed = false };

        waisted.PrepareForRendering();

        // Parallel to the asymptote x = y, but not *on* it -- and the difference is the whole
        // point.  A ray lying along the asymptotic line itself has both its squared and its linear
        // term vanish and genuinely misses, because that is what an asymptote is: the surface
        // approaches that line forever and never reaches it.  Offset from it, the linear term
        // survives and there is one crossing.
        Ray ray = new (new Point(0, -4, 0), new Vector(1, 1, 0).Unit);
        List<Intersection> hits = [];

        waisted.AddIntersections(ray, hits);

        Assert.AreEqual(1, hits.Count,
            "a ray along an asymptote should meet the surface exactly once");

        Point where = ray.At(hits[0].Distance);

        Assert.IsTrue(
            Math.Abs(where.X * where.X + where.Z * where.Z - where.Y * where.Y - 1) < 1e-9,
            $"the asymptotic crossing at ({where.X:F4}, {where.Y:F4}, {where.Z:F4}) is not on it");

        // And leaning slightly off the asymptote must not lose it either.
        foreach (double lean in new[] { 1e-3, 1e-6 })
        {
            List<Intersection> nearly = [];

            waisted.AddIntersections(
                new Ray(new Point(0, -4, 0), new Vector(1 + lean, 1, 0).Unit), nearly);

            Assert.IsTrue(nearly.Count > 0,
                $"a ray leaning {lean} off the asymptote lost the surface");
        }
    }

    /// <summary>
    /// This tests that every crossing of a saddle lies on the saddle and inside the rectangle it is
    /// cut to.
    /// </summary>
    [TestMethod]
    public void TestEveryCrossingOfASaddleLiesOnIt()
    {
        Saddle saddle = new () { Width = 3, Depth = 3 };
        Random random = new (20260904);
        int found = 0;

        saddle.PrepareForRendering();

        for (int index = 0; index < Rays; index++)
        {
            Point origin = new (
                random.NextDouble() * 8 - 4, random.NextDouble() * 8 - 4, -6);
            Point at = new (
                random.NextDouble() * 3 - 1.5, random.NextDouble() * 4 - 2,
                random.NextDouble() * 3 - 1.5);
            Vector direction = (at - origin).Unit;
            List<Intersection> hits = [];

            saddle.AddIntersections(new Ray(origin, direction), hits);

            foreach (Intersection hit in hits)
            {
                Point where = new Ray(origin, direction).At(hit.Distance);

                Assert.IsTrue(
                    Math.Abs(where.X * where.X - where.Z * where.Z - where.Y) < 1e-9,
                    $"a crossing at ({where.X:F4}, {where.Y:F4}, {where.Z:F4}) is not on y = x² - z²");
                Assert.IsTrue(Math.Abs(where.X) <= 1.5 + 1e-9 && Math.Abs(where.Z) <= 1.5 + 1e-9,
                    $"a crossing at ({where.X:F4}, {where.Z:F4}) is outside the rectangle");
                found++;
            }
        }

        Assert.IsTrue(found > 200, $"only {found} crossings in {Rays} rays; the test proves little");
    }

    /// <summary>
    /// This tests the saddle's own degenerate ray: one at forty-five degrees in the X/Z plane, along
    /// a line the surface is ruled by, where the rise along X cancels the fall along Z exactly.  It
    /// meets the sheet once; losing it would draw two blank diagonals across every saddle.
    /// </summary>
    [TestMethod]
    public void TestARayAlongARulingMeetsTheSaddleOnce()
    {
        Saddle saddle = new () { Width = 4, Depth = 4 };

        saddle.PrepareForRendering();

        // One ray from each of the two families of rulings.  Both origins are chosen so the
        // crossing lands well inside the rectangle: a ruling ray that leaves the sheet's extent is
        // rejected for being outside it, which says nothing about the degenerate arithmetic.
        foreach ((Point origin, Vector direction) in new[]
        {
            (new Point(-1.5, -2, 0.5), new Vector(1, 1, 1).Unit),
            (new Point(-1.5, -2, -0.5), new Vector(1, 1, -1).Unit)
        })
        {
            Ray ray = new (origin, direction);
            List<Intersection> hits = [];

            saddle.AddIntersections(ray, hits);

            Assert.AreEqual(1, hits.Count,
                $"a ray along a ruling ({direction}) should meet the sheet exactly once");

            Point where = ray.At(hits[0].Distance);

            Assert.IsTrue(Math.Abs(where.X * where.X - where.Z * where.Z - where.Y) < 1e-9,
                $"the ruled crossing at ({where.X:F4}, {where.Y:F4}, {where.Z:F4}) is not on it");
        }
    }

    /// <summary>
    /// This tests the general quadric against a surface that already exists: a sphere written out as
    /// <c>x² + y² + z² - 1 = 0</c> must be crossed exactly where <see cref="Sphere"/> is crossed.
    /// <para>
    /// Holding one closed form to another is the strongest check available here — the two share no
    /// code, so agreeing to a billionth over hundreds of rays is not something a wrong solver does.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestAQuadricSpellingOutASphereAgreesWithOne()
    {
        Quadric written = new () { A = 1, B = 1, C = 1, J = -1 };
        Sphere ball = new ();
        Random random = new (20260904);
        int compared = 0;

        written.PrepareForRendering();
        ball.PrepareForRendering();

        for (int index = 0; index < Rays; index++)
        {
            Point origin = new (
                random.NextDouble() * 6 - 3, random.NextDouble() * 6 - 3, -4);
            Point at = new (
                random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1,
                random.NextDouble() * 2 - 1);
            Ray ray = new (origin, (at - origin).Unit);
            List<Intersection> fromQuadric = [];
            List<Intersection> fromSphere = [];

            written.AddIntersections(ray, fromQuadric);
            ball.AddIntersections(ray, fromSphere);

            Assert.AreEqual(fromSphere.Count, fromQuadric.Count,
                "the sphere and the quadric spelling it disagree on how many crossings there are");

            foreach ((Intersection expected, Intersection actual) in fromSphere
                         .OrderBy(hit => hit.Distance)
                         .Zip(fromQuadric.OrderBy(hit => hit.Distance)))
            {
                Assert.IsTrue(Math.Abs(expected.Distance - actual.Distance) < 1e-9,
                    $"the sphere is crossed at {expected.Distance} and the quadric at " +
                    $"{actual.Distance}");
                compared++;
            }
        }

        Assert.IsTrue(compared > 300, $"only {compared} crossings compared; the test proves little");
    }

    /// <summary>
    /// This tests the case that earned the general quadric its place: the two-sheet hyperboloid,
    /// <c>x² + z² - y² = -1</c>, which the named form deliberately does not offer.
    /// </summary>
    [TestMethod]
    public void TestAQuadricGivesTheTwoSheetHyperboloid()
    {
        Quadric opposed = new () { A = 1, B = -1, C = 1, J = 1 };

        opposed.PrepareForRendering();

        // Straight down the axis, which threads both bowls: one nose each, at y = plus and minus one.
        Ray ray = new (new Point(0, 5, 0), new Vector(0, -1, 0));
        List<Intersection> hits = [];

        opposed.AddIntersections(ray, hits);

        Assert.AreEqual(2, hits.Count, "a ray down the axis should meet both sheets");

        foreach (Intersection hit in hits)
        {
            Point where = ray.At(hit.Distance);

            Assert.IsTrue(Math.Abs(Math.Abs(where.Y) - 1) < 1e-9,
                $"a sheet's nose should sit at y = ±1, not {where.Y}");
        }

        // And nothing at all between the sheets, which is what makes it two sheets.
        List<Intersection> across = [];

        opposed.AddIntersections(new Ray(new Point(-5, 0, 0), new Vector(1, 0, 0)), across);

        Assert.AreEqual(0, across.Count, "a ray through the waist should meet nothing; there is none");
    }

    /// <summary>
    /// This tests the engine's rule that a surface reports crossings behind the ray's origin as well
    /// as ahead of it, since a quadric is meant to stand in a CSG.
    /// </summary>
    [TestMethod]
    public void TestAQuadricReportsCrossingsBehindTheOrigin()
    {
        Quadric ball = new () { A = 1, B = 1, C = 1, J = -1 };

        ball.PrepareForRendering();

        List<Intersection> hits = [];

        ball.AddIntersections(new Ray(new Point(0, 0, 0), new Vector(1, 0, 0)), hits);

        Assert.AreEqual(2, hits.Count, "a ray from the middle crosses the surface twice");
        Assert.IsTrue(hits.Any(hit => hit.Distance < 0), "the crossing behind the origin is missing");
        Assert.IsTrue(hits.Any(hit => hit.Distance > 0), "the crossing ahead of the origin is missing");
    }
}
