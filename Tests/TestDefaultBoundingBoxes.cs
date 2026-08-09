using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

/// <summary>
/// These tests hold every surface's default bounding box to the one rule that matters: <b>the box must
/// contain the whole surface</b>.
/// <para>
/// The two ways of getting a box wrong are not equally bad, which is why the tests lean the way they
/// do.  A box that is too <i>large</i> costs a little speed and nothing else -- a ray tests it, gets in,
/// and finds nothing.  A box that is too <i>small</i> makes the surface vanish in patches, silently, in
/// whatever direction the box falls short, and that is the sort of fault that ships and is found a year
/// later in one picture at one angle.
/// </para>
/// <para>
/// So the test is not "does the box look right" but "is there a ray that finds this surface and misses
/// its box".  <see cref="Surface.Intersect"/> tests the box and then the surface;
/// <see cref="Surface.AddIntersections"/> tests the surface alone.  Thousands of rays from every
/// direction must get the same answer from both, and any ray that does not is a hole in the box.
/// </para>
/// </summary>
[TestClass]
public class TestDefaultBoundingBoxes
{
    /// <summary>
    /// How many rays each surface is asked about.  They are worked out from a fixed seed rather than
    /// taken at random, so a failure here can be looked into rather than merely seen once.
    /// </summary>
    private const int Rays = 4000;

    [TestMethod]
    public void TestASphereIsInsideItsBox()
    {
        AssertNothingEscapes(new Sphere(), 3);
    }

    [TestMethod]
    public void TestACubeIsInsideItsBox()
    {
        AssertNothingEscapes(new Cube(), 3);
    }

    [TestMethod]
    public void TestACylinderIsInsideItsBox()
    {
        AssertNothingEscapes(new Cylinder { MinimumY = -1, MaximumY = 1 }, 3);
        AssertNothingEscapes(new Cylinder { MinimumY = 0, MaximumY = 4 }, 6);
        AssertNothingEscapes(new Cylinder { MinimumY = -2.5, MaximumY = -0.5 }, 5);

        // Open at the ends is the same extent; only the caps are missing.
        AssertNothingEscapes(new Cylinder { MinimumY = 0, MaximumY = 3, Closed = false }, 5);
    }

    [TestMethod]
    public void TestAConicIsInsideItsBox()
    {
        // A conic is widest at whichever end lies further from the middle, which is the part of this
        // easiest to get wrong: a cone from 0 to 3 is three wide at the top and nothing at the bottom.
        AssertNothingEscapes(new Conic { MinimumY = -1, MaximumY = 1 }, 3);
        AssertNothingEscapes(new Conic { MinimumY = 0, MaximumY = 3 }, 6);
        AssertNothingEscapes(new Conic { MinimumY = -4, MaximumY = -1 }, 7);
        AssertNothingEscapes(new Conic { MinimumY = -3, MaximumY = 1, Closed = false }, 6);
    }

    [TestMethod]
    public void TestATorusIsInsideItsBox()
    {
        AssertNothingEscapes(new Torus { MajorRadius = 1, MinorRadius = 0.25 }, 3);
        AssertNothingEscapes(new Torus { MajorRadius = 2.5, MinorRadius = 0.9 }, 6);

        // A minor radius as large as the major one closes the hole in the middle.
        AssertNothingEscapes(new Torus { MajorRadius = 1, MinorRadius = 1 }, 4);
    }

    [TestMethod]
    public void TestAnEggIsInsideItsBox()
    {
        AssertNothingEscapes(new Egg { BottomRadius = 1, TopRadius = 0.6 }, 4);
        AssertNothingEscapes(new Egg { BottomRadius = 0.6, TopRadius = 1 }, 4);
        AssertNothingEscapes(new Egg { BottomRadius = 2, TopRadius = 3.5 }, 9);
    }

