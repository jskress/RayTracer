using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace Tests;

[TestClass]
public class TestCsgSurfaces
{
    private static readonly List<(CsgOperation, bool, bool, bool, bool)> IntersectionAllowedTests =
    [
        (CsgOperation.Union, true, true, true, false),
        (CsgOperation.Union, true, true, false, true),
        (CsgOperation.Union, true, false, true, false),
        (CsgOperation.Union, true, false, false, true),
        (CsgOperation.Union, false, true, true, false),
        (CsgOperation.Union, false, true, false, false),
        (CsgOperation.Union, false, false, true, true),
        (CsgOperation.Union, false, false, false, true),

        (CsgOperation.Intersection, true, true, true, true),
        (CsgOperation.Intersection, true, true, false, false),
        (CsgOperation.Intersection, true, false, true, true),
        (CsgOperation.Intersection, true, false, false, false),
        (CsgOperation.Intersection, false, true, true, true),
        (CsgOperation.Intersection, false, true, false, true),
        (CsgOperation.Intersection, false, false, true, false),
        (CsgOperation.Intersection, false, false, false, false),

        (CsgOperation.Difference, true, true, true, false),
        (CsgOperation.Difference, true, true, false, true),
        (CsgOperation.Difference, true, false, true, false),
        (CsgOperation.Difference, true, false, false, true),
        (CsgOperation.Difference, false, true, true, true),
        (CsgOperation.Difference, false, true, false, true),
        (CsgOperation.Difference, false, false, true, false),
        (CsgOperation.Difference, false, false, false, false)
    ];
    private static readonly List<(CsgOperation, int, int)> IntersectionFilterTests =
    [
        (CsgOperation.Union, 0, 3),
        (CsgOperation.Intersection, 1, 2),
        (CsgOperation.Difference, 0, 1)
    ];

    [TestMethod]
    public void TestConstruction()
    {
        Sphere sphere = new ();
        Cube cube = new ();
        CsgSurface surface = new ()
        {
            Operation = CsgOperation.Union,
            Left = sphere,
            Right = cube
        };

        Assert.AreEqual(CsgOperation.Union, surface.Operation);
        Assert.AreSame(sphere, surface.Left);
        Assert.AreSame(cube, surface.Right);
        Assert.AreSame(surface, sphere.Parent);
        Assert.AreSame(surface, cube.Parent);
    }

    [TestMethod]
    public void TestIsIntersectionAllowed()
    {
        foreach ((CsgOperation operation, bool isLeftHit, bool isLeftInside,
                     bool isRightInside, bool expected) in IntersectionAllowedTests)
        {
            CsgSurface surface = new ()
            {
                Operation = operation
            };

            Assert.AreEqual(expected, surface.IsIntersectionAllowed(
                isLeftHit, isLeftInside, isRightInside));
        }
    }

    [TestMethod]
    public void TestFilterIntersections()
    {
        foreach ((CsgOperation operation, int index0, int index1) in IntersectionFilterTests)
        {
            Sphere sphere = new ();
            Cube cube = new ();
            CsgSurface surface = new ()
            {
                Operation = operation,
                Left = sphere,
                Right = cube
            };
            List<Intersection> intersections =
            [
                new Intersection(sphere, 1),
                new Intersection(cube, 2),
                new Intersection(sphere, 3),
                new Intersection(cube, 4)
            ];
            List<Intersection> original = [..intersections];

            surface.FilterIntersections(intersections);

            Assert.AreEqual(2, intersections.Count);
            Assert.AreSame(original[index0], intersections[0]);
            Assert.AreSame(original[index1], intersections[1]);
        }
    }

