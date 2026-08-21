using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

/// <summary>
/// These tests cover the traversal a shadow query uses, which looks along only a stretch of the ray.
/// <para>
/// The whole of its licence to skip anything is that a shadow query throws away every crossing behind
/// the point it started from and every crossing at or past the light.  So the one thing worth testing
/// is that it returns <b>exactly</b> what an ordinary query would have returned once those are thrown
/// away -- no more, and above all no less.  Every test here is therefore differential against the
/// unbounded traversal rather than against a hand-written expectation.
/// </para>
/// <para>
/// The dangerous case has its own test, and finding a test that could actually see it took two tries.
/// A CSG surface works out what is solid by walking every crossing of both its halves in order, and
/// it needs the ones behind the ray's origin to know whether the ray began inside.  Ruling out a CSG
/// whose whole box lies behind the point is sound, since everything it could report would have been
/// discarded anyway; truncating the list <em>inside</em> one is not.  But a difference of two cubes
/// cannot tell the difference: a closed solid behind the ray gives an even number of crossings, so the
/// toggles cancel.  It takes an <em>open</em> surface for one half -- a single triangle, one crossing,
/// no pairing -- before the fault shows at all.
/// </para>
/// </summary>
[TestClass]
public class TestBoundedTraversal
{
    private const int Rays = 2000;

    /// <summary>
    /// This method asks the ordinary way and then discards what a shadow query would discard, which is
    /// the answer the bounded traversal has to match.
    /// </summary>
    private static List<(double, Surface)> Expected(Surface surface, Ray ray, double maxDistance)
    {
        List<Intersection> found = [];

        surface.Intersect(ray, found);

        return found
            .Where(crossing => crossing.Distance >= 0 && crossing.Distance < maxDistance)
            .OrderBy(crossing => crossing.Distance)
            .ThenBy(crossing => crossing.Surface.Name)
            .Select(crossing => (crossing.Distance, crossing.Surface))
            .ToList();
    }

    private static List<(double, Surface)> Bounded(Surface surface, Ray ray, double maxDistance)
    {
        List<Intersection> found = [];

        surface.IntersectWithin(ray, found, maxDistance);

        return found
            .Where(crossing => crossing.Distance >= 0 && crossing.Distance < maxDistance)
            .OrderBy(crossing => crossing.Distance)
            .ThenBy(crossing => crossing.Surface.Name)
            .Select(crossing => (crossing.Distance, crossing.Surface))
            .ToList();
    }

    /// <summary>
    /// This method insists the two agree over a great many rays and a range of bounds.
    /// </summary>
    private static void AssertBoundedAgrees(Surface surface, double from, double[] bounds)
    {
        surface.PrepareForRendering();

        int kept = 0;

        for (int index = 0; index < Rays; index++)
        {
            Ray ray = RayNumber(index, from);

            foreach (double bound in bounds)
            {
                List<(double, Surface)> expected = Expected(surface, ray, bound);
                List<(double, Surface)> bounded = Bounded(surface, ray, bound);

                kept += expected.Count;

                Assert.AreEqual(expected.Count, bounded.Count,
                    $"ray {index} at bound {bound}: {expected.Count} crossings the ordinary way, " +
                    $"{bounded.Count} the bounded way");

                for (int at = 0; at < expected.Count; at++)
                {
                    Assert.AreEqual(expected[at].Item1, bounded[at].Item1, 1e-12,
                        $"ray {index} at bound {bound}, crossing {at}");
                    Assert.AreSame(expected[at].Item2, bounded[at].Item2,
                        $"ray {index} at bound {bound}, crossing {at} came from another surface");
                }
            }
        }

        Assert.IsTrue(kept > Rays / 4,
            $"only {kept} crossings were kept -- these rays are not reaching the geometry");
    }

    private static Group RowOfBalls(int count)
    {
        Group group = new ();

        for (int index = 0; index < count; index++)
        {
            group.Add(new Sphere
            {
                Name = $"ball{index:D3}",
                Transform = Transforms.Translate(index * 1.4 - count * 0.7, 0, 0) *
                            Transforms.Scale(0.5)
            });
        }

        return group;
    }

    [TestMethod]
    public void TestABoundedQueryFindsWhatAnOrdinaryOneWouldHaveKept()
    {
        AssertBoundedAgrees(RowOfBalls(24), 12, [2, 6, 12, 30, double.PositiveInfinity]);
    }

    [TestMethod]
    public void TestABoundedQueryAgreesForAGroupTooSmallToArrange()
    {
        Group group = RowOfBalls(3);

        AssertBoundedAgrees(group, 8, [3, 9, double.PositiveInfinity]);
    }

    [TestMethod]
    public void TestACsgSurfaceIsNeverTruncatedInside()
    {
        // Broad coverage of a difference seen from every direction, at bounds that fall inside the
        // shape as well as outside it.
        //
        // It is worth saying what this does *not* prove, since it looks as though it should: leaking
        // the bound into the CSG's own reckoning leaves this test perfectly green.  A closed solid
        // lying wholly behind the ray gives an even number of crossings, so the filter's toggles
        // cancel and dropping them changes nothing.  The test below, with an open surface for the
        // subtracted half, is the one that bites.
        Group group = new ();

        for (int index = 0; index < 10; index++)
        {
            group.Add(new CsgSurface
            {
                Name = $"csg{index:D2}",
                Operation = CsgOperation.Difference,
                Left = new Cube { Name = $"outer{index:D2}" },
                Right = new Cube
                {
                    Name = $"inner{index:D2}",
                    Transform = Transforms.Scale(0.7) * Transforms.Translate(0, 0.4, 0)
                },
                Transform = Transforms.Translate(index * 2.4 - 12, 0, 0)
            });
        }

        AssertBoundedAgrees(group, 14, [4, 10, 16, 40, double.PositiveInfinity]);
    }

