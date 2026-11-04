using System.Reflection;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;

namespace Tests;

/// <summary>
/// These tests cover the fractals beside the Julia set: the Mandelbulb, the Mandelbox and the Menger sponge.  None
/// can be held to an equation, so what is held is what is known of each without rendering it: where it lies, the
/// symmetries it has, how much of the cube the sponge keeps, and that a ray going in comes out.
/// </summary>
[TestClass]
public class TestFractals
{
    private string _directory;

    [TestInitialize]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"fractals-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// This method asks a fractal how far its surface is from a point, as the march does.
    /// </summary>
    private static double DistanceAt(DistanceEstimatedSurface shape, double x, double y, double z)
    {
        MethodInfo method = typeof(DistanceEstimatedSurface).GetMethod(
            "DistanceAt", BindingFlags.NonPublic | BindingFlags.Instance)!;

        return (double) method.Invoke(shape, [x, y, z])!;
    }

    /// <summary>
    /// This method returns where a ray crosses a shape, nearest first, ahead of its origin.
    /// </summary>
    private static List<double> Crossings(Surface shape, Ray ray)
    {
        List<Intersection> found = [];

        shape.AddIntersections(ray, found);

        return found.Where(crossing => crossing.Distance > 0).Select(crossing => crossing.Distance).Order().ToList();
    }

    /// <summary>
    /// Every crossing a ray toward a fractal finds lies within the box it says it lies in (for the bulb and the box,
    /// the reach of the set grown by four details, their surfaces standing off it), and the farthest reaches
    /// most of the way to it -- a box drawn too small would cut the shape, and still keep every crossing inside --
    /// a ray heading away from it meets nothing, and rays toward its middle do meet it, going in and coming out
    /// again, as a solid is met.
    /// </summary>
    [TestMethod]
    public void TestEachFractalKeepsToItsOwnBoxAndIsSolid()
    {
        // The bulb's and the box's surfaces stand off their sets at their detail, and their boxes are grown by four
        // details to hold them.
        (DistanceEstimatedSurface Shape, double Reach, double Least)[] shapes =
        [
            (new Mandelbulb(), 2 + 4 * 0.002, 1), (new Mandelbulb { Power = 3 }, 2 + 4 * 0.002, 1),
            (new Mandelbox(), 6 + 4 * 0.01, 4.8), (new Mandelbox { Factor = -1.5 }, 2 + 4 * 0.01, 1.6),
            (new MengerSponge(), 1, 0.95)
        ];

        foreach ((DistanceEstimatedSurface shape, double reach, double least) in shapes)
        {
            Random random = new (20261104);
            int met = 0;
            int odd = 0;
            double reached = 0;

            shape.PrepareForRendering();

            for (int index = 0; index < 150; index++)
            {
                Vector outward = new Vector(
                    random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5).Unit;
                Point from = Point.Zero + outward * reach * (2 + random.NextDouble());
                Point near = new (
                    (random.NextDouble() - 0.5) * reach * 0.3, (random.NextDouble() - 0.5) * reach * 0.3,
                    (random.NextDouble() - 0.5) * reach * 0.3);
                Ray toward = new (from, (near - from).Unit);
                List<double> crossings = Crossings(shape, toward);

                foreach (double distance in crossings)
                {
                    Point at = toward.At(distance);
                    double farthest = Math.Max(Math.Abs(at.X), Math.Max(Math.Abs(at.Y), Math.Abs(at.Z)));

                    Assert.IsTrue(farthest <= reach * 1.001,
                        $"{shape.GetType().Name} met {farthest:F4} out, past {reach}");
                    reached = Math.Max(reached, farthest);
                }

                if (crossings.Count > 0)
                    met++;

                if (crossings.Count % 2 == 1)
                    odd++;

                Assert.AreEqual(0, Crossings(shape, new Ray(from, outward)).Count,
                    $"a ray heading away from the {shape.GetType().Name} met it");
            }

            Assert.IsTrue(met > 60, $"only {met} of 150 rays toward the {shape.GetType().Name} met it");
            Assert.IsTrue(reached >= least, $"the {shape.GetType().Name} reached only {reached:F3} of its {reach}");
            Assert.IsTrue(odd <= 3, $"{odd} of 150 rays met the {shape.GetType().Name} an odd number of times");
        }
    }

