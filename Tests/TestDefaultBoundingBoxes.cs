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

    /// <summary>
    /// A superellipsoid lies inside the unit box whatever its two exponents are, since every term of
    /// its function is positive and so none of them may exceed one.  The exponents are the thing to
    /// vary here rather than the size: they are what changes the shape, and a box that held only for
    /// the round ones would be no box at all.
    /// </summary>
    /// <summary>
    /// A bowl's radius is the square root of its height, so its box is widest at the top and its
    /// bottom is the narrow end -- the opposite way round from a conic, which is the easy thing to
    /// get backwards here.
    /// </summary>
    [TestMethod]
    public void TestAParaboloidIsInsideItsBox()
    {
        AssertNothingEscapes(new Paraboloid { MinimumY = 0, MaximumY = 1 }, 3);
        AssertNothingEscapes(new Paraboloid { MinimumY = 0, MaximumY = 4 }, 6);
        AssertNothingEscapes(new Paraboloid { MinimumY = 1, MaximumY = 3 }, 5);
        AssertNothingEscapes(new Paraboloid { MinimumY = 0, MaximumY = 2, Closed = false }, 4);
    }

    /// <summary>
    /// A waisted surface is widest at whichever end lies further from its waist, which is the same
    /// shape of mistake a conic offers and is worth asking about the same way.
    /// </summary>
    [TestMethod]
    public void TestAHyperboloidIsInsideItsBox()
    {
        AssertNothingEscapes(new Hyperboloid { MinimumY = -1, MaximumY = 1 }, 4);
        AssertNothingEscapes(new Hyperboloid { MinimumY = -3, MaximumY = 3 }, 8);
        // Wholly to one side of the waist, where the widest end is not the further-from-origin one
        // by accident but by arithmetic.
        AssertNothingEscapes(new Hyperboloid { MinimumY = 1, MaximumY = 2.5 }, 6);
        AssertNothingEscapes(new Hyperboloid { MinimumY = -2, MaximumY = 0, Closed = false }, 6);
    }

    /// <summary>
    /// A saddle rises along X and falls along Z, so its box is not symmetric about nought in Y: the
    /// top comes from its width and the bottom from its depth, and a box that used one for both
    /// would clip a corner off whichever is larger.
    /// </summary>
    [TestMethod]
    public void TestASaddleIsInsideItsBox()
    {
        AssertNothingEscapes(new Saddle { Width = 2, Depth = 2 }, 4);
        AssertNothingEscapes(new Saddle { Width = 4, Depth = 1 }, 6);
        AssertNothingEscapes(new Saddle { Width = 1, Depth = 4 }, 6);
    }

    [TestMethod]
    public void TestASuperellipsoidIsInsideItsBox()
    {
        // Nearly a box, nearly a sphere, and the pinched shapes either side of both.
        foreach ((double east, double north) in new[]
        {
            (1.0, 1.0), (0.2, 0.2), (1.8, 1.8), (0.25, 1.75), (1.75, 0.25), (0.4, 0.35)
        })
        {
            AssertNothingEscapes(
                new Superellipsoid { EastWest = east, NorthSouth = north }, 3);
        }
    }

    /// <summary>
    /// A disc's box turns on which way it faces, so the facings that matter are the ones along an
    /// axis -- where the box is flat and has no room to spare -- and the ones lying awkwardly between
    /// two.  An inner radius takes nothing away from the extent.
    /// </summary>
    [TestMethod]
    public void TestADiscIsInsideItsBox()
    {
        foreach (Vector facing in new[]
        {
            new Vector(0, 1, 0), new Vector(1, 0, 0), new Vector(0, 0, 1),
            new Vector(1, 1, 0), new Vector(0.3, 1, -0.4), new Vector(-1, -2, 3)
        })
        {
            AssertNothingEscapes(
                new Disc { Center = new Point(0, 0, 0), Normal = facing, Radius = 1 }, 3);
            AssertNothingEscapes(
                new Disc
                {
                    Center = new Point(0.4, -0.2, 0.3), Normal = facing,
                    Radius = 1.2, InnerRadius = 0.5
                }, 4);
        }
    }

    /// <summary>
    /// A parallelogram is the case where taking the near and far corners is not enough: a side
    /// running backwards along an axis puts the furthest corner somewhere the arithmetic would not
    /// guess, so the leaning ones are what is asked about here.
    /// </summary>
    [TestMethod]
    public void TestAParallelogramIsInsideItsBox()
    {
        foreach ((Point corner, Vector first, Vector second) in new[]
        {
            (new Point(0, 0, 0), new Vector(1, 0, 0), new Vector(0, 1, 0)),
            // Sides running the other way, which is what a two-corner box gets wrong.
            (new Point(0.5, 0.5, 0), new Vector(-1.5, 0, 0), new Vector(0, -1.2, 0)),
            // Leaning in all three directions at once.
            (new Point(-0.3, 0.2, -0.4), new Vector(1.4, 0.6, -0.5), new Vector(-0.2, 1.1, 0.9)),
            // Nearly edge on, where the box is thin in one direction and has no room to spare.
            (new Point(0, 0, 0), new Vector(2, 0.01, 0), new Vector(0, 0.01, 2))
        })
        {
            AssertNothingEscapes(
                new Parallelogram { Point = corner, Side1 = first, Side2 = second }, 4);
        }
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

    [TestMethod]
    public void TestAnEmptyGroupCanBeHitByNothing()
    {
        // A group with nothing in it holds nothing, so no ray can find anything inside it.  It used to
        // report *no box at all*, which says the opposite -- come in and test everything -- and the
        // empty box it reports now turns every ray away, since an empty box carries its minima at the
        // largest number a double holds and its maxima at the smallest.
        Group group = new ();

        group.PrepareForRendering();

        Assert.IsNotNull(group.BoundingBox);

        for (int index = 0; index < Rays; index++)
        {
            Ray ray = RayNumber(index, 4);
            List<Intersection> intersections = [];

            group.Intersect(ray, intersections);

            Assert.AreEqual(0, intersections.Count, $"ray {index} got into an empty group");
        }
    }

    [TestMethod]
    public void TestAnEmptyGroupDoesNotRobItsParentOfABox()
    {
        // The fault this pair of tests exists for, and it was worth a great deal.  A group is treated
        // as unbounded whenever any child cannot say where it is, which is right for a plane and quite
        // wrong for a group holding nothing -- and because the answer travels upward, one empty group
        // left every group above it unbounded too.  Measured on a-stand-of-trees: 328 empty groups
        // robbed 447 more, and each ray was doing 7,227 box tests to enter 25 of them.
        Group group = new ();

        group.Add(new Group());
        group.Add(Ball());
        group.PrepareForRendering();

        Assert.IsNotNull(group.BoundingBox, "an empty child robbed this group of its box");

        // And the box must still hold the sphere.  A box that came out too *small* would be the far
        // worse outcome of getting this wrong: the sphere would vanish in patches rather than merely
        // costing time.
        AssertNothingEscapes(group, 6);
    }

    [TestMethod]
    public void TestAnEmptyGroupBuriedDeepDoesNotRobTheTop()
    {
        // The answer travels up through however many groups there are, so one empty group at the
        // bottom of a stack has to be stopped at the bottom of the stack.
        Group inner = new ();

        inner.Add(new Group());
        inner.Add(Ball());

        Group middle = new ();

        middle.Add(inner);

        Group outer = new ();

        outer.Add(middle);
        outer.Add(Ball(3, 0, 0));
        outer.PrepareForRendering();

        Assert.IsNotNull(inner.BoundingBox);
        Assert.IsNotNull(middle.BoundingBox);
        Assert.IsNotNull(outer.BoundingBox);
        AssertNothingEscapes(outer, 8);
    }

    [TestMethod]
    public void TestAChildThatTrulyCannotSayWhereItIsStillLeavesTheGroupUnbounded()
    {
        // The other side of the comparison, and the reason to write it down: the fix must tell "I hold
        // nothing" apart from "I hold something endless", and a test that only checked the first would
        // pass just as well if the second had been broken along with it.  A plane goes on forever, so a
        // group holding one has to be entered by every ray.
        Group group = new ();

        group.Add(new Plane());
        group.Add(Ball());
        group.PrepareForRendering();

        Assert.IsNull(group.BoundingBox, "a group holding a plane must stay unbounded");
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

    [TestMethod]
    public void TestABlobIsInsideItsBox()
    {
        // One component on its own: the box is its influence ball, which is a good deal larger than
        // the surface, since the surface sits where the field has fallen to the threshold.
        AssertNothingEscapes(BlobOf(new BlobSphereComponent
        {
            Center = Point.Zero, Radius = 2, Strength = 1
        }), 5);

        // Two reaching for each other, where the surface swells out into a neck between them and is
        // in places further out than either component alone would put it.
        AssertNothingEscapes(BlobOf(
            new BlobSphereComponent { Center = new Point(-1.4, 0, 0), Radius = 2, Strength = 1 },
            new BlobSphereComponent { Center = new Point(1.4, 0, 0), Radius = 2, Strength = 1 }), 6);

        // A cylinder, whose primitives are a body and two caps and whose box must hold all three.
        AssertNothingEscapes(BlobOf(new BlobCylinderComponent
        {
            Start = new Point(-2, -0.5, 0.7), End = new Point(2, 1, -0.7), Radius = 1, Strength = 1
        }), 6);

        // And the two together, off the origin, so a box quietly centred on nothing would show.
        AssertNothingEscapes(BlobOf(
            new BlobSphereComponent { Center = new Point(2, 1.5, -1), Radius = 1.6, Strength = 1 },
            new BlobCylinderComponent
            {
                Start = new Point(2, 1.5, -1), End = new Point(-1, -1, 1), Radius = 0.7,
                Strength = 2
            }), 7);

        // The same shapes at a threshold near nothing, which pushes the surface out to within a few
        // percent of the influence radius.  This is the case that actually tests the box -- see
        // BlobOf's own note on why the ones above do not press hard.
        AssertNothingEscapes(BlobOf(1e-4,
            new BlobSphereComponent { Center = Point.Zero, Radius = 2, Strength = 1 }), 5);
        AssertNothingEscapes(BlobOf(1e-4,
            new BlobCylinderComponent
            {
                Start = new Point(-2, -0.5, 0.7), End = new Point(2, 1, -0.7), Radius = 1,
                Strength = 1
            }), 6);

        AssertEveryCrossingIsInsideTheBox(BlobOf(1e-4,
            new BlobSphereComponent { Center = Point.Zero, Radius = 2, Strength = 1 }), 5);
        AssertEveryCrossingIsInsideTheBox(BlobOf(1e-4,
            new BlobCylinderComponent
            {
                Start = new Point(-2, -0.5, 0.7), End = new Point(2, 1, -0.7), Radius = 1,
                Strength = 1
            }), 6);
    }

    [TestMethod]
    public void TestATransformedBlobComponentIsInsideItsBox()
    {
        // A stretched component reaches further than its own radius, and along an axis its radius
        // knows nothing about.  A box built before the transform rather than after would fall short
        // exactly here.
        AssertNothingEscapes(BlobOf(1e-4, new BlobSphereComponent
        {
            Center = Point.Zero, Radius = 1.5, Strength = 1, Transform = Transforms.Scale(2.5, 1, 1)
        }), 7);

        // And turned, so the reach is along no axis at all and the box has to be the one around
        // where the corners land.
        AssertNothingEscapes(BlobOf(1e-4, new BlobSphereComponent
        {
            Center = Point.Zero, Radius = 1.5, Strength = 1,
            Transform = Transforms.RotateAroundZ(Math.PI / 5, true) * Transforms.Scale(2.5, 1, 1)
        }), 7);

        // A shear, which is the transform that tells an inverse from its transpose -- and moves a
        // box's corners furthest from where a scale would put them.
        AssertNothingEscapes(BlobOf(1e-4, new BlobCylinderComponent
        {
            Start = new Point(-1.5, 0, 0), End = new Point(1.5, 0, 0), Radius = 0.8, Strength = 1,
            Transform = Transforms.Shear(0.7, 0, 0, 0, 0, 0)
        }), 7);

        // And the sharper check on each of the three, since a transformed box is the one most easily
        // built a little too small.
        AssertEveryCrossingIsInsideTheBox(BlobOf(1e-4, new BlobSphereComponent
        {
            Center = Point.Zero, Radius = 1.5, Strength = 1, Transform = Transforms.Scale(2.5, 1, 1)
        }), 7);
        AssertEveryCrossingIsInsideTheBox(BlobOf(1e-4, new BlobSphereComponent
        {
            Center = Point.Zero, Radius = 1.5, Strength = 1,
            Transform = Transforms.RotateAroundZ(Math.PI / 5, true) * Transforms.Scale(2.5, 1, 1)
        }), 7);
        AssertEveryCrossingIsInsideTheBox(BlobOf(1e-4, new BlobCylinderComponent
        {
            Start = new Point(-1.5, 0, 0), End = new Point(1.5, 0, 0), Radius = 0.8, Strength = 1,
            Transform = Transforms.Shear(0.7, 0, 0, 0, 0, 0)
        }), 7);
    }

    [TestMethod]
    public void TestABlobThatReachesForeverHasNoBox()
    {
        // A plane component is a slab running to the horizon, so there is no finite box to give and
        // a blob holding one must say so rather than hand back one holding its other components.
        // Answering with a box here would make the blob vanish everywhere outside it.
        Blob withAPlane = BlobOf(
            new BlobSphereComponent { Center = Point.Zero, Radius = 2, Strength = 1 },
            new BlobPlaneComponent
            {
                Point = Point.Zero, Normal = Directions.Up, Radius = 1, Strength = 1
            });

        withAPlane.PrepareForRendering();

        Assert.IsNull(withAPlane.BoundingBox);

        // And a threshold at nothing makes the whole of space solid, since the field beyond every
        // component is nought and nought clears it.
        Blob withNoThreshold = BlobOf(
            new BlobSphereComponent { Center = Point.Zero, Radius = 2, Strength = 1 });

        withNoThreshold.Threshold = 0;

        withNoThreshold.PrepareForRendering();

        Assert.IsNull(withNoThreshold.BoundingBox);
    }

    /// <summary>
    /// This method insists that every point of the surface a great many rays can find lies inside
    /// the box.
    /// <para>
    /// **This asks a sharper question than the ray-versus-box comparison does**, and it was added
    /// because that comparison could not see a box short by half a percent.  To notice a shortfall
    /// that small by comparing answers, a ray has to be aimed into the thin shell between where the
    /// box stops and where the surface stops -- and a sweep of seventy steps across the whole scene
    /// steps over a shell that thin almost every time.  Asking instead where the crossings *landed*
    /// needs no such luck: thousands of crossings spread over the surface reach very nearly its
    /// widest point on every axis, so a box that stops short of it is caught at once.
    /// </para>
    /// </summary>
    /// <param name="surface">The surface to try.</param>
    /// <param name="from">How far off to fire the rays from.</param>
    private static void AssertEveryCrossingIsInsideTheBox(Surface surface, double from)
    {
        surface.PrepareForRendering();

        BoundingBox box = surface.BoundingBox;

        Assert.IsNotNull(box, "this surface should have worked out a box for itself");

        Point low = box.Minimum;
        Point high = box.Maximum;
        int found = 0;

        for (int index = 0; index < Rays * 4; index++)
        {
            Ray ray = RayNumber(index, from);
            List<Intersection> crossings = [];

            surface.AddIntersections(ray, crossings);

            foreach (Intersection crossing in crossings)
            {
                Point where = ray.At(crossing.Distance);

                found++;

                Assert.IsTrue(
                    where.X >= low.X && where.X <= high.X &&
                    where.Y >= low.Y && where.Y <= high.Y &&
                    where.Z >= low.Z && where.Z <= high.Z,
                    $"ray {index} crossed the surface at {where}, which is outside the box running " +
                    $"{low} to {high}");
            }
        }

        Assert.IsTrue(found > 1000,
            $"only {found} crossings were found, which is too few to have tested the box at all");
    }

    /// <summary>
    /// This method builds a blob from the given components, at a threshold that puts its surface
    /// well inside their influence.
    /// </summary>
    /// <param name="components">The components to build it from.</param>
    /// <returns>The blob.</returns>
    private static Blob BlobOf(params IBlobComponent[] components)
    {
        return BlobOf(0.125, components);
    }

    /// <summary>
    /// This method builds a blob from the given components at the given threshold.
    /// <para>
    /// **The threshold is what decides how hard these tests press.**  A component's box is its
    /// influence, and the surface sits where the field has fallen to the threshold -- so at an
    /// ordinary threshold the surface is well inside the box and a box short by a tenth still holds
    /// it.  Shrinking the box on purpose proved exactly that: the transformed cases did not notice.
    /// A threshold near nothing pushes the surface out to nearly the influence radius, and only then
    /// is the box being asked a real question.  At the ordinary 0.125 the surface stands at 70.7% of
    /// the influence radius and a quarter could be shaved off the box before anything noticed; at
    /// 1e-4 it stands at 97.7% and a shortfall of a few percent is caught.
    /// </para>
    /// <para>
    /// **Do not push it further than that.**  The falloff is a *cube*, so it has a TRIPLE root at the
    /// influence radius, and a threshold near nothing puts the surface right on top of it -- where the
    /// error in locating a root scales as the cube root of the threshold and the solver starts
    /// reporting crossings out beyond the influence entirely, at points where the field is exactly
    /// nought.  A threshold of 1e-9 does this reliably, and it looks exactly like a box that is too
    /// small.  Two and a bit percent of slack is inherent anyway: the box *is* the influence, and the
    /// surface always lies strictly within it.
    /// </para>
    /// </summary>
    /// <param name="threshold">The threshold to build it at.</param>
    /// <param name="components">The components to build it from.</param>
    /// <returns>The blob.</returns>
    private static Blob BlobOf(double threshold, params IBlobComponent[] components)
    {
        Blob blob = new () { Threshold = threshold };

        blob.Components.AddRange(components);

        return blob;
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