using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

[TestClass]
public class TestDeformation
{
    // An arc of a circle about the Y axis, in the XZ plane, starting at [radius, 0, 0] and heading +Z, as cubics
    // each a sixteenth of a right angle -- within a part in a hundred billion of the circle.
    private static Spline Arc(double radius, double angle, int pieces)
    {
        Spline spline = new () { Start = new Point(radius, 0, 0) };
        double step = angle / pieces;
        double reach = 4.0 / 3 * Math.Tan(step / 4) * radius;

        for (int piece = 0; piece < pieces; piece++)
        {
            double from = piece * step;
            double to = from + step;
            Point start = new (radius * Math.Cos(from), 0, radius * Math.Sin(from));
            Point end = new (radius * Math.Cos(to), 0, radius * Math.Sin(to));

            spline.Segments.Add(new SplineSegmentSpec
            {
                Control1 = start + new Vector(-Math.Sin(from), 0, Math.Cos(from)) * reach,
                Control2 = end - new Vector(-Math.Sin(to), 0, Math.Cos(to)) * reach,
                End = end
            });
        }

        return spline;
    }

    private static Spline Line(Point from, Point to)
    {
        Spline spline = new () { Start = from };

        spline.Segments.Add(new SplineSegmentSpec { End = to });

        return spline;
    }

    private static List<Intersection> Crossings(Surface surface, Ray ray)
    {
        List<Intersection> crossings = [];

        surface.Intersect(ray, crossings);

        return crossings.OrderBy(crossing => crossing.Distance).ToList();
    }

    private static IEnumerable<Ray> RaysAt(Random random, Point middle, double spread, int count)
    {
        for (int index = 0; index < count; index++)
        {
            Vector away = new Vector(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5)
                .Unit;
            Point target = middle + new Vector(
                (random.NextDouble() - 0.5) * spread, (random.NextDouble() - 0.5) * spread,
                (random.NextDouble() - 0.5) * spread);

            yield return new Ray(target + away * 10, -away * (0.5 + random.NextDouble()));
        }
    }

    [TestMethod]
    public void TestAStraightSplineAlongTheCenterlineLeavesTheShapeAsItIs()
    {
        // The spline is shorter than the cube and starts inside it, so the straight runs before and after it are
        // tried as well.
        Cube plain = new ();
        Cube bent = new ()
        {
            Deformation = new Deformation
            {
                Spline = Line(new Point(0, -0.3, 0), new Point(0, 0.2, 0)),
                From = new Point(0, -0.3, 0),
                Toward = new Vector(0, 1, 0)
            }
        };

        plain.PrepareForRendering();
        bent.PrepareForRendering();

        int crossed = 0;

        foreach (Ray ray in RaysAt(new Random(7), Point.Zero, 2.4, 2000))
        {
            List<Intersection> expected = Crossings(plain, ray);
            List<Intersection> actual = Crossings(bent, ray);

            Assert.AreEqual(expected.Count, actual.Count);

            for (int index = 0; index < expected.Count; index++)
            {
                Assert.AreEqual(expected[index].Distance, actual[index].Distance, 1e-9);

                Point place = ray.At(actual[index].Distance);

                Assert.IsTrue(plain.NormalAt(place, expected[index])
                    .Dot(bent.NormalAt(place, actual[index])) > 1 - 1e-12);
            }

            crossed += expected.Count;
        }

        Assert.IsTrue(crossed > 1000);
    }