    [TestMethod]
    public void TestRayCsgIntersectionMiss()
    {
        CsgSurface surface = new ()
        {
            Operation = CsgOperation.Union,
            Left = new Sphere(),
            Right = new Cube()
        };
        Ray ray = new (new Point(0, 2, -5), Directions.In);
        List<Intersection> intersections = new ();

        surface.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestRayCsgIntersectionHit()
    {
        Sphere s1 = new ();
        Sphere s2 = new ()
        {
            Transform = Transforms.Translate(0, 0, 0.5)
        };
        CsgSurface surface = new ()
        {
            Operation = CsgOperation.Union,
            Left = s1,
            Right = s2
        };
        Ray ray = new (new Point(0, 0, -5), Directions.In);
        List<Intersection> intersections = new ();

        surface.AddIntersections(ray, intersections);

        Assert.AreEqual(2, intersections.Count);
        Assert.AreEqual(4, intersections[0].Distance);
        Assert.AreSame(s1, intersections[0].Surface);
        Assert.AreEqual(6.5, intersections[1].Distance);
        Assert.AreSame(s2, intersections[1].Surface);
    }

    /// <summary>
    /// Once readied, a CSG puts each crossing on a side by looking it up in a list of what stands
    /// on that side, rather than walking the side.  The list must reach as deep as the walk did: here
    /// the left side is a group holding a CSG, and the crossings are on a sphere inside that.  A
    /// crossing put on the wrong side changes what a difference keeps, and whether the right side's
    /// crossing it keeps is turned inside out.
    /// </summary>
    [TestMethod]
    public void TestReadiedCsgFindsCrossingsDeepInItsSides()
    {
        Sphere deep = new ();
        Cube cube = new ();
        CsgSurface surface = new ()
        {
            Operation = CsgOperation.Difference,
            Left = new Group()
                .Add(new Sphere { Transform = Transforms.Translate(0, 5, 0) })
                .Add(new CsgSurface
                {
                    Operation = CsgOperation.Union,
                    Left = deep,
                    Right = new Sphere { Transform = Transforms.Translate(0, -5, 0) }
                }),
            Right = cube
        };

        surface.PrepareForRendering();

        List<Intersection> intersections =
        [
            new Intersection(deep, 1),
            new Intersection(cube, 2),
            new Intersection(deep, 3),
            new Intersection(cube, 4)
        ];
        List<Intersection> original = [..intersections];

        surface.FilterIntersections(intersections);

        Assert.AreEqual(2, intersections.Count);
        Assert.AreSame(original[0], intersections[0]);
        Assert.AreSame(original[1], intersections[1]);
        Assert.IsFalse(intersections[0].ShouldFlipInsideForOut);
        Assert.IsTrue(intersections[1].ShouldFlipInsideForOut);
    }

    /// <summary>
    /// What stands on a side is listed when the CSG is readied, so a side put in afterward must not
    /// be judged by the list made for the one it replaced.  Both sides are swapped for new shapes
    /// after readying, and the crossings on the new shapes must still land on the right sides.
    /// </summary>
    [TestMethod]
    public void TestReplacingASideAfterReadyingIsSeen()
    {
        CsgSurface surface = new ()
        {
            Operation = CsgOperation.Difference,
            Left = new Sphere(),
            Right = new Cube()
        };

        surface.PrepareForRendering();

        Sphere sphere = new ();
        Cube cube = new ();

        surface.Left = sphere;
        surface.Right = cube;

        List<Intersection> intersections =
        [
            new Intersection(sphere, 1),
            new Intersection(cube, 2),
            new Intersection(sphere, 3),
            new Intersection(cube, 4)
        ];
        List<Intersection> original = [..intersections];

        surface.FilterIntersections(intersections);

        Assert.AreEqual(2, intersections.Count);
        Assert.AreSame(original[0], intersections[0]);
        Assert.AreSame(original[1], intersections[1]);
    }

    /// <summary>
    /// A tube's crossings come from the segments it builds when it is readied, and it builds new
    /// ones each time, so a CSG must list its sides only after they are ready -- and again whenever
    /// it is readied again.  The tube runs up the Y axis, 2 in radius, and a cube keeps its x &gt;= 0
    /// half; a ray coming in along X from x = 5 must meet the tube's wall at x = 2 and then the cut
    /// face at x = 0, and nothing else.
    /// </summary>
    [TestMethod]
    public void TestTubeInReadiedCsgKeepsTheRightCrossings()
    {
        Tube tube = new ()
        {
            Start = new TubeControlPoint { Center = new Point(0, -10, 0), Radius = 2 },
            Segments =
            {
                new TubeSegmentSpec { End = new TubeControlPoint { Center = new Point(0, 10, 0), Radius = 2 } }
            }
        };
        Cube cube = new ()
        {
            Transform = Transforms.Translate(10, 0, 0) * Transforms.Scale(10, 10, 10)
        };
        CsgSurface surface = new ()
        {
            Operation = CsgOperation.Intersection,
            Left = tube,
            Right = cube
        };

        surface.PrepareForRendering();
        surface.PrepareForRendering();

        Ray ray = new (new Point(5, 0, 0), Directions.Left);
        List<Intersection> intersections = [];

        surface.AddIntersections(ray, intersections);

        Assert.AreEqual(2, intersections.Count);
        Assert.AreEqual(3, intersections[0].Distance, 1e-9);
        Assert.IsTrue(new SurfaceIterator(tube).Surfaces.Contains(intersections[0].Surface));
        Assert.AreEqual(5, intersections[1].Distance, 1e-9);
        Assert.AreSame(cube, intersections[1].Surface);
    }

    /// <summary>
    /// A crossing found through an instance stands in the CSG's tree as the instance, not as the
    /// shared shape behind it, so it is the instance that the list of a side must hold.  A unit
    /// sphere, shared, has its x &gt;= 0 half cut away by a cube; a ray coming in along X must meet
    /// the sphere at x = -1, through the instance, and then the cut face at x = 0.
    /// </summary>
    [TestMethod]
    public void TestInstanceInReadiedCsgKeepsTheRightCrossings()
    {
        Instance instance = new () { Prototype = new Sphere() };
        Cube cube = new ()
        {
            Transform = Transforms.Translate(10, 0, 0) * Transforms.Scale(10, 10, 10)
        };
        CsgSurface surface = new ()
        {
            Operation = CsgOperation.Difference,
            Left = instance,
            Right = cube
        };

        surface.PrepareForRendering();

        Ray ray = new (new Point(-5, 0, 0), Directions.Right);
        List<Intersection> intersections = [];

        surface.AddIntersections(ray, intersections);

        Assert.AreEqual(2, intersections.Count);
        Assert.AreEqual(4, intersections[0].Distance, 1e-9);
        Assert.AreSame(instance, intersections[0].Portal);
        Assert.AreEqual(5, intersections[1].Distance, 1e-9);
        Assert.AreSame(cube, intersections[1].Surface);
    }

    /// <summary>
    /// An extrusion built from flat pieces -- its end caps and its walls -- must, like the
    /// analytic solids, report crossings behind a ray's origin so a CSG can tell inside from
    /// outside for a ray that starts within it.  A square profile is extruded into a box
    /// (x, z in [-1, 1], y in [0, 2]) and another box keeps only the x &gt;= 0 half.  A ray that
    /// starts inside the extrusion in the cut-away half and rises toward the kept half -- the
    /// path a shadow ray takes to an overhead light -- must not be occluded by the removed part.
    /// </summary>
    [TestMethod]
    public void TestExtrusionInCsgIsNotOccludedByItsRemovedPart()
    {
        GeneralPath profile = new GeneralPath()
            .MoveTo(-1, -1)
            .LineTo(1, -1)
            .LineTo(1, 1)
            .LineTo(-1, 1)
            .ClosePath();
        Extrusion extrusion = new () { Path = profile, MinimumY = 0, MaximumY = 2, Closed = true };
        Cube cube = new ()
        {
            Transform = Transforms.Translate(10, 0, 0) * Transforms.Scale(10, 10, 10)
        };
        CsgSurface csg = new ()
        {
            Operation = CsgOperation.Intersection, Left = extrusion, Right = cube
        };

        csg.PrepareForRendering();

        Point from = new (-0.5, 1, 0);   // inside the box, in the removed x < 0 half.
        Point toward = new (0.5, 10, 0);  // up and over the kept half, as toward a lamp.
        Vector direction = toward - from;
        double distance = direction.Magnitude;
        Ray ray = new (from, direction.Unit);
        List<Intersection> hits = [];

        csg.AddIntersections(ray, hits);

        Assert.IsFalse(
            hits.Any(hit => hit.Distance > 0.0001 && hit.Distance < distance),
            "nothing the CSG keeps should stand between a point in the removed half and the lamp");
    }

    /// <summary>
    /// A plane is a half-space, so a ray climbing up through it began inside it, and one heading down
    /// began outside; one running along it is inside all the way if it is below.  Turned over, the
    /// plane's inside turns over with it.
    /// </summary>
    [TestMethod]
    public void TestAPlaneKnowsWhereARayBegins()
    {
        Plane plane = new ();
        Plane over = new () { Transform = Transforms.RotateAroundX(180) };

        Assert.IsTrue(plane.StartsInside(new Ray(new Point(0, 5, 0), new Vector(0, 1, 0))), "climbing");
        Assert.IsFalse(plane.StartsInside(new Ray(new Point(0, -5, 0), new Vector(0, -1, 0))), "falling");
        Assert.IsTrue(plane.StartsInside(new Ray(new Point(0, -1, 0), new Vector(1, 0, 0))), "along, below");
        Assert.IsFalse(plane.StartsInside(new Ray(new Point(0, 1, 0), new Vector(1, 0, 0))), "along, above");
        Assert.IsFalse(over.StartsInside(new Ray(new Point(0, 5, 0), new Vector(0, 1, 0))), "turned over");
        Assert.IsFalse(new Sphere().StartsInside(new Ray(Point.Zero, new Vector(0, 1, 0))), "a sphere");
    }

    /// <summary>
    /// A floor cut from a plane by a box is lit from above: the top of the box, which the cut took away,
    /// casts no shadow on it.
    /// </summary>
    [TestMethod]
    public void TestAFloorCutFromAPlaneIsNotShadowedByWhatWasCutAway()
    {
        Scene scene = new ();
        PointLight lamp = new () { Location = new Point(0, 10, 0) };

        scene.Lights.Add(lamp);
        scene.Surfaces.Add(new CsgSurface
        {
            Operation = CsgOperation.Intersection,
            Left = new Plane(),
            Right = new Cube { Transform = Transforms.Scale(5, 1, 5) }
        });

        Assert.IsFalse(scene.IsInShadow(lamp, new Point(0, 0.001, 0)), "on the floor");
        Assert.IsFalse(scene.IsInShadow(lamp, new Point(2, 0.5, 2)), "above it");
    }

    /// <summary>
    /// A sphere with its lower half cut away by a plane, seen from below, shows its flat cut and then
    /// its dome -- not the half that is gone.  It must, whether or not it has been readied.
    /// </summary>
    [TestMethod]
    public void TestASphereCutByAPlaneShowsItsTopToARayFromBelow()
    {
        foreach (bool readied in new[] { false, true })
        {
            CsgSurface half = new ()
            {
                Operation = CsgOperation.Difference, Left = new Sphere(), Right = new Plane()
            };

            if (readied)
                half.PrepareForRendering();

            AssertCrossings(half, new Ray(new Point(0, -2, 0), new Vector(0, 1, 0)), [2, 3],
                readied ? "readied" : "as built");
        }
    }

    /// <summary>
    /// A ray running along inside a floor cut from a plane, under its top, crosses the sides of the box
    /// it was cut by, since it is inside the plane the whole way.
    /// </summary>
    [TestMethod]
    public void TestARayAlongInsideACutPlaneMeetsTheSides()
    {
        CsgSurface floor = new ()
        {
            Operation = CsgOperation.Intersection, Left = new Plane(), Right = new Cube()
        };

        AssertCrossings(floor, new Ray(new Point(-5, -0.5, 0), new Vector(1, 0, 0)), [4, 6], "along it");
    }

    /// <summary>
    /// A plane is still a plane when it stands in a group or is shared through an instance: a ray
    /// climbing through either, cut by a box, enters the box inside the plane and leaves the plane
    /// inside the box.
    /// </summary>
    [TestMethod]
    public void TestAPlaneInAGroupOrAnInstanceStillBeginsInside()
    {
        Surface[] wrapped = [new Group { Surfaces = { new Plane() } }, new Instance { Prototype = new Plane() }];

        foreach (Surface plane in wrapped)
        {
            foreach (bool readied in new[] { false, true })
            {
                CsgSurface floor = new ()
                {
                    Operation = CsgOperation.Intersection, Left = plane, Right = new Cube()
                };

                if (readied)
                    floor.PrepareForRendering();

                AssertCrossings(floor, new Ray(new Point(0, -3, 0), new Vector(0, 1, 0)), [2, 3],
                    $"{plane.GetType().Name}, {(readied ? "readied" : "as built")}");
            }
        }
    }

    /// <summary>
    /// A combination that is endless reports where a ray begins to the combination it is a side of,
    /// whether it is that side itself or stands in a group that is.  A plane joined to a sphere above
    /// it, all cut to a box: a ray climbing through enters the box already inside the union, leaves the
    /// plane, enters the sphere, and leaves the box.
    /// </summary>
    [TestMethod]
    public void TestAnEndlessSideSaysWhereARayBegins()
    {
        foreach (bool grouped in new[] { false, true })
        {
            foreach (bool readied in new[] { false, true })
            {
                CsgSurface union = new ()
                {
                    Operation = CsgOperation.Union,
                    Left = new Plane(),
                    Right = new Sphere { Transform = Transforms.Translate(0, 1.5, 0) }
                };
                CsgSurface cut = new ()
                {
                    Operation = CsgOperation.Intersection,
                    Left = grouped ? new Group { Surfaces = { union } } : union,
                    Right = new Cube { Transform = Transforms.Scale(2) }
                };

                if (readied)
                    cut.PrepareForRendering();

                AssertCrossings(cut, new Ray(new Point(0, -5, 0), new Vector(0, 1, 0)), [3, 5, 5.5, 7],
                    $"{(grouped ? "in a group" : "alone")}, {(readied ? "readied" : "as built")}");
            }
        }
    }

    /// <summary>
    /// This method asserts that a surface keeps exactly the crossings of a ray at the given distances.
    /// </summary>
    private static void AssertCrossings(Surface surface, Ray ray, double[] expected, string what)
    {
        List<Intersection> hits = [];

        surface.Intersect(ray, hits);

        double[] found = hits.Select(hit => hit.Distance).OrderBy(distance => distance).ToArray();

        Assert.AreEqual(expected.Length, found.Length, $"{what}: crossings at {string.Join(", ", found)}");

        for (int index = 0; index < expected.Length; index++)
            Assert.AreEqual(expected[index], found[index], 1e-9, $"{what}: crossing {index}");
    }
}
