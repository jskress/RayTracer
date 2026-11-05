using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Patterns;

namespace Tests;

/// <summary>
/// These tests cover hypertexture: the distance field it measures any solid by, the hard skin it moves by noise and
/// the soft stuff it thins out across the skin, and what a scene may and may not say of either.
/// </summary>
[TestClass]
public class TestHypertexture
{
    private string _directory;

    [TestInitialize]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"hypertexture-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    // A number from nought to one for a draw and a purpose, the same every run.
    private static double Unit(int draw, int purpose)
    {
        ulong bits = (ulong) (uint) draw * 0x9E3779B97F4A7C15UL ^ (ulong) (uint) (purpose + 7) * 0xC2B2AE3D27D4EB4FUL;

        bits = (bits ^ (bits >> 30)) * 0xBF58476D1CE4E5B9UL;
        bits = (bits ^ (bits >> 27)) * 0x94D049BB133111EBUL;
        bits ^= bits >> 31;

        return (bits >> 11) * (1.0 / (1UL << 53));
    }

    private static Point Within(int draw, double reach) => new (
        (2 * Unit(draw, 0) - 1) * reach, (2 * Unit(draw, 1) - 1) * reach, (2 * Unit(draw, 2) - 1) * reach);

    private static DistanceGrid Measured(Surface solid, double reach, double cell)
    {
        solid.PrepareForRendering();

        return DistanceGrid.Of(
            solid, new BoundingBox().Add(new Point(-reach, -reach, -reach)).Add(new Point(reach, reach, reach)), cell);
    }

    /// <summary>
    /// The field measures a ball within four tenths of a cell of its true distance everywhere in the box, inside and
    /// out (0.26 measured), and an axis-aligned box off its faces within a hair: a face is a plane, and a straight line between nodes
    /// reads a plane's distance exactly.
    /// </summary>
    [TestMethod]
    public void TestTheFieldMeasuresAShapesDistance()
    {
        const double cell = 0.05;
        DistanceGrid ball = Measured(new Sphere(), 1.6, cell);
        double worst = 0;

        for (int draw = 0; draw < 3000; draw++)
        {
            Point place = Within(draw, 1.5);
            double truth = new Vector(place).Magnitude - 1;

            worst = Math.Max(worst, Math.Abs(ball.DistanceAt(place.X, place.Y, place.Z) - truth));
        }

        Assert.IsLessThan(0.4 * cell, worst, $"a ball's distance was {worst / cell:F2} cells out");

        DistanceGrid box = Measured(new Cube { Transform = Transforms.Scale(0.8) }, 1.6, cell);
        double flat = 0;

        // Places off a face, square to it rather than off its edges or corners: there the distance is to the plane
        // alone.  (Inside, the distance creases where the nearest face changes, and is read across the crease.)
        for (int draw = 0; draw < 4000; draw++)
        {
            Point place = Within(draw, 1.4);
            double[] axes = [Math.Abs(place.X), Math.Abs(place.Y), Math.Abs(place.Z)];
            int most = Array.IndexOf(axes, axes.Max());

            if (axes[most] < 0.8 || axes.Where((_, axis) => axis != most).Any(value => value > 0.6))
                continue;

            flat = Math.Max(flat, Math.Abs(box.DistanceAt(place.X, place.Y, place.Z) - (axes[most] - 0.8)));
        }

        Assert.IsLessThan(0.05 * cell, flat, $"a face's distance was {flat / cell:F2} cells out");
    }

    /// <summary>
    /// A box whose faces lie exactly along lines of the grid -- a half either side, nodes every eighth from minus one
    /// -- is measured as well as any: a line running in a face is outvoted by the two lines across it at every node.
    /// </summary>
    [TestMethod]
    public void TestAFaceOnAGridLineIsMeasuredToo()
    {
        DistanceGrid box = Measured(new Cube { Transform = Transforms.Scale(0.5) }, 1, 0.125);

        for (int draw = 0; draw < 2000; draw++)
        {
            Point place = Within(draw, 0.95);
            double[] axes = [Math.Abs(place.X), Math.Abs(place.Y), Math.Abs(place.Z)];
            int most = Array.IndexOf(axes, axes.Max());
            double measured = box.DistanceAt(place.X, place.Y, place.Z);

            Assert.IsTrue(double.IsFinite(measured), $"{place} measured {measured}");

            if (axes[most] > 0.5 && axes.Where((_, axis) => axis != most).All(value => value < 0.35))
                Assert.AreEqual(axes[most] - 0.5, measured, 0.01, $"{place} off a face");
        }
    }