    [TestMethod]
    public void TestACylinderBentRoundAnArcIsATorus()
    {
        // A tube of radius one, 0.8 of a right angle long, bent round a circle of radius three, is that much of a
        // torus: the same crossings at the same distances, facing the same ways.
        const double major = 3;
        const double sweep = 0.8 * Math.PI / 2;
        Torus torus = new () { MajorRadius = major, MinorRadius = 1 };
        Cylinder tube = new ()
        {
            MinimumY = 0,
            MaximumY = major * sweep,
            Closed = false,
            Deformation = new Deformation
            {
                Spline = Arc(major, Math.PI / 2, 16),
                Toward = new Vector(0, 1, 0)
            }
        };

        torus.PrepareForRendering();
        tube.PrepareForRendering();

        int crossed = 0;

        foreach (Ray ray in RaysAt(new Random(11), new Point(1.5, 0, 1.5), 5, 3000))
        {
            List<Intersection> all = Crossings(torus, ray);

            // Only the crossings within the tube's sweep, and none of the rays meeting the torus within a hair of
            // either end of it, where an open tube's rim decides.
            List<double> angles = all
                .Select(crossing => ray.At(crossing.Distance))
                .Select(place => Math.Atan2(place.Z, place.X))
                .ToList();

            if (angles.Any(angle => Math.Abs(angle) < 1e-6 || Math.Abs(angle - sweep) < 1e-6))
                continue;

            List<Intersection> expected = all.Where((_, index) => angles[index] > 0 && angles[index] < sweep).ToList();
            List<Intersection> actual = Crossings(tube, ray);

            Assert.AreEqual(expected.Count, actual.Count, $"Crossings of the ray from {ray.Origin}.");

            for (int index = 0; index < expected.Count; index++)
            {
                Assert.AreEqual(expected[index].Distance, actual[index].Distance, 1e-8);

                Point place = ray.At(actual[index].Distance);

                Assert.IsTrue(torus.NormalAt(place, expected[index])
                    .Dot(tube.NormalAt(place, actual[index])) > 1 - 1e-10);
            }

            crossed += expected.Count;
        }

        Assert.IsTrue(crossed > 1000, $"Only {crossed} crossings were tried.");
    }

    [TestMethod]
    public void TestABentShapeIsPlacedByItsTransforms()
    {
        Cube plain = new () { Transform = Transforms.Translate(5, 0, 0) };
        Cube bent = new ()
        {
            Transform = Transforms.Translate(5, 0, 0),
            Deformation = new Deformation
            {
                Spline = Line(new Point(0, -1, 0), new Point(0, 1, 0)),
                From = new Point(0, -1, 0),
                Toward = new Vector(0, 1, 0)
            }
        };

        plain.PrepareForRendering();
        bent.PrepareForRendering();

        Ray ray = new (new Point(5.2, 0.3, -10), new Vector(0, 0, 1));

        Assert.AreEqual(Crossings(plain, ray)[0].Distance, Crossings(bent, ray)[0].Distance, 1e-9);
    }

    [TestMethod]
    public void TestABendTighterThanTheShapeIsThickIsRefused()
    {
        Cylinder tube = new ()
        {
            MinimumY = 0,
            MaximumY = 1,
            Deformation = new Deformation
            {
                Spline = Arc(0.9, Math.PI / 2, 4),
                Toward = new Vector(0, 1, 0)
            }
        };

        Assert.ThrowsExactly<InvalidOperationException>(() => tube.PrepareForRendering());
    }

    // A bar four long up the Y axis, white against black, seen from -Z, bent or not by what follows it.
    private static Canvas RenderABar(string deformed, out string error)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"deformation-tests-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "scene.igl");
        string output = Path.Combine(directory, "out.png");

        Directory.CreateDirectory(directory);
        File.WriteAllText(path, $$"""
            camera { location [1.5, 2, -12]  look at [1.5, 2, 0] }
            point light { location [0, 0, -10] }
            Arc = spline {
                move to 0, 0, 0
                curve 0, 1.6569, 0, 1.3431, 3, 0 to 3, 3, 0
            }
            group {
                cube { material { pigment White  ambient 1 } scale [0.2, 2, 0.2] translate [0, 2, 0] }
                {{deformed}}
            }
            """);

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            if (renderer is null)
            {
                error = captured.ToString();

                return null;
            }

            renderer.Render(new RenderOptions { OutputFileName = output, Width = 120, Height = 120 });
            error = captured.ToString().Contains("Error") ? captured.ToString() : null;