    [TestMethod]
    public void TestAnEndlessCylinderOrConicHasNoBox()
    {
        // There is no finite box that holds an endless surface, so the honest answer is none at all --
        // and a surface with no box is tested directly, which is right rather than merely safe.
        Cylinder endless = new () { MinimumY = double.NegativeInfinity, MaximumY = 1 };
        Conic opening = new () { MinimumY = -1, MaximumY = double.PositiveInfinity };

        endless.PrepareForRendering();
        opening.PrepareForRendering();

        Assert.IsNull(endless.BoundingBox);
        Assert.IsNull(opening.BoundingBox);
    }

    [TestMethod]
    public void TestAUnionIsInsideItsBox()
    {
        AssertNothingEscapes(Combined(CsgOperation.Union, Ball(), Box(1.2, 0, 0)), 4);
        AssertNothingEscapes(Combined(CsgOperation.Union, Ball(), Ball(0, 2.5, 0)), 6);
    }

    [TestMethod]
    public void TestAnIntersectionIsInsideItsBox()
    {
        AssertNothingEscapes(Combined(CsgOperation.Intersection, Ball(), Box(0.6, 0, 0)), 4);
        AssertNothingEscapes(Combined(CsgOperation.Intersection, Box(), Ball(0.5, 0.5, 0)), 4);
    }

    [TestMethod]
    public void TestADifferenceIsInsideItsBox()
    {
        AssertNothingEscapes(Combined(CsgOperation.Difference, Ball(), Box(0.8, 0.8, 0)), 4);
        AssertNothingEscapes(Combined(CsgOperation.Difference, Box(), Ball(1, 1, 1)), 4);
    }

    [TestMethod]
    public void TestWhatAnEndlessPartLeavesBounded()
    {
        // The reasoning worth having a test for.  An intersection cannot reach beyond either of its
        // parts, so one that can say where it is bounds the whole thing however endless the other is;
        // and a difference only ever takes material away, so the left one bounds it whatever the right
        // one does.  A union of an endless thing is genuinely endless and rightly says so.
        CsgSurface cutBall = Combined(CsgOperation.Intersection, Ball(), new Plane());
        CsgSurface carvedBall = Combined(CsgOperation.Difference, Ball(), new Plane());
        CsgSurface endless = Combined(CsgOperation.Union, Ball(), new Plane());

        cutBall.PrepareForRendering();
        carvedBall.PrepareForRendering();
        endless.PrepareForRendering();

        Assert.IsNotNull(cutBall.BoundingBox, "a sphere cut by a plane is still inside the sphere");
        Assert.IsNotNull(carvedBall.BoundingBox, "a sphere carved by a plane is still inside it");
        Assert.IsNull(endless.BoundingBox, "a union with a plane really does go on forever");

        // And they must still hold what they hold.
        AssertNothingEscapes(Combined(CsgOperation.Intersection, Ball(), new Plane()), 4);
        AssertNothingEscapes(Combined(CsgOperation.Difference, Ball(), new Plane()), 4);
    }

    [TestMethod]
    public void TestAGroupHoldingACombinationHasABoxOfItsOwn()
    {
        // The point of the whole thing: a box on the parts gives every group above them one too, and a
        // combination that could not say where it was used to stop that at the first CSG it met.
        Group group = new ();

        group.Add(Combined(CsgOperation.Difference, Ball(), Box(0.5, 0.5, 0.5)));
        group.Add(Ball(3, 0, 0));
        group.PrepareForRendering();

        Assert.IsNotNull(group.BoundingBox);
        AssertNothingEscapes(group, 6);
    }

    private static Sphere Ball(double x = 0, double y = 0, double z = 0)
    {
        Sphere ball = new ();

        if (x != 0 || y != 0 || z != 0)
            ball.Transform = Transforms.Translate(x, y, z);

        return ball;
    }

    private static Cube Box(double x = 0, double y = 0, double z = 0)
    {
        Cube box = new ();

        if (x != 0 || y != 0 || z != 0)
            box.Transform = Transforms.Translate(x, y, z);

        return box;
    }

    private static CsgSurface Combined(CsgOperation operation, Surface left, Surface right)
    {
        return new CsgSurface { Operation = operation, Left = left, Right = right };
    }