    /// <summary>
    /// A ball that lies along X -- reporting, to every ray along that axis, a run inside it that is not there -- is
    /// still measured as a ball: each node is signed by two lines of the three through it, not by any one.
    /// </summary>
    [TestMethod]
    public void TestANodeIsSignedByTwoLinesInThree()
    {
        DistanceGrid grid = Measured(new LyingBall(), 1.6, 0.05);

        for (int draw = 0; draw < 200; draw++)
        {
            Point place = new (1.32 + 0.16 * Unit(draw, 10), 0.6 * (Unit(draw, 11) - 0.5), 0.6 * (Unit(draw, 12) - 0.5));

            Assert.IsGreaterThan(0, grid.DistanceAt(place.X, place.Y, place.Z), $"{place} came out inside");
        }
    }

    /// <summary>
    /// This class is a ball that lies to rays along X, reporting a run inside it from x = 1.3 to x = 1.5.
    /// </summary>
    private sealed class LyingBall : Sphere
    {
        public override void AddIntersections(Ray ray, List<Intersection> intersections)
        {
            base.AddIntersections(ray, intersections);

            if (Math.Abs(ray.Direction.X) > 0.99 && Math.Abs(ray.Origin.Y) < 0.9 && Math.Abs(ray.Origin.Z) < 0.9)
            {
                intersections.Add(new Intersection(this, (1.3 - ray.Origin.X) / ray.Direction.X));
                intersections.Add(new Intersection(this, (1.5 - ray.Origin.X) / ray.Direction.X));
            }
        }
    }

    /// <summary>
    /// A group is measured as the union of its parts, as overlapping parts are inside wherever any one is: two balls
    /// overlapping put their middle 0.866 inside the skin, where counting crossings of the group as one surface -- four
    /// along a line through both -- puts it outside.  A CSG is measured as what it is: a ball's worth cut out of a box
    /// is outside within the cut.
    /// </summary>
    [TestMethod]
    public void TestTheFieldMeasuresGroupsAsUnionsAndCsgAsItIs()
    {
        Group pair = new ();

        pair.Add(new Sphere { Transform = Transforms.Translate(-0.5, 0, 0) });
        pair.Add(new Sphere { Transform = Transforms.Translate(0.5, 0, 0) });

        DistanceGrid both = Measured(pair, 2, 0.04);

        Assert.AreEqual(-0.866, both.DistanceAt(0, 0, 0), 0.03, "the middle of two overlapping balls");
        Assert.AreEqual(-0.5, both.DistanceAt(1, 0, 0), 0.03, "half way into one ball");
        Assert.AreEqual(0.5, both.DistanceAt(2, 0, 0), 0.03, "half a unit off the far side");

        CsgSurface cut = new ()
        {
            Operation = CsgOperation.Difference,
            Left = new Cube(),
            Right = new Sphere { Transform = Transforms.Translate(0, 1, 0) * Transforms.Scale(0.6) }
        };
        DistanceGrid carved = Measured(cut, 1.5, 0.04);

        Assert.AreEqual(0.4, carved.DistanceAt(0, 0.8, 0), 0.03, "inside the cut, 0.4 from its floor");
        Assert.AreEqual(-0.4, carved.DistanceAt(0, -0.6, 0), 0.03, "inside the box, 0.4 from its bottom");
    }