    [TestMethod]
    public void TestACsgHalfMadeOfAnOpenSurfaceIsNotTruncatedEither()
    {
        // The case that actually catches a bound leaking into a CSG's own reckoning, and it took some
        // working out.  The obvious test -- a difference of two cubes -- passes even with the leak,
        // because a closed solid lying wholly behind the ray gives an even number of crossings, so the
        // filter's inside/outside toggles cancel and dropping them changes nothing.
        //
        // An *open* surface does not have that courtesy.  One triangle behind the origin is one
        // crossing, and dropping it flips the filter's idea of whether the ray is inside the
        // subtracted half for everything that follows.  Here the outer cube's far wall is correctly
        // removed by the difference; leak the bound and it comes back.
        Group group = new ();
        Group subtracted = new ();

        subtracted.Add(new Triangle
        {
            Name = "leaf",
            Point1 = new Point(-0.5, -0.8, -0.8),
            Point2 = new Point(-0.5, 0.8, -0.8),
            Point3 = new Point(-0.5, 0, 0.8)
        });

        group.Add(new CsgSurface
        {
            Name = "csg",
            Operation = CsgOperation.Difference,
            Left = new Cube { Name = "shell" },
            Right = subtracted
        });

        // Enough other things to be worth arranging into a hierarchy, so the bound really does travel
        // through one on its way to the CSG.
        for (int index = 0; index < 10; index++)
        {
            group.Add(new Sphere
            {
                Name = $"far{index:D2}",
                Transform = Transforms.Translate(index * 3 + 8, 0, 0) * Transforms.Scale(0.4)
            });
        }

        group.PrepareForRendering();

        // Starting inside the shell, past the triangle, looking out through the far wall.
        Ray ray = new (new Point(0, 0, 0), new Vector(1, 0, 0));
        List<Intersection> ordinary = [];
        List<Intersection> bounded = [];

        group.Intersect(ray, ordinary);
        group.IntersectWithin(ray, bounded, 3);

        List<double> expected = ordinary
            .Where(crossing => crossing.Distance >= 0 && crossing.Distance < 3)
            .Select(crossing => crossing.Distance)
            .OrderBy(distance => distance)
            .ToList();
        List<double> actual = bounded
            .Where(crossing => crossing.Distance >= 0 && crossing.Distance < 3)
            .Select(crossing => crossing.Distance)
            .OrderBy(distance => distance)
            .ToList();

        CollectionAssert.AreEqual(expected, actual,
            "the bounded query disagreed with the ordinary one about what the difference leaves solid");
    }

    [TestMethod]
    public void TestAnUnboundableChildIsStillAlwaysAsked()
    {
        // A plane has no box, so no bound can rule it out and it must be asked about every ray.  About
        // half of what a real shadow ray throws away comes from surfaces like this one.
        Group group = RowOfBalls(12);

        group.Add(new Plane { Name = "floor", Transform = Transforms.Translate(0, -3, 0) });

        AssertBoundedAgrees(group, 12, [5, 15, double.PositiveInfinity]);
    }

    [TestMethod]
    public void TestTheOrdinaryTraversalStillKeepsWhatIsBehindTheRay()
    {
        // The regression that would matter most and show least.  The bounded traversal drops crossings
        // behind the origin, and for a while it shared a code path with the ordinary one, told apart
        // only by an infinite bound -- which would have dropped them there too, silently, wherever a
        // light happened to be infinitely far off.
        Group group = RowOfBalls(9);

        group.PrepareForRendering();

        Ray ray = new (new Point(0, 0, 0), new Vector(1, 0, 0));
        List<Intersection> found = [];

        group.Intersect(ray, found);

        Assert.IsTrue(found.Any(crossing => crossing.Distance < 0),
            "the ordinary traversal must still report crossings behind the ray's origin");
    }

    [TestMethod]
    public void TestABoundOfInfinityStillRulesOutWhatIsBehind()
    {
        // A sky light has no near side, so its distance really is infinite.  That must not stop the
        // bounded traversal ruling out what lies behind the point, which was the flaw in the first
        // version of this: it tied the two together and so bought nothing at all on any scene lit by
        // a sky.
        Group group = RowOfBalls(9);

        group.PrepareForRendering();

        Ray ray = new (new Point(0, 0, 0), new Vector(1, 0, 0));
        List<Intersection> bounded = [];

        group.IntersectWithin(ray, bounded, double.PositiveInfinity);

        Assert.IsFalse(bounded.Any(crossing => crossing.Distance < -0.001),
            "a bounded query should not be reporting crossings from behind the point");
        Assert.IsTrue(bounded.Any(crossing => crossing.Distance > 0),
            "and it should still be finding what lies ahead");
    }

    private static Ray RayNumber(int index, double from)
    {
        double around = ScatterGenerator.At(index, 1) * Math.PI * 2;
        double up = Math.Acos(2 * ScatterGenerator.At(index, 2) - 1);
        Point origin = new (
            from * Math.Sin(up) * Math.Cos(around),
            from * Math.Cos(up),
            from * Math.Sin(up) * Math.Sin(around));
        Point at = new (
            (ScatterGenerator.At(index, 3) - 0.5) * from * 0.8,
            (ScatterGenerator.At(index, 4) - 0.5) * from * 0.8,
            (ScatterGenerator.At(index, 5) - 0.5) * from * 0.8);

        return new Ray(origin, (at - origin).Unit);
    }
}