    /// <summary>
    /// A fractal whose surface stands off its set is held whole by its own box: no ray toward it meets it where the
    /// box cuts it.  The set reaches its box, so a box no larger than the set cut the standing-off surface flat on
    /// every face -- at a detail of a twentieth, a box of a fractal of boxes.
    /// </summary>
    [TestMethod]
    public void TestAFractalsBoxHoldsItsWholeSurface()
    {
        foreach (DistanceEstimatedSurface shape in new DistanceEstimatedSurface[]
                 {
                     new Mandelbox { Detail = 0.05 }, new Mandelbox { Factor = -1.5, Detail = 0.05 },
                     new Mandelbulb { Detail = 0.05 }
                 })
        {
            Random random = new (5);
            int cut = 0;

            shape.PrepareForRendering();

            for (int index = 0; index < 200; index++)
            {
                Vector outward = new Vector(
                    random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5).Unit;
                Ray toward = new (Point.Zero + outward * 20, -outward);
                List<Intersection> found = [];

                shape.AddIntersections(toward, found);
                cut += found.Count(crossing => crossing is PrecomputedNormalIntersection);
            }

            Assert.AreEqual(0, cut, $"the {shape.GetType().Name}'s box cut it {cut} times");
        }
    }

    /// <summary>
    /// A Mandelbulb stands up the Y axis, the scene's up: raising to the power n multiplies the angle round the axis
    /// by n, and adding the point back keeps it, so the bulb is the same turned by a (n - 1)th of a turn about its
    /// axis -- seven-fold for the eighth power -- about Y, and not about Z.
    /// </summary>
    [TestMethod]
    public void TestAMandelbulbStandsUpTheYAxis()
    {
        Mandelbulb bulb = new ();
        Random random = new (7);
        double turn = 2 * Math.PI / 7;
        double aboutZ = 0;

        bulb.PrepareForRendering();

        for (int index = 0; index < 200; index++)
        {
            double x = random.NextDouble() * 2.4 - 1.2, y = random.NextDouble() * 2.4 - 1.2;
            double z = random.NextDouble() * 2.4 - 1.2;
            double here = DistanceAt(bulb, x, y, z);
            double turned = DistanceAt(bulb,
                x * Math.Cos(turn) + z * Math.Sin(turn), y, -x * Math.Sin(turn) + z * Math.Cos(turn));

            Assert.AreEqual(here, turned, 1e-9, $"turned about Y at ({x:F3}, {y:F3}, {z:F3})");

            aboutZ = Math.Max(aboutZ, Math.Abs(here - DistanceAt(bulb,
                x * Math.Cos(turn) - y * Math.Sin(turn), x * Math.Sin(turn) + y * Math.Cos(turn), z)));
        }

        Assert.IsTrue(aboutZ > 0.01, "the bulb should not have its seven-fold turn about Z");
    }

    /// <summary>
    /// A Mandelbox has a cube's symmetries -- every fold and every turn of the sphere is the same along each axis
    /// and either way along it -- so its distance is the same at a point mirrored across any axis or with any two
    /// coordinates swapped.
    /// </summary>
    [TestMethod]
    public void TestAMandelboxHasACubesSymmetries()
    {
        foreach (Mandelbox box in new[] { new Mandelbox(), new Mandelbox { Factor = -1.5 } })
        {
            Random random = new (11);

            box.PrepareForRendering();

            for (int index = 0; index < 200; index++)
            {
                double x = random.NextDouble() * 4 - 2, y = random.NextDouble() * 4 - 2;
                double z = random.NextDouble() * 4 - 2;
                double here = DistanceAt(box, x, y, z);

                Assert.AreEqual(here, DistanceAt(box, -x, y, z), 1e-9);
                Assert.AreEqual(here, DistanceAt(box, x, -y, z), 1e-9);
                Assert.AreEqual(here, DistanceAt(box, y, x, z), 1e-9);
                Assert.AreEqual(here, DistanceAt(box, z, y, x), 1e-9);
            }
        }
    }