    /// <summary>
    /// A hypertexture that reaches nowhere is its solid; one that reaches r has every crossing within r of the solid's
    /// skin, either side; eroded, every crossing within r inside it -- and the noise, stretched over its middle 96
    /// percent, takes the skin out to its reach and in to its reach, granite though it is, whose own values all lie low.
    /// </summary>
    [TestMethod]
    public void TestTheSkinMovesAsFarAsItsReach()
    {
        foreach ((double reach, bool eroded, double least, double most) in new[]
                 {
                     (0.0, false, 0.0, 0.0), (0.15, false, -0.15, 0.15), (0.15, true, -0.15, 0.0)
                 })
        {
            Hypertexture shape = new ()
            {
                Solid = new Sphere(), Reach = reach, Eroded = eroded, Resolution = 64,
                Noise = new DensityShape { Pattern = new GranitePattern(), Transform = Transforms.Scale(0.4) }
            };

            shape.PrepareForRendering();

            // The field itself: the skin moved in by up to the reach and, unless eroded, out by as much, both ends met.
            double movedIn = 0, movedOut = 0;

            for (int draw = 0; draw < 3000; draw++)
            {
                Point place = new Point(0, 0, 0) + RandomWay(draw) * (0.85 + 0.3 * Unit(draw, 13));
                double moved = shape.FieldAt(place) - shape.SkinDistanceAt(place);

                movedIn = Math.Max(movedIn, moved);
                movedOut = Math.Max(movedOut, -moved);
            }

            Assert.AreEqual(reach, movedIn, 1e-9, $"reach {reach}, eroded {eroded}: the skin moved in by {movedIn}");
            Assert.AreEqual(eroded ? 0 : reach, movedOut, 1e-9,
                $"reach {reach}, eroded {eroded}: the skin moved out by {movedOut}");

            double slack = 0.7 * shape.Cell + 1e-3;
            double low = double.MaxValue, high = double.MinValue;

            for (int draw = 0; draw < 400; draw++)
            {
                Point from = new Point(0, 0, 0) + RandomWay(draw) * 3;
                Vector toward = (new Point(0, 0, 0) - from).Unit;

                foreach (Intersection crossing in Crossings(shape, new Ray(from, toward)))
                {
                    double off = new Vector(from + toward * crossing.Distance).Magnitude - 1;

                    low = Math.Min(low, off);
                    high = Math.Max(high, off);
                }
            }

            // Moved, the skin should reach most of the way out to its reach and back.
            Assert.IsGreaterThanOrEqualTo(least - slack, low, $"reach {reach}, eroded {eroded}: in as far as {low}");
            Assert.IsLessThanOrEqualTo(most + slack, high, $"reach {reach}, eroded {eroded}: out as far as {high}");
            Assert.IsLessThan(least + 0.4 * reach + slack, low, $"reach {reach}, eroded {eroded}: never in past {low}");
            Assert.IsGreaterThan(most - 0.4 * reach - slack, high, $"reach {reach}, eroded {eroded}: never out past {high}");
        }
    }

    private static Vector RandomWay(int draw)
    {
        double z = 2 * Unit(draw, 3) - 1;
        double turn = 2 * Math.PI * Unit(draw, 4);
        double r = Math.Sqrt(1 - z * z);

        return new Vector(r * Math.Cos(turn), r * Math.Sin(turn), z);
    }

    private static List<Intersection> Crossings(Surface shape, Ray ray)
    {
        List<Intersection> found = [];

        shape.AddIntersections(ray, found);

        return found.Where(crossing => crossing.Distance > 0).OrderBy(crossing => crossing.Distance).ToList();
    }

    /// <summary>
    /// A ray in comes out: crossings come in pairs, each pair a way in and a way out, and the skin faces out where it is
    /// met -- by its normal against the ray, every way in facing the ray and every way out away from it.  So does a
    /// soft hypertexture's shell, every one: given the noise's slope as well as its own, it faced off at random and
    /// came out in black blots where rays left it on the wrong side.
    /// </summary>
    [TestMethod]
    public void TestARayThatGoesInComesOut()
    {
        RaysGoInAndComeOut(new Hypertexture { Solid = new Sphere(), Reach = 0.2, Resolution = 64 }, 0.97);
        RaysGoInAndComeOut(
            new Hypertexture
            {
                Solid = new Sphere(), Reach = 0.2, Soft = 0.05, Drawn = new Vector(0, 0.5, 0), Resolution = 64
            }, 1);
    }

    private static void RaysGoInAndComeOut(Hypertexture shape, double share)
    {
        shape.PrepareForRendering();

        int odd = 0, facing = 0, counted = 0;

        for (int draw = 0; draw < 300; draw++)
        {
            Point from = new Point(0, 0, 0) + RandomWay(draw) * 3;
            Vector toward = (new Point(Unit(draw, 5) - 0.5, 0, 0) - from).Unit;
            List<Intersection> crossings = Crossings(shape, new Ray(from, toward));

            if (crossings.Count % 2 == 1)
                odd++;

            for (int at = 0; at < crossings.Count; at++)
            {
                Point place = from + toward * crossings[at].Distance;
                double dot = shape.SurfaceNormalAt(place, crossings[at]).Dot(toward);

                counted++;

                if (at % 2 == 0 ? dot < 0 : dot > 0)
                    facing++;
            }
        }

        Assert.AreEqual(0, odd, "rays went in and never came out");
        Assert.IsGreaterThanOrEqualTo(share * counted, facing, $"only {facing} of {counted} crossings faced the right way");
    }