            return error is null ? new ImageFile(output).Load()[0] : null;
        }
        finally
        {
            Console.SetOut(was);
            Directory.Delete(directory, true);
        }
    }

    // The middle, across, of the white in a picture, or -1 if there is none.
    private static double MiddleOfTheWhite(Canvas image)
    {
        double sum = 0;
        int count = 0;

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                if (image.GetPixel(x, y).Red > 0.5)
                {
                    sum += x;
                    count++;
                }
            }
        }

        return count == 0 ? -1 : sum / count;
    }

    [TestMethod]
    public void TestADeformedBlockBendsWhatItIsWrittenIn()
    {
        Canvas straight = RenderABar("", out string error);

        Assert.IsNull(error, error);

        Canvas bent = RenderABar("deformed { along Arc  centerline toward Y }", out error);

        Assert.IsNull(error, error);

        // Straight, the bar stands over its foot; bent round toward +X, most of it lies well over to the right.
        Assert.IsTrue(MiddleOfTheWhite(straight) > 0 && MiddleOfTheWhite(bent) > MiddleOfTheWhite(straight) + 8,
            $"The bar's middle: straight {MiddleOfTheWhite(straight)}, bent {MiddleOfTheWhite(bent)}.");
    }

    [TestMethod]
    public void TestADeformationMustSayItsSplineAndItsCenterline()
    {
        RenderABar("deformed { centerline toward Y }", out string error);

        Assert.IsTrue(error?.Contains("\"along\"") ?? false, error);

        RenderABar("deformed { along Arc }", out error);

        Assert.IsTrue(error?.Contains("centerline toward") ?? false, error);
    }

    // A bar 0.5 either side of the Y axis up to four, made of a cube in a group so that its own transforms are inside
    // what the group's deformation bends.
    private static Group Bar(Deformation deformation, double half = 0.5, double height = 4)
    {
        Group bar = new () { Deformation = deformation };

        bar.Add(new Cube { Transform = Transforms.Translate(0, height / 2, 0) * Transforms.Scale(half, height / 2, half) });

        return bar;
    }

    [TestMethod]
    public void TestATwistLeavesARoundBarAsItIs()
    {
        Cylinder plain = new () { MinimumY = 0, MaximumY = 4 };
        Cylinder twisted = new ()
        {
            MinimumY = 0,
            MaximumY = 4,
            Deformation = new Deformation
            {
                Spline = Line(Point.Zero, new Point(0, 4, 0)), Toward = new Vector(0, 1, 0), Twist = Math.PI / 2
            }
        };

        plain.PrepareForRendering();
        twisted.PrepareForRendering();

        int crossed = 0;

        foreach (Ray ray in RaysAt(new Random(3), new Point(0, 2, 0), 4, 1500))
        {
            List<Intersection> expected = Crossings(plain, ray);
            List<Intersection> actual = Crossings(twisted, ray);

            Assert.AreEqual(expected.Count, actual.Count);

            for (int index = 0; index < expected.Count; index++)
            {
                Assert.AreEqual(expected[index].Distance, actual[index].Distance, 1e-8);

                Point place = ray.At(actual[index].Distance);

                Assert.IsTrue(plain.NormalAt(place, expected[index])
                    .Dot(twisted.NormalAt(place, actual[index])) > 1 - 1e-9);
            }

            crossed += expected.Count;
        }

        Assert.IsTrue(crossed > 500);
    }

    [TestMethod]
    public void TestATwistTurnsASquareBarEvenlyAlongItsLength()
    {
        // Twisted a right angle over four, the bar is turned by (y / 4) right angles at height y, and a ray through its
        // axis, square on to it, meets the turned square's face half over the greater of |cos| and |sin| of the turn
        // out -- at a height between two of the spline's pieces too, where a ray lies in the plane between them.
        Group bar = Bar(new Deformation
        {
            Spline = Line(Point.Zero, new Point(0, 4, 0)), Toward = new Vector(0, 1, 0), Twist = Math.PI / 2
        });

        bar.PrepareForRendering();

        foreach (double height in new[] { 0.5, 1, 2, 3, 3.5, 4.0 / 3 })
        {
            double turn = Math.PI / 2 * height / 4;
            Ray ray = new (new Point(0, height, 10), new Vector(0, 0, -1));

            List<Intersection> crossings = Crossings(bar, ray);

            Assert.AreEqual(2, crossings.Count, $"At height {height}.");
            Assert.AreEqual(10 - 0.5 / Math.Max(Math.Abs(Math.Cos(turn)), Math.Abs(Math.Sin(turn))),
                crossings[0].Distance, 1e-8, $"At height {height}.");
        }
    }

    [TestMethod]
    public void TestStretchedLaysTheShapeOverTheWholeSpline()
    {
        // A bar two long laid over a spline four long is twice as long, and no thicker.
        Group bar = Bar(new Deformation
        {
            Spline = Line(Point.Zero, new Point(0, 4, 0)), Toward = new Vector(0, 1, 0), Stretched = true
        }, 0.2, 2);

        bar.PrepareForRendering();

        Assert.AreEqual(10 - 4, Crossings(bar, new Ray(new Point(0, 10, 0), new Vector(0, -1, 0)))[0].Distance, 1e-8);
        Assert.AreEqual(10 - 0.2, Crossings(bar, new Ray(new Point(0, 3.5, 10), new Vector(0, 0, -1)))[0].Distance, 1e-8);
    }

    [TestMethod]
    public void TestABentShapeInACombinationIsCutAsItStands()
    {
        // Bent along a straight spline it is itself, and so is what is cut from it.
        CsgSurface plain = new () { Operation = CsgOperation.Difference, Left = new Cube(), Right = new Sphere { Transform = Transforms.Translate(1, 1, 1) } };
        CsgSurface bent = new ()
        {
            Operation = CsgOperation.Difference,
            Left = new Cube
            {
                Deformation = new Deformation
                {
                    Spline = Line(new Point(0, -0.5, 0), new Point(0, 0.5, 0)), From = new Point(0, -0.5, 0),
                    Toward = new Vector(0, 1, 0)
                }
            },
            Right = new Sphere { Transform = Transforms.Translate(1, 1, 1) }
        };

        plain.PrepareForRendering();
        bent.PrepareForRendering();

        int crossed = 0;

        foreach (Ray ray in RaysAt(new Random(5), Point.Zero, 2.4, 1500))
        {
            List<Intersection> expected = Crossings(plain, ray);
            List<Intersection> actual = Crossings(bent, ray);

            Assert.AreEqual(expected.Count, actual.Count);

            for (int index = 0; index < expected.Count; index++)
                Assert.AreEqual(expected[index].Distance, actual[index].Distance, 1e-9);

            crossed += expected.Count;
        }

        Assert.IsTrue(crossed > 500);
    }

    [TestMethod]
    public void TestABentAndTwistedBarFacesTheWayItsSurfaceRuns()
    {
        // Whatever the bend and twist, a crossing's normal is square to the way the surface runs there, found by rays
        // a hair to either side meeting the same face.
        Group bar = Bar(new Deformation
        {
            Spline = Arc(6, Math.PI / 2, 8), From = Point.Zero, Toward = new Vector(0, 1, 0), Twist = Math.PI / 3
        });

        bar.Transform = Transforms.Translate(-6, 0, 0);
        bar.PrepareForRendering();

        const double step = 1e-5;
        int checkedFaces = 0;

        foreach (Ray ray in RaysAt(new Random(9), new Point(-2, 0, 3), 6, 3000))
        {
            List<Intersection> crossings = Crossings(bar, ray);

            if (crossings.Count == 0)
                continue;

            Point place = ray.At(crossings[0].Distance);
            Vector normal = bar.Surfaces[0].NormalAt(place, crossings[0]);
            Vector aside = ray.Direction.Cross(new Vector(0.3, 1, 0.2)).Unit;
            Vector other = ray.Direction.Cross(aside).Unit;
            bool sameFace = true;

            foreach (Vector offset in new[] { aside * step, other * step })
            {
                Ray beside = new (ray.Origin + offset, ray.Direction);
                List<Intersection> near = Crossings(bar, beside);

                if (near.Count == 0 || Math.Abs(near[0].Distance - crossings[0].Distance) > 100 * step)
                {
                    sameFace = false;

                    break;
                }

                Vector along = beside.At(near[0].Distance) - place;

                Assert.AreEqual(0, normal.Dot(along) / along.Magnitude, 1e-4);
            }

            if (sameFace)
                checkedFaces++;
        }

        Assert.IsTrue(checkedFaces > 300, $"Only {checkedFaces} crossings were checked.");
    }

    [TestMethod]
    public void TestAFootprintIsCarriedThroughABendAsItsDirectionsAre()
    {
        Cube cube = new ()
        {
            Deformation = new Deformation { Spline = Arc(3, Math.PI / 2, 8), Toward = new Vector(0, 1, 0), From = new Point(0, -1, 0) }
        };

        cube.PrepareForRendering();

        Point inside = new (0.2, 0.6, 0.3);
        Point world = cube.SurfaceToWorld(inside);
        Vector across = new (0.01, 0.02, 0);
        Vector along = new (0, 0.01, 0.03);
        Footprint carried = cube.WorldToSurface(
            new Footprint(cube.DirectionToWorld(across, 0, default, inside), cube.DirectionToWorld(along, 0, default, inside)),
            0, default, world);

        Assert.IsTrue((carried.Across - across).Magnitude < 1e-12 && (carried.Along - along).Magnitude < 1e-12,
            $"Carried back as {carried.Across} and {carried.Along}.");
        Assert.IsTrue((cube.WorldToSurface(world) - inside).Magnitude < 1e-12);
    }

    [TestMethod]
    public void TestABentInstanceIsTheShapeItStandsForBent()
    {
        Deformation Bend() => new () { Spline = Arc(3, Math.PI / 2, 8), Toward = new Vector(0, 1, 0) };

        Group shape = Bar(null);
        Group direct = Bar(Bend());
        Instance instance = new () { Prototype = shape, Deformation = Bend() };

        direct.PrepareForRendering();
        instance.PrepareForRendering();

        int crossed = 0;

        foreach (Ray ray in RaysAt(new Random(13), new Point(2, 0, 2), 4, 1500))
        {
            List<Intersection> expected = Crossings(direct, ray);
            List<Intersection> actual = Crossings(instance, ray);

            Assert.AreEqual(expected.Count, actual.Count);

            for (int index = 0; index < expected.Count; index++)
            {
                Assert.AreEqual(expected[index].Distance, actual[index].Distance, 1e-9);
                Assert.IsTrue(ReferenceEquals(actual[index].Portals.Through, instance));
            }

            crossed += expected.Count;
        }

        Assert.IsTrue(crossed > 500);
    }

    [TestMethod]
    public void TestWhatABendCannotCarryIsRefused()
    {
        Group holding = new ()
        {
            Deformation = new Deformation { Spline = Line(Point.Zero, new Point(0, 4, 0)), Toward = new Vector(0, 1, 0) }
        };

        Strand strand = new () { Width = 0.01 };

        strand.Points.Add(new RibbonControlPoint { Position = new Point(0, 1, 0), Width = 0.01 });
        strand.Points.Add(new RibbonControlPoint { Position = new Point(0, 3, 0), Width = 0.01 });
        holding.Add(strand);

        Assert.ThrowsExactly<InvalidOperationException>(() => holding.PrepareForRendering());

        Group nowhere = Bar(new Deformation
        {
            Spline = Line(Point.Zero, new Point(0, 4, 0)), From = new Point(0, 10, 0), Toward = new Vector(0, 1, 0),
            Stretched = true
        });

        Assert.ThrowsExactly<InvalidOperationException>(() => nowhere.PrepareForRendering());
    }
}