    /// <summary>
    /// This method fires a great many rays at a surface from every direction and insists that testing
    /// the box first never loses one.
    /// </summary>
    /// <param name="surface">The surface to try.</param>
    /// <param name="from">How far out to stand while aiming at it.</param>
    private static void AssertNothingEscapes(Surface surface, double from)
    {
        surface.PrepareForRendering();

        Assert.IsNotNull(surface.BoundingBox, "this surface should have worked out a box for itself");

        for (int index = 0; index < Rays; index++)
            AssertTheBoxKeepsIt(surface, RayNumber(index, from), $"ray {index}");

        // And a sweep of rays running straight along each axis, which is what actually catches a box
        // that falls a little short.  A ray thrown at a surface from anywhere usually passes through
        // the middle of it as well, so a box short by a tenth still stops that ray -- it is the ray
        // that only just grazes the far edge which gets lost, and those have to be aimed at on purpose
        // rather than waited for.  A shortfall of a tenth went unnoticed until this was added.
        const int Across = 70;

        for (int axis = 0; axis < 3; axis++)
        {
            for (int down = 0; down < Across; down++)
            {
                for (int over = 0; over < Across; over++)
                {
                    double first = (down * 2.0 / (Across - 1) - 1) * from;
                    double second = (over * 2.0 / (Across - 1) - 1) * from;
                    double far = from * 3;
                    (Point origin, Vector direction) = axis switch
                    {
                        0 => (new Point(-far, first, second), new Vector(1, 0, 0)),
                        1 => (new Point(first, -far, second), new Vector(0, 1, 0)),
                        _ => (new Point(first, second, -far), new Vector(0, 0, 1))
                    };

                    AssertTheBoxKeepsIt(
                        surface, new Ray(origin, direction), $"the sweep along axis {axis}");
                }
            }
        }
    }

    /// <summary>
    /// This method insists that one ray finds the surface the same number of times whether it is asked
    /// through the box or straight.
    /// </summary>
    /// <param name="surface">The surface to try.</param>
    /// <param name="ray">The ray to try it with.</param>
    /// <param name="which">What to call the ray if it goes wrong.</param>
    private static void AssertTheBoxKeepsIt(Surface surface, Ray ray, string which)
    {
        List<Intersection> throughTheBox = [];
        List<Intersection> straightAtIt = [];

        surface.Intersect(ray, throughTheBox);
        surface.AddIntersections(ray, straightAtIt);

        Assert.AreEqual(straightAtIt.Count, throughTheBox.Count,
            $"{which}, from {ray.Origin} toward {ray.Direction}, found the surface " +
            $"{straightAtIt.Count} times but its box turned the ray away");
    }

    /// <summary>
    /// This method works out one ray of the spread: a point somewhere on a sphere around the surface,
    /// aimed at a point somewhere near it.  Aiming at a spread rather than at the middle is what
    /// produces the grazing rays, which are where a box that is a little too small gives itself away.
    /// </summary>
    /// <param name="index">Which ray of the spread this is.</param>
    /// <param name="from">The radius of the sphere to stand on.</param>
    /// <returns>The ray.</returns>
    private static Ray RayNumber(int index, double from)
    {
        double around = ScatterGenerator.At(index, 1) * Math.PI * 2;
        double up = Math.Acos(2 * ScatterGenerator.At(index, 2) - 1);
        Point origin = new (
            from * Math.Sin(up) * Math.Cos(around),
            from * Math.Cos(up),
            from * Math.Sin(up) * Math.Sin(around));

        // Aimed a little wide of the middle, over a spread wider than the surface itself, so that
        // plenty of these rays pass close by rather than straight through.
        Point at = new (
            (ScatterGenerator.At(index, 3) - 0.5) * from * 2.4,
            (ScatterGenerator.At(index, 4) - 0.5) * from * 2.4,
            (ScatterGenerator.At(index, 5) - 0.5) * from * 2.4);

        return new Ray(origin, (at - origin).Unit);
    }
}