    /// <summary>
    /// Drawn out along a vector, the skin reaches out that way where it faces it, and nowhere else: a ball drawn up by
    /// half a unit reaches above its top, toward the half unit where the noise is lowest, and its bottom stays put.
    /// </summary>
    [TestMethod]
    public void TestTheSkinIsDrawnOutTheWayItIsAsked()
    {
        Hypertexture shape = new () { Solid = new Sphere(), Reach = 0, Drawn = new Vector(0, 0.5, 0), Resolution = 64 };

        shape.PrepareForRendering();

        double top = double.MinValue, bottom = double.MaxValue;

        for (int column = 0; column < 200; column++)
        {
            double x = (2 * Unit(column, 6) - 1) * 0.3, z = (2 * Unit(column, 7) - 1) * 0.3;
            List<Intersection> down = Crossings(shape, new Ray(new Point(x, 3, z), new Vector(0, -1, 0)));

            top = Math.Max(top, 3 - down[0].Distance);
            bottom = Math.Min(bottom, 3 - down[^1].Distance);
        }

        Assert.IsGreaterThan(1.3, top, $"the top reached only {top}");
        Assert.IsLessThan(1.55, top, $"the top reached {top}, past the half unit it was drawn");
        Assert.AreEqual(-1, bottom, 0.05, "the bottom should stay where the ball's is");
    }

    /// <summary>
    /// Soft, the stuff is whole deep inside, nothing well outside, and thins out smoothly between; and its shell holds
    /// all of it, so that just outside every crossing of the shell there is none.
    /// </summary>
    [TestMethod]
    public void TestSoftStuffThinsOutAcrossTheSkin()
    {
        Hypertexture shape = new () { Solid = new Sphere(), Reach = 0.1, Soft = 0.1, Resolution = 64 };

        shape.PrepareForRendering();

        Assert.AreEqual(1, shape.StuffAt(new Point(0, 0, 0)), 1e-9, "deep inside");
        Assert.AreEqual(0, shape.StuffAt(new Point(0, 1.3, 0)), 1e-9, "well outside");

        // Along a line out through the skin, the stuff never grows, and falls from whole to nothing.
        double previous = 1;
        bool between = false;

        for (double r = 0.6; r <= 1.3; r += 0.005)
        {
            double stuff = shape.StuffAt(new Point(r * 0.6, r * 0.8, 0));

            Assert.IsLessThanOrEqualTo(previous + 0.15, stuff, $"the stuff jumped up at {r}");
            between |= stuff is > 0.1 and < 0.9;
            previous = stuff;
        }

        Assert.IsTrue(between, "the stuff should thin out across the skin, not stop");

        // Drawn out, the stuff thins from root to tip: half way up the drawing it is neither whole nor gone.  Thinned
        // over the soft width alone, a flame was whole stuff to its tip.
        Hypertexture flame = new ()
        {
            Solid = new Sphere(), Reach = 0, Soft = 0.02, Drawn = new Vector(0, 0.8, 0), Resolution = 64
        };

        flame.PrepareForRendering();

        List<double> halfWay = Enumerable.Range(0, 49)
            .Select(at => flame.StuffAt(new Point(0.03 * (at % 7 - 3), 1.2, 0.03 * (at / 7 - 3))))
            .Where(stuff => stuff > 0.02)
            .ToList();

        Assert.IsGreaterThan(5, halfWay.Count, "half way up, the flame should be there");
        Assert.IsGreaterThan(0.8 * halfWay.Count, halfWay.Count(stuff => stuff < 0.9),
            $"half way up, the flame was whole at {halfWay.Count(stuff => stuff >= 0.9)} of {halfWay.Count} places");

        for (int draw = 0; draw < 200; draw++)
        {
            Point from = new Point(0, 0, 0) + RandomWay(draw) * 3;
            Vector toward = (new Point(0, 0, 0) - from).Unit;

            List<Intersection> crossings = Crossings(shape, new Ray(from, toward));

            // Just outside the shell -- before a way in, after a way out -- there is nothing.
            for (int at = 0; at < crossings.Count; at++)
            {
                Point place = from + toward * crossings[at].Distance;
                Point outside = at % 2 == 0 ? place - toward * 0.005 : place + toward * 0.005;

                Assert.AreEqual(0, shape.StuffAt(outside), 1e-9, "there should be no stuff outside the shell");
            }
        }
    }