    /// <summary>
    /// A Menger sponge keeps twenty of every twenty-seven of a cube at each cut, so after n cuts it holds (20/27)^n
    /// of the cube from -1 to 1: within a percent and a half over 60 thousand points, for one to four cuts.  And its
    /// middle is the crossing of the first cut's tunnels, so a ray down any axis passes clean through, and the middle
    /// is at least a third from the solid; outside the cube, the distance is the distance to it.
    /// </summary>
    [TestMethod]
    public void TestAMengerSpongeKeepsTwentyOfEveryTwentySeven()
    {
        for (int cuts = 1; cuts <= 4; cuts++)
        {
            MengerSponge sponge = new () { Iterations = cuts };
            Random random = new (cuts);
            int inside = 0;
            const int Count = 60000;

            sponge.PrepareForRendering();

            for (int index = 0; index < Count; index++)
            {
                if (DistanceAt(sponge, random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1,
                        random.NextDouble() * 2 - 1) < 0)
                    inside++;
            }

            double expected = Math.Pow(20.0 / 27, cuts);

            Assert.AreEqual(expected, (double) inside / Count, expected * 0.015, $"{cuts} cuts");

            Assert.AreEqual(0, Crossings(sponge, new Ray(new Point(-3, 0, 0), new Vector(1, 0, 0))).Count,
                $"down the X axis through {cuts} cuts");
            Assert.AreEqual(0, Crossings(sponge, new Ray(new Point(0, -3, 0), new Vector(0, 1, 0))).Count,
                $"down the Y axis through {cuts} cuts");
            Assert.IsTrue(DistanceAt(sponge, 0, 0, 0) >= 1.0 / 3 - 1e-12,
                "the middle should be a third from the solid");
            Assert.AreEqual(2, DistanceAt(sponge, 3, 0.5, 0.5), 1e-12, "outside the cube, the distance to it");
        }

        MengerSponge cube = new () { Iterations = 0 };

        cube.PrepareForRendering();
        Assert.AreEqual(2, Crossings(cube, new Ray(new Point(-3, 0, 0), new Vector(1, 0, 0))).Count,
            "with no cuts the sponge is the cube");
    }

    /// <summary>
    /// A shape that reaches past the box it is bounded by is cut by it, the box's faces its surface there: a ray
    /// that meets the shape first at the box's near face goes in there, and comes out at the far one.  (Until the
    /// march recorded a start inside, the near face was passed unrecorded, the ray went in nowhere, and a ball cut
    /// by its box showed its own hollow where the cut face should be.)  Each crossing at the box faces as the box's
    /// face does.
    /// </summary>
    [TestMethod]
    public void TestAShapeCutByItsBoxIsEnteredAtTheBox()
    {
        MengerSponge cube = new ()
        {
            Iterations = 0,
            BoundingBox = new BoundingBox().Add(new Point(-0.5, -0.5, -0.5)).Add(new Point(0.5, 0.5, 0.5))
        };

        cube.PrepareForRendering();

        List<double> crossings = Crossings(cube, new Ray(new Point(-3, 0, 0), new Vector(1, 0, 0)));

        Assert.AreEqual(2, crossings.Count, string.Join(", ", crossings));
        // Within two accuracies: the box stands a whisker outside the faces it was given.
        Assert.AreEqual(2.5, crossings[0], 2e-4, $"in at the box's near face: {string.Join(", ", crossings)}");
        Assert.AreEqual(3.5, crossings[1], 2e-4, "out at its far face");

        // And each faces as the box's face does there, not as the field slopes: the field is a cube's, a ball's, a
        // sponge's, and the face is flat.  A ball cut by its box, facing as the ball does, was shaded as a cone.  Cut
        // by a box thin in X, met high up, the cube's own slope is up toward its top, and the box's face is not.
        MengerSponge slab = new ()
        {
            Iterations = 0,
            BoundingBox = new BoundingBox().Add(new Point(-0.2, -0.8, -0.8)).Add(new Point(0.2, 0.8, 0.8))
        };
        Ray along = new (new Point(-3, 0.6, 0.1), new Vector(1, 0, 0));
        List<Intersection> found = [];

        slab.PrepareForRendering();
        slab.AddIntersections(along, found);
        found.Sort();

        Assert.AreEqual(2, found.Count);
        Assert.AreEqual(new Vector(-1, 0, 0), slab.SurfaceNormalAt(along.At(found[0].Distance), found[0]));
        Assert.AreEqual(new Vector(1, 0, 0), slab.SurfaceNormalAt(along.At(found[^1].Distance), found[^1]));
    }

