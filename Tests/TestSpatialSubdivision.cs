using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.General;

namespace Tests;

/// <summary>
/// These tests cover the tree of nested boxes a group builds over its children.
/// <para>
/// There is only one thing worth testing about an acceleration structure, and it is not that it is
/// fast: it is that it finds <b>exactly</b> what the slow way would have found.  A hierarchy that
/// drops a box reports fewer crossings than there are, and the damage is quiet -- a surface thins out
/// or vanishes in patches, at some angles and not others, and a picture that looks plausible is
/// wrong.  So every test here is differential: the group's answer is compared against walking its
/// children one by one, over thousands of rays from every direction.
/// </para>
/// </summary>
[TestClass]
public class TestSpatialSubdivision
{
    private const int Rays = 3000;

    /// <summary>
    /// This method asks every child of the group directly, which is what the group itself used to do
    /// and what its hierarchy must agree with to the last crossing.
    /// </summary>
    private static List<(double Distance, Surface Surface)> WalkedAnswer(Group group, Ray ray)
    {
        List<Intersection> found = [];

        foreach (Surface surface in group.Surfaces)
            surface.Intersect(ray, found);

        found.Sort();

        return Described(found);
    }

    private static List<(double Distance, Surface Surface)> GroupAnswer(Group group, Ray ray)
    {
        List<Intersection> found = [];

        group.AddIntersections(ray, found);

        return Described(found);
    }

    /// <summary>
    /// The surface matters as much as the distance here.  Comparing distances alone would call two
    /// different surfaces met at the same distance a match, which is the very case that used to come
    /// out differently depending on how the geometry was walked.
    /// </summary>
    private static List<(double Distance, Surface Surface)> Described(List<Intersection> found)
    {
        return found
            .Select(intersection => (intersection.Distance, intersection.Surface))
            .ToList();
    }

    /// <summary>
    /// This method insists the two answers agree for a great many rays.
    /// </summary>
    private static void AssertTheTreeFindsEverything(Group group, double from)
    {
        group.PrepareForRendering();

        int hits = 0;

        for (int index = 0; index < Rays; index++)
        {
            Ray ray = RayNumber(index, from);
            List<(double Distance, Surface Surface)> walked = WalkedAnswer(group, ray);
            List<(double Distance, Surface Surface)> tree = GroupAnswer(group, ray);

            hits += walked.Count;

            Assert.AreEqual(walked.Count, tree.Count,
                $"ray {index} found {walked.Count} crossings by walking and {tree.Count} by the tree");

            for (int at = 0; at < walked.Count; at++)
            {
                Assert.AreEqual(walked[at].Distance, tree[at].Distance, 1e-12,
                    $"ray {index}, crossing {at}");
                Assert.AreSame(walked[at].Surface, tree[at].Surface,
                    $"ray {index}, crossing {at} came from a different surface");
            }
        }

        // A differential test where the rays all miss agrees perfectly and proves nothing, so this
        // insists the geometry was actually being found.  A crossing for every fourth ray is a low
        // bar deliberately: the sparsest case here is three spheres, and what matters is that the
        // number is nowhere near zero, not that it is large.
        Assert.IsTrue(hits > Rays / 4,
            $"only {hits} crossings over {Rays} rays -- this test is not reaching the geometry");
    }

    /// <summary>
    /// Builds a grid of spheres, which is the shape a height field has and the shape most likely to
    /// expose a tree that divides badly.
    /// </summary>
    private static Group GridOfBalls(int across)
    {
        Group group = new ();

        // Centered on the origin, because the rays below are aimed at a spread around the origin.  An
        // uncentered grid is not wrong so much as badly aimed at, and the tests still agreed ray for
        // ray -- they failed on the guard that says the rays must actually be finding something.
        double middle = (across - 1) * 1.5 / 2;

        for (int x = 0; x < across; x++)
        for (int y = 0; y < across; y++)
        for (int z = 0; z < across; z++)
        {
            group.Add(new Sphere
            {
                Transform = Transforms.Translate(
                    x * 1.5 - middle, y * 1.5 - middle, z * 1.5 - middle) * Transforms.Scale(0.5)
            });
        }

        return group;
    }