    /// <summary>
    /// The march misses nothing it can see, the field being no distance once noise is in it: along each ray, every run
    /// a quarter of a cell long or more that a walk in steps of a fortieth of a cell finds inside the moved skin, or
    /// outside it between pieces, the march agrees about at the run's middle.  Finer than that a granite's last octaves
    /// raise slivers a thousandth thick, finer than the field knows the solid or a pixel shows, and the march may step
    /// over those as it may over any detail below what it is asked to.  Striding through the inside as an escape-time
    /// fractal's march must, it leapt the gaps between the pieces of a rough skin and filled them in.
    /// </summary>
    [TestMethod]
    public void TestTheMarchMissesNothingItCanSee()
    {
        Hypertexture shape = new ()
        {
            Solid = new Sphere(), Reach = 0.12, Resolution = 64,
            Noise = new DensityShape { Pattern = new GranitePattern(), Transform = Transforms.Scale(0.15) }
        };

        shape.PrepareForRendering();

        double step = shape.Cell / 40;
        int wrong = 0, runs = 0;

        for (int draw = 0; draw < 120; draw++)
        {
            Point from = new Point(0, 0, 0) + RandomWay(draw) * 2;
            Vector toward = (new Point(0.6 * (Unit(draw, 8) - 0.5), 0.6 * (Unit(draw, 9) - 0.5), 0) - from).Unit;
            List<double> crossings = Crossings(shape, new Ray(from, toward)).Select(c => c.Distance).ToList();
            bool inside = false;
            double began = 0;

            for (double t = 0; t < 4; t += step)
            {
                bool now = shape.FieldAt(from + toward * t) < 0;

                if (now == inside)
                    continue;

                // A run just ended; the first one, before the skin, is outside and agreed about by any march.
                if (t - began >= shape.Cell / 4 && began > 0)
                {
                    double middle = (began + t) / 2;
                    bool marchedInside = crossings.Count(crossing => crossing < middle) % 2 == 1;

                    runs++;

                    if (marchedInside != inside)
                        wrong++;
                }

                inside = now;
                began = t;
            }
        }

        Assert.IsGreaterThan(300, runs, "the walks should find the skin");
        Assert.AreEqual(0, wrong, $"the march disagreed about {wrong} of {runs} runs a quarter cell long");
    }

    /// <summary>
    /// The same hypertexture measured twice is the same, the field built in parallel though it is.
    /// </summary>
    [TestMethod]
    public void TestTheFieldIsTheSameEveryTime()
    {
        Group pair = new ();

        pair.Add(new Cube { Transform = Transforms.Translate(-0.4, 0, 0) });
        pair.Add(new Sphere { Transform = Transforms.Translate(0.6, 0.3, 0) });

        DistanceGrid first = Measured(pair, 2.2, 0.05);
        DistanceGrid second = Measured(pair, 2.2, 0.05);

        for (int draw = 0; draw < 500; draw++)
        {
            Point place = Within(draw, 2);

            Assert.AreEqual(first.DistanceAt(place.X, place.Y, place.Z), second.DistanceAt(place.X, place.Y, place.Z));
        }
    }

    /// <summary>
    /// A scene writes both kinds, hard and soft, and each reaches the picture: the hard one as a lit solid, the soft
    /// one as glowing stuff in the dark, its shell clear.
    /// </summary>
    [TestMethod]
    public void TestASceneWritesBothKinds()
    {
        int hard = Covered("""
            hypertexture { solid { sphere { } }  reach 0.1  noise granite { scale 0.4 }  material { pigment White } }
            """);
        int soft = Covered("""
            hypertexture {
                solid { cube { scale 0.7 } }
                soft 0.08  reach 0.1  drawn [0, 0.3, 0]
                medium { density 3  emission [1, 0.6, 0.2] }
                no shadow
            }
            """);

        Assert.IsGreaterThan(300, hard, $"the hard one covered {hard}");
        Assert.IsGreaterThan(300, soft, $"the soft one covered {soft}");
    }