    /// <summary>
    /// A scene writes each fractal, and the numbers it gives reach the shape: each comes out on the picture, and a
    /// Mandelbulb of the third power is another shape than one of the eighth.
    /// </summary>
    [TestMethod]
    public void TestASceneWritesEachFractal()
    {
        int eighth = Covered("mandelbulb { power 8  iterations 8  detail 0.004 }");
        int third = Covered("mandelbulb { power 3  iterations 8 }");

        Assert.IsTrue(eighth > 300, $"the eighth power covered {eighth}");
        Assert.AreNotEqual(eighth, third, "the power should reach the shape");
        Assert.IsTrue(
            Covered("mandelbox { factor -1.5  inner radius 0.4  outer radius 1.1  fold 1  scale 0.5 }") > 300);
        Assert.IsTrue(Covered("menger { iterations 3 }") > 300);
    }

    /// <summary>
    /// This method renders a fractal in front of a black sky and counts the pixels it covers.
    /// </summary>
    private int Covered(string fractal)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, $"out-{Guid.NewGuid():N}.png");

        File.WriteAllText(path,
            "camera { location [0, 0, -4]  look at [0, 0, 0] }\n" +
            "point light { location [-4, 6, -6] }\n" + fractal + "\n");
        new LanguageParser(path).Parse().Render(new RenderOptions
        {
            OutputFileName = output, Width = 40, Height = 40, ProgressStyleText = "none"
        });

        Canvas canvas = new ImageFile(output).Load()[0];
        int covered = 0;

        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                Color color = canvas.GetPixel(x, y);

                if (color.Red + color.Green + color.Blue > 0.02)
                    covered++;
            }
        }

        return covered;
    }

    /// <summary>
    /// What makes no fractal is refused: a Mandelbulb's power below two, a Mandelbox's factor between minus one and
    /// one, its inner radius not inside its outer, no rounds at all, a sponge cut fewer than no times.
    /// </summary>
    [TestMethod]
    public void TestWhatMakesNoFractalIsRefused()
    {
        StringAssert.Contains(Refusal("mandelbulb { power 1.5 }"), "at least two");
        StringAssert.Contains(Refusal("mandelbulb { iterations 0 }"), "at least once");
        StringAssert.Contains(Refusal("mandelbox { factor 0.5 }"), "more than one, or less than minus one");
        StringAssert.Contains(Refusal("mandelbox { inner radius 1.2  outer radius 1 }"), "must be less than its outer");
        StringAssert.Contains(Refusal("mandelbox { detail 0 }"), "more than nought");
        StringAssert.Contains(Refusal("menger { iterations -1 }"), "fewer than no times");
    }

    private string Refusal(string fractal)
    {
        string path = Path.Combine(_directory, "refused.igl");
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        File.WriteAllText(path,
            "camera { location [0, 0, -4]  look at [0, 0, 0] }\npoint light { location [-4, 6, -6] }\n" + fractal);
        Console.SetOut(captured);

        try
        {
            new LanguageParser(path).Parse()?.Render(new RenderOptions
            {
                OutputFileName = Path.Combine(_directory, "refused.png"), Width = 4, Height = 4,
                ProgressStyleText = "none"
            });
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
        finally
        {
            Console.SetOut(was);
        }

        return captured.ToString();
    }
}