    [TestMethod]
    public void TestATreeFindsWhatWalkingFinds()
    {
        // 125 spheres, comfortably past the count at which a tree gets built.
        AssertTheTreeFindsEverything(GridOfBalls(5), 14);
    }

    [TestMethod]
    public void TestAGroupTooSmallToArrangeStillAnswers()
    {
        // Below the threshold no tree is built at all, and the walk has to keep working.
        Group group = new ();

        group.Add(new Sphere());
        group.Add(new Sphere { Transform = Transforms.Translate(2, 0, 0) });
        group.Add(new Sphere { Transform = Transforms.Translate(-2, 0, 0) });

        AssertTheTreeFindsEverything(group, 8);
    }

    [TestMethod]
    public void TestCrossingsBehindTheRayAreStillReported()
    {
        // The engine invariant a hierarchy is most likely to break.  The usual way to make one of
        // these fast is to stop opening boxes once something nearer has been found, which throws away
        // every crossing behind the origin -- and CSG counts crossings to decide what is inside it,
        // while refraction needs the ones behind to know it is leaving rather than entering.  So the
        // ray here starts in the middle of the grid, where half of everything is behind it.
        Group group = GridOfBalls(5);

        group.PrepareForRendering();

        Ray ray = new (new Point(3, 3, 3), new Vector(1, 0, 0));
        List<(double Distance, Surface Surface)> walked = WalkedAnswer(group, ray);
        List<(double Distance, Surface Surface)> tree = GroupAnswer(group, ray);

        CollectionAssert.AreEqual(walked, tree);
        Assert.IsTrue(walked.Any(crossing => crossing.Distance < 0),
            "this ray should have had something behind it to find");
        Assert.IsTrue(tree.Any(crossing => crossing.Distance < 0),
            "the tree dropped every crossing behind the ray's origin");
    }

    [TestMethod]
    public void TestAChildThatCannotBeBoxedIsStillAlwaysTested()
    {
        // A plane goes on forever and has no place in a tree of boxes, so it is kept aside and handed
        // every ray.  Left in the tree it would need a box holding everything, which would defeat the
        // whole arrangement; left out altogether it would simply stop being drawn.
        Group group = GridOfBalls(4);

        group.Add(new Plane { Transform = Transforms.Translate(0, -3, 0) });

        AssertTheTreeFindsEverything(group, 12);
    }

    [TestMethod]
    public void TestAnEmptyGroupAmongTheChildrenChangesNothing()
    {
        // An empty group occupies no region, so it belongs in neither list.  This is the case that
        // used to rob every group above it of its box.
        Group group = GridOfBalls(4);

        group.Add(new Group());
        group.Add(new Group());

        AssertTheTreeFindsEverything(group, 12);
    }

    [TestMethod]
    public void TestChildrenPiledAtOneSpotDoNotStopTheTreeDividing()
    {
        // Every center at the same coordinate gives a split with nothing on one side of it.  Left
        // alone that recurses forever on the same run; the build halves by count instead.
        Group group = new ();

        for (int index = 0; index < 40; index++)
            group.Add(new Sphere { Transform = Transforms.Scale(1 + index * 0.05) });

        AssertTheTreeFindsEverything(group, 10);
    }

    [TestMethod]
    public void TestNestedGroupsAgreeToo()
    {
        // A stand of trees is groups within groups within groups, and each level arranges its own
        // children.  A fault in how the levels compose would not show in a single flat group.
        Group outer = new ();

        for (int index = 0; index < 12; index++)
        {
            Group inner = new ();

            for (int at = 0; at < 12; at++)
            {
                inner.Add(new Sphere
                {
                    Transform = Transforms.Translate(at * 0.8, 0, 0) * Transforms.Scale(0.3)
                });
            }

            inner.Transform = Transforms.Translate(-4, index * 0.9 - 5, index * 0.6 - 3);

            outer.Add(inner);
        }

        AssertTheTreeFindsEverything(outer, 16);
    }

    /// <summary>
    /// Rays from all over a sphere around the geometry, aimed across it rather than at its middle, so
    /// that plenty of them graze it.  Worked out from a fixed seed, so a failure can be looked into
    /// rather than merely seen once.
    /// </summary>
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