    /// <summary>
    /// Every property a scene writes reaches the hypertexture: each one changed from a plain one changes the picture.
    /// </summary>
    [TestMethod]
    public void TestEveryPropertyReachesTheSkin()
    {
        const string Plain = "hypertexture { solid { sphere { } }  reach 0.1  noise bozo { scale 0.3 }  material { pigment White } }";
        Canvas plain = Rendered(Plain);

        foreach ((string from, string to) in new[]
                 {
                     ("reach 0.1", "reach 0.2"), ("noise bozo { scale 0.3 }", "noise granite { scale 0.3 }"),
                     ("reach 0.1", "reach 0.1  eroded"), ("reach 0.1", "reach 0.1  drawn [0, 0.5, 0]"),
                     ("reach 0.1", "reach 0.1  resolution 12"), ("{ sphere { } }", "{ cube { scale 0.8 } }")
                 })
        {
            Canvas changed = Rendered(Plain.Replace(from, to));
            int differing = 0;

            for (int y = 0; y < plain.Height; y++)
            {
                for (int x = 0; x < plain.Width; x++)
                {
                    Color a = plain.GetPixel(x, y), b = changed.GetPixel(x, y);

                    if (Math.Abs(a.Red - b.Red) + Math.Abs(a.Green - b.Green) + Math.Abs(a.Blue - b.Blue) > 0.03)
                        differing++;
                }
            }

            Assert.IsGreaterThan(20, differing, $"'{to}' changed only {differing} pixels");
        }
    }

    private Canvas Rendered(string surface)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, $"out-{Guid.NewGuid():N}.png");

        File.WriteAllText(path,
            "camera { location [0, 0, -4]  look at [0, 0, 0] }\npoint light { location [-4, 6, -6] }\n" + surface);
        new LanguageParser(path).Parse().Render(new RenderOptions
        {
            OutputFileName = output, Width = 40, Height = 40, ProgressStyleText = "none"
        });

        return new ImageFile(output).Load()[0];
    }

    private int Covered(string surface)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, $"out-{Guid.NewGuid():N}.png");

        File.WriteAllText(path,
            "camera { location [0, 0, -4]  look at [0, 0, 0] }\npoint light { location [-4, 6, -6] }\n" + surface);
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

                if (color.Red + color.Green + color.Blue > 0.05)
                    covered++;
            }
        }

        return covered;
    }

    /// <summary>
    /// What makes no hypertexture is refused: no solid, a solid with no end, a soft one with no medium or with a
    /// material, a medium without softness, a medium with a shape of its own, too coarse a field.
    /// </summary>
    [TestMethod]
    public void TestWhatMakesNoHypertextureIsRefused()
    {
        StringAssert.Contains(Refusal("hypertexture { reach 0.1 }"), "needs a solid");
        StringAssert.Contains(Refusal("hypertexture { solid { plane { } } }"), "must end somewhere");
        StringAssert.Contains(Refusal("hypertexture { solid { sphere { } }  soft 0.1 }"), "needs a medium");
        StringAssert.Contains(Refusal("hypertexture { solid { sphere { } }  medium { density 2 } }"), "Only a soft");
        StringAssert.Contains(
            Refusal("hypertexture { solid { sphere { } }  soft 0.1  medium { density 2 }  material { pigment Red } }"),
            "takes no material");
        StringAssert.Contains(
            Refusal("hypertexture { solid { sphere { } }  soft 0.1  medium { density granite { scale 0.2 } } }"),
            "cannot have a density function or pattern");
        StringAssert.Contains(Refusal("hypertexture { solid { sphere { } }  resolution 4 }"), "at least 8");
        StringAssert.Contains(
            Refusal("hypertexture { solid { sphere { } }  soft 0  medium { density 2 } }"), "more than nought");
    }

    private string Refusal(string surface)
    {
        string path = Path.Combine(_directory, "refused.igl");
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        File.WriteAllText(path,
            "camera { location [0, 0, -4]  look at [0, 0, 0] }\npoint light { location [-4, 6, -6] }\n" + surface);
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
