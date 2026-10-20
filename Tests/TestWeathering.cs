using Lex.Parser;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Patterns;
using RayTracer.Pigments;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the weathering patterns, which look about a place rather than at a point: how closed in
/// it is, how far it stands out, which way it faces.  Each is held to the same question asked of the geometry
/// directly, summed here over every way out of the place.
/// </summary>
[TestClass]
public class TestWeathering
{
    // A surface glowing as bright as the pattern's value: nought black, one white.
    private static Material Showing(Pattern pattern)
    {
        PatternPigment pigment = new () { Pattern = pattern, PigmentSet = new PigmentSet() };

        pigment.PigmentSet.AddEntry(SolidPigment.Black);
        pigment.PigmentSet.AddEntry(SolidPigment.White, 1);

        return new Material
        {
            Pigment = pigment, Ambient = 0, Diffuse = 0, Specular = 0, Emission = Colors.White
        };
    }

    private static Material Plain() => new ()
    {
        Pigment = new SolidPigment(Colors.Gray), Ambient = 0, Diffuse = 0, Specular = 0
    };

    private static Scene SceneOf(int samples, params Surface[] surfaces)
    {
        Scene scene = new () { Background = new SolidPigment(Colors.Black), WeatheringSamples = samples };

        foreach (Surface surface in surfaces)
        {
            surface.PrepareForRendering();
            scene.Surfaces.Add(surface);
        }

        return scene;
    }

    // What the first thing a ray meets shows.
    private static double Seen(Scene scene, Point from, Vector toward)
    {
        Ray ray = new (from, toward.Unit);
        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.Hit();

        hit.PrepareUsing(ray, hits);

        return scene.GetHitColor(hit, 1).Red;
    }

    // A wall standing at x = h, square to the floor.
    private static Plane WallAt(double h, Material material = null) => new ()
    {
        Material = material ?? Plain(), Transform = Transforms.Translate(h, 0, 0) * Transforms.RotateAroundZ(90)
    };

    /// <summary>
    /// This method works out how closed in a place on a floor is, the floor's normal up, with walls at the
    /// given distances along +X (and -X, if given): each way out of it spread as a matte surface spreads
    /// light, and counted by how near it meets a wall within the reach.
    /// </summary>
    private static double ClosedIn(double reach, double toTheRight, double? toTheLeft = null)
    {
        const int rings = 1200;
        const int spokes = 1200;
        double total = 0;

        for (int ring = 0; ring < rings; ring++)
        {
            double theta = (ring + 0.5) / rings * Math.PI / 2;

            for (int spoke = 0; spoke < spokes; spoke++)
            {
                double phi = (spoke + 0.5) / spokes * 2 * Math.PI;
                double x = Math.Sin(theta) * Math.Cos(phi);
                double distance = x > 0 ? toTheRight / x : toTheLeft is { } left && x < 0 ? left / -x : double.PositiveInfinity;
                double counted = distance < reach ? 1 - distance / reach : 0;

                total += counted * Math.Cos(theta) * Math.Sin(theta) * (Math.PI / 2 / rings) * (2 * Math.PI / spokes);
            }
        }

        return total / Math.PI;
    }

    /// <summary>
    /// This tests the crevices of open ground, a floor beside a wall, and the foot of a groove between two:
    /// nothing at all in the open, and beside walls what the walls close off, by how near they stand.
    /// </summary>
    [TestMethod]
    public void TestAPlaceIsAsClosedInAsWhatStandsRoundIt()
    {
        CrevicesPattern crevices = new () { Radius = 1 };
        Point above = new (0, 2, -1);
        Vector down = new Point(0, 0, 0) - above;

        Assert.AreEqual(0, Seen(SceneOf(256, new Plane { Material = Showing(crevices) }), above, down), 1e-12, "open");

        foreach (double h in new[] { 0.15, 0.3, 0.5 })
        {
            double expected = ClosedIn(1, h);
            double seen = Seen(SceneOf(2048, new Plane { Material = Showing(crevices) }, WallAt(h)), above, down);

            Assert.IsGreaterThan(0.005, expected, $"nothing to compare at {h}");
            Assert.AreEqual(expected, seen, 0.01 * expected + 1e-4, $"a wall {h} off: {seen} against {expected}");
        }

        double groove = ClosedIn(1, 0.1, 0.1);

        Assert.AreEqual(groove, Seen(SceneOf(2048, new Plane { Material = Showing(crevices) }, WallAt(0.1), WallAt(-0.1)), above, down),
            0.01 * groove, "the foot of a groove");
        Assert.IsGreaterThan(0.5, groove, "a narrow groove is mostly closed in");
    }

    /// <summary>
    /// This tests how far a place stands out: nothing in the middle of a big cube's top, and near its edge
    /// what looking into the cube finds coming out through its side -- which, the side being a plane square
    /// to the top at the same distance, is what a floor beside a wall that far off finds closed in.
    /// </summary>
    [TestMethod]
    public void TestAPlaceStandsOutAsFarAsItsEdgeIsNear()
    {
        EdgesPattern edges = new () { Radius = 1 };
        Cube block = new () { Material = Showing(edges), Transform = Transforms.Scale(5) };
        Scene scene = SceneOf(2048, block);

        double middle = Seen(scene, new Point(0, 7, -1), new Vector(0, -7, 1));

        Assert.AreEqual(0, middle, 1e-12, $"the middle of the top: {middle}");

        foreach (double s in new[] { 0.15, 0.4 })
        {
            double expected = ClosedIn(1, s);
            Point place = new (5 - s, 5, 0);

            Assert.AreEqual(expected, Seen(scene, place + new Vector(0, 2, -1), new Vector(0, -2, 1)), 0.01 * expected,
                $"{s} from the edge");
        }
    }

    /// <summary>
    /// This method works out how far a place on a top face stands out, the face's normal up: each way into the
    /// solid below it spread as a matte surface spreads light, and counted by how near the face it comes out
    /// through lies within the reach, times what that face says it counts for.
    /// </summary>
    private static double StandsOut(double reach, Func<Vector, (double Distance, double Counts)> leaving)
    {
        const int rings = 1200;
        const int spokes = 1200;
        double total = 0;

        for (int ring = 0; ring < rings; ring++)
        {
            double theta = (ring + 0.5) / rings * Math.PI / 2;

            for (int spoke = 0; spoke < spokes; spoke++)
            {
                double phi = (spoke + 0.5) / spokes * 2 * Math.PI;
                Vector way = new (Math.Sin(theta) * Math.Cos(phi), -Math.Cos(theta), Math.Sin(theta) * Math.Sin(phi));
                (double distance, double counts) = leaving(way);
                double counted = distance < reach ? (1 - distance / reach) * counts : 0;

                total += counted * Math.Cos(theta) * Math.Sin(theta) * (Math.PI / 2 / rings) * (2 * Math.PI / spokes);
            }
        }

        return total / Math.PI;
    }

    /// <summary>
    /// This tests a sheet: looking in from the top of a thin plate, a ray comes out of its far side at once,
    /// which is not an edge, so the middle of the plate is as flat as a thick block's; toward its rim the rays
    /// that reach the side before the far face are what stand it out, as at a block's.  The same of a sheet
    /// left by cutting a block out from under a block, as a droid's dome panels are, whose far face is the
    /// cutter's, its normal pointing into the sheet.
    /// </summary>
    [TestMethod]
    public void TestASheetStandsOutOnlyAtItsRim()
    {
        const double thickness = 0.1;
        Material showing = Showing(new EdgesPattern { Radius = 1 });
        Surface thin = new Cube { Material = showing, Transform = Transforms.Scale(5, thickness / 2, 5) };
        Surface cut = new CsgSurface
        {
            Operation = CsgOperation.Difference,
            Left = new Cube
            {
                Material = showing, Transform = Transforms.Translate(0, -1 + thickness / 2, 0) * Transforms.Scale(5, 1, 5)
            },
            Right = new Cube
            {
                Material = showing, Transform = Transforms.Translate(0, -1 - thickness / 2, 0) * Transforms.Scale(6, 1, 6)
            }
        };

        foreach ((string name, Surface plate) in new[] { ("a thin block", thin), ("a cut block", cut) })
        {
            Scene scene = SceneOf(8192, plate);
            double top = thickness / 2;

            Assert.AreEqual(0, Seen(scene, new Point(0, top + 2, -1), new Vector(0, -2, 1)), 1e-12,
                $"the middle of {name}");

            // Not much further in: there only the rays nearly along the face reach the side first, too few of them
            // to hold to a percent.
            foreach (double s in new[] { 0.15, 0.25 })
            {
                double expected = StandsOut(1, way =>
                {
                    double throughTheSide = way.X > 0 ? s / way.X : double.PositiveInfinity;
                    double throughTheBack = thickness / -way.Y;

                    return throughTheSide < throughTheBack ? (throughTheSide, 1) : (throughTheBack, 0);
                });
                Point place = new (5 - s, top, 0);

                Assert.IsGreaterThan(0.005, expected, $"nothing to compare at {s}");
                Assert.AreEqual(expected, Seen(scene, place + new Vector(0, 2, -1), new Vector(0, -2, 1)),
                    0.01 * expected, $"{name}, {s} from the rim");
            }
        }
    }

    /// <summary>
    /// This tests a ridge sharper than a right angle, a wedge of sixty degrees: the face a ray looking in
    /// comes out through is turned back the place's way by the cosine of sixty degrees, so each ray counts
    /// for a half of what it would round a square corner.
    /// </summary>
    [TestMethod]
    public void TestASharpRidgeStandsOutByHowSharpItIs()
    {
        EdgesPattern edges = new () { Radius = 1 };
        Material showing = Showing(edges);
        // The top, y = 0, and a face falling away from the ridge along Z at sixty degrees to it, both half-spaces.
        Vector slant = new (Math.Sin(Math.PI / 3), -Math.Cos(Math.PI / 3), 0);
        CsgSurface wedge = new ()
        {
            Operation = CsgOperation.Intersection,
            Left = new Plane { Material = showing },
            Right = new Plane { Material = showing, Transform = Transforms.RotateAroundZ(-120) }
        };
        Scene scene = SceneOf(2048, wedge);

        foreach (double s in new[] { 0.15, 0.4 })
        {
            double expected = StandsOut(1, way =>
            {
                double toward = way.Dot(slant);

                return toward > 0 ? (s * slant.X / toward, 0.5) : (double.PositiveInfinity, 0);
            });

            Assert.IsGreaterThan(0.005, expected, $"nothing to compare at {s}");
            Assert.AreEqual(expected, Seen(scene, new Point(-s, 2, -1), new Vector(0, -2, 1)), 0.01 * expected,
                $"{s} from the ridge");
        }
    }

    /// <summary>
    /// This tests a place on something moving, or beside it: it is looked about at the instant it was met, so a
    /// floor is closed in by a wall that has slid up to it only at the instant the wall is there, a growing
    /// model's reach grows with it, and a sliding shell is looked into where it is.
    /// </summary>
    [TestMethod]
    public void TestAPlaceIsLookedAboutWhenItIsMet()
    {
        Plane floor = new () { Material = Showing(new CrevicesPattern { Radius = 1 }) };
        Plane wall = WallAt(5);
        Scene scene = new () { Background = new SolidPigment(Colors.Black), WeatheringSamples = 2048 };

        wall.MotionAt = fraction => Transforms.Translate(-4.7 * fraction, 0, 0);

        foreach (Surface surface in new Surface[] { floor, wall })
        {
            surface.PrepareForRendering([0, 1]);
            scene.Surfaces.Add(surface);
        }

        // What is seen of a place at an instant, from above it and a little toward -Z.
        static double At(Scene scene, int timeIndex, Point place)
        {
            Ray ray = new (place + new Vector(0, 2, -1), new Vector(0, -2, 1).Unit, timeIndex);
            List<Intersection> hits = scene.Intersect(ray);
            Intersection hit = hits.Hit();

            hit.PrepareUsing(ray, hits);

            return scene.GetHitColor(hit, 1).Red;
        }

        double expected = ClosedIn(1, 0.3);

        Assert.AreEqual(0, At(scene, 0, Point.Zero), 1e-12, "with the wall far off");
        Assert.AreEqual(expected, At(scene, 1, Point.Zero), 0.01 * expected, "with the wall slid up");

        // A floor and wall drawn half size in a group growing to twice its size: the reach grows with it.
        Group growing = new () { MotionAt = fraction => Transforms.Scale(1 + fraction) };
        Scene grown = new () { Background = new SolidPigment(Colors.Black), WeatheringSamples = 2048 };

        growing.Add(new Plane { Material = Showing(new CrevicesPattern { Radius = 0.5 }) }).Add(WallAt(0.2));
        growing.PrepareForRendering([0, 1]);
        grown.Surfaces.Add(growing);

        double full = ClosedIn(1, 0.4);

        Assert.AreEqual(full, At(grown, 1, Point.Zero), 0.01 * full, "grown to twice its size");

        // A thin shell sliding along, looked into from its top: what it comes out through is where the shell is
        // then, so it is as flat there as where it set off.
        Material showing = Showing(new EdgesPattern { Radius = 1 });
        CsgSurface shell = new ()
        {
            Operation = CsgOperation.Difference,
            Left = new Sphere { Material = showing },
            Right = new Sphere { Material = showing, Transform = Transforms.Scale(0.9) },
            MotionAt = fraction => Transforms.Translate(3 * fraction, 0, 0)
        };
        Scene sliding = new () { Background = new SolidPigment(Colors.Black), WeatheringSamples = 2048 };

        shell.PrepareForRendering([0, 1]);
        sliding.Surfaces.Add(shell);

        double setOff = At(sliding, 0, new Point(0, 1, 0));

        // Not quite nought: the far face of a ball's shell turns a little toward rays slanting through it.
        Assert.IsLessThan(0.15, setOff, $"the top of a thin shell: {setOff}");
        Assert.AreEqual(setOff, At(sliding, 1, new Point(3, 1, 0)), 0.01, "once it has slid along");
    }

    /// <summary>
    /// This tests a place met by light before the eye looks at anything: a metal floor whose color is how closed
    /// in it is turns light onto the wall beside it by how closed in the wall makes it, which it can know only by
    /// looking about in its own scene -- not in none, nor in whatever scene the thread shaded last, where the
    /// floor is open and black.  A black metal still mirrors a little (POV's metallic tint is never quite black),
    /// so it is held to a floor that is black wherever it is.
    /// </summary>
    [TestMethod]
    public void TestPhotonsAreTurnedByWhereAPlaceIs()
    {
        double Landed(Pigment pigment)
        {
            Material metal = new ()
            {
                Pigment = pigment, Ambient = 0, Diffuse = 0, Specular = 0, Reflective = 1, Metallic = 1
            };
            Scene scene = SceneOf(
                64,
                new Cube { Material = metal, Transform = Transforms.Translate(0, -0.5, 0) * Transforms.Scale(3, 0.5, 3) },
                new Cube
                {
                    Material = new Material { Pigment = new SolidPigment(Colors.White), Ambient = 0, Specular = 0 },
                    Transform = Transforms.Translate(0.8, 2, 0) * Transforms.Scale(0.5, 2, 3)
                });

            scene.Lights.Add(new PointLight { Location = new Point(-2, 3, 0) });
            scene.CausticPhotons = 50_000;
            scene.ShootCausticPhotons();

            return scene.Caustics.Photons.Sum(photon => (double) photon.Red);
        }

        PatternPigment closedIn = new () { Pattern = new CrevicesPattern { Radius = 1 }, PigmentSet = new PigmentSet() };

        closedIn.PigmentSet.AddEntry(SolidPigment.Black);
        closedIn.PigmentSet.AddEntry(SolidPigment.White, 1);

        double black = Landed(SolidPigment.Black);
        double looked = Landed(closedIn);

        Assert.IsGreaterThan(0, black, "a black metal still mirrors a little");
        Assert.IsGreaterThan(2 * black, looked, $"the floor beside the wall: {looked} against {black} for black");
    }

    /// <summary>
    /// This tests which way a place faces, round a ball: up at its top, square at its side, down at its foot,
    /// and toward another direction where asked.
    /// </summary>
    [TestMethod]
    public void TestAPlaceFacesTheWayItsSurfaceDoes()
    {
        Scene up = SceneOf(1, new Sphere { Material = Showing(new FacingPattern()) });
        Scene right = SceneOf(1, new Sphere { Material = Showing(new FacingPattern { Toward = new Vector(2, 0, 0) }) });

        Assert.AreEqual(1, Seen(up, new Point(0, 5, 0), new Vector(0, -1, 0)), 1e-9, "the top");
        Assert.AreEqual(0.5, Seen(up, new Point(5, 0, 0), new Vector(-1, 0, 0)), 1e-9, "the side");
        Assert.AreEqual(0, Seen(up, new Point(0, -5, 0), new Vector(0, 1, 0)), 1e-9, "the foot");
        Assert.AreEqual(1, Seen(right, new Point(5, 0, 0), new Vector(-1, 0, 0)), 1e-9, "facing +X");
        Assert.AreEqual(0.5 + 0.5 * Math.Sqrt(0.5), Seen(right, new Point(5, 5, 0), new Vector(-1, -1, 0)), 1e-9,
            "half way round");
    }

    /// <summary>
    /// This tests the reach, in the units of the space a surface stands in: a floor and a wall drawn half the
    /// size in a group scaled up twice, looked at with half the reach, are closed in exactly as they are drawn
    /// full size -- while a floor scaled up by its own transform is looked at with the scene's reach still.
    /// </summary>
    [TestMethod]
    public void TestTheReachIsInTheUnitsAShapeStandsIn()
    {
        Point above = new (0, 2, -1);
        Vector down = new Point(0, 0, 0) - above;
        double full = Seen(
            SceneOf(256, new Plane { Material = Showing(new CrevicesPattern { Radius = 1 }) }, WallAt(0.4)), above, down);
        double scaled = Seen(SceneOf(256, new Group { Transform = Transforms.Scale(2) }
            .Add(new Plane { Material = Showing(new CrevicesPattern { Radius = 0.5 }) }).Add(WallAt(0.2))), above, down);

        Assert.IsGreaterThan(0.01, full, "something to compare");
        Assert.AreEqual(full, scaled, 1e-9 * full, "in a scaled group");

        double stretched = Seen(SceneOf(256,
            new Plane { Material = Showing(new CrevicesPattern { Radius = 1 }), Transform = Transforms.Scale(3) },
            WallAt(0.4)), above, down);

        Assert.AreEqual(full, stretched, 1e-9 * full, "scaled by its own transform");
    }

    /// <summary>
    /// This tests what does not close a place in: something that casts no shadow, as a stand-in does, is not
    /// there to gather dirt against.
    /// </summary>
    [TestMethod]
    public void TestAStandInClosesNothingIn()
    {
        Plane wall = WallAt(0.2);

        wall.NoShadow = true;

        Assert.AreEqual(0, Seen(SceneOf(256, new Plane { Material = Showing(new CrevicesPattern { Radius = 1 }) }, wall),
            new Point(0, 2, -1), new Vector(0, -2, 1)), 1e-12);
    }

    /// <summary>
    /// This tests that a place is worth the same every time it is asked about, however often its pigment is
    /// asked -- once for each light -- and in whatever order places are asked; and that what was worked out
    /// for one scene is not taken for another's.
    /// </summary>
    [TestMethod]
    public void TestAPlaceIsWorthTheSameEveryTime()
    {
        Scene scene = SceneOf(64, new Plane { Material = Showing(new CrevicesPattern { Radius = 1 }) }, WallAt(0.3));
        Point here = new (0, 2, -1);
        Point there = new (0.1, 2, -0.6);
        double first = Seen(scene, here, new Point(0, 0, 0) - here);

        Seen(scene, there, new Point(0.1, 0, 0.2) - there);

        Assert.AreEqual(first, Seen(scene, here, new Point(0, 0, 0) - here));

        // The same floor in two scenes, beside a wall in one and not in the other, is two places.
        Plane floor = new () { Material = Showing(new CrevicesPattern { Radius = 1 }) };
        Scene open = SceneOf(64, floor);
        Scene walled = SceneOf(64, floor, WallAt(0.3));

        Assert.AreEqual(0, Seen(open, here, new Point(0, 0, 0) - here), 1e-12, "in the open");
        Assert.IsGreaterThan(0.01, Seen(walled, here, new Point(0, 0, 0) - here), "beside the wall");
    }

    /// <summary>
    /// This tests the words a scene says them with, each pattern's own entry before or after its colors, and
    /// what is refused.
    /// </summary>
    [TestMethod]
    public void TestASceneSaysWhereWeatherGathers()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"weathering-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "out.png");

            // Looking straight down at a floor beside a wall, or at the top of a ball, looking about in enough
            // directions to be held to the sum; the sixty-four a scene gets unless it says otherwise come within a
            // few percent.
            double Seen(string pigment, string context = "weathering samples 1024", string shape = "plane {")
            {
                File.WriteAllText(path,
                    $"context {{ no gamma  {context} }}\n" +
                    "camera { location [0, 4, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 0.5 }\n" +
                    "background Black\n" +
                    "plane { rotate Z 90  translate X 0.4  material { pigment Gray  ambient 0  diffuse 0  specular 0 } }\n" +
                    shape + "  material { pigment " + pigment + "  ambient 0  diffuse 0  specular 0  emission 1 } }\n");

                ImageRenderer renderer = new LanguageParser(path).Parse();

                Assert.IsNotNull(renderer, $"the scene with '{pigment}' did not parse");

                renderer.Render(new RenderOptions
                {
                    OutputFileName = output, Width = 5, Height = 5, BitsPerChannel = 16, ProgressStyleText = "none"
                });

                // The middle pixel of five looks straight at the middle; of four, none does, and the floor's
                // value climbs steeply toward the wall.
                return new ImageFile(output).Load()[0].GetPixel(2, 2).Red;
            }

            double before = Seen("crevices { radius 1  [0, Black, 1, White] }");
            double after = Seen("crevices { [0, Black, 1, White]  radius 1 }");
            double closer = Seen("crevices { radius 0.3  [0, Black, 1, White] }");

            Assert.AreEqual(ClosedIn(1, 0.4), before, 0.02 * before, $"crevices, a reach of one: {before} against {ClosedIn(1, 0.4)}");
            Assert.AreEqual(before, after, 1e-9, "the reach after the colors");
            Assert.AreEqual(0, closer, 1e-9, "a wall beyond the reach");
            Assert.AreEqual(0, Seen("crevices { [0, Black, 1, White] }"), 1e-9,
                "the reach left unsaid, a quarter of a unit, short of the wall");
            Assert.AreEqual(1, Seen("facing { [0, Black, 1, White] }", shape: "sphere {"), 1e-3, "facing up");
            Assert.AreEqual(0.5, Seen("facing { toward X  [0, Black, 1, White] }", shape: "sphere {"), 1e-3, "toward X");
            Assert.AreEqual(0.5, Seen("facing { toward [1, 0, 1]  [0, Black, 1, White] }", shape: "sphere {"), 1e-3,
                "toward a direction");
            Assert.AreEqual(0, Seen("edges { radius 0.2  [0, Black, 1, White] }", shape: "cube { scale 5"), 1e-9,
                "edges, the middle of a big cube's top");
            double sixteen = Seen("crevices { radius 1  [0, Black, 1, White] }", "weathering samples 16");

            Assert.AreEqual(before, sixteen, 0.1 * before, "fewer directions, near enough");
            Assert.AreNotEqual(before, sixteen, "fewer directions, looked about in");

            Assert.ThrowsExactly<TokenException>(() => Seen("crevices { radius 0  [0, Black, 1, White] }"), "no reach");
            Assert.ThrowsExactly<TokenException>(() => Seen("crevices { [0, Black, 1, White] }", "weathering samples 0"),
                "no directions");

            // Said of the wrong pattern, an entry is refused as the scene is read, and nothing is rendered.
            TextWriter was = Console.Out;

            Console.SetOut(new StringWriter());

            try
            {
                foreach ((string pigment, string why) in new[]
                         {
                             ("facing { radius 1  [0, Black, 1, White] }", "facing has no reach"),
                             ("crevices { toward Y  [0, Black, 1, White] }", "crevices face no way")
                         })
                {
                    File.WriteAllText(path,
                        "camera { location [0, 4, 0]  look at [0, 0, 0]  up [0, 0, 1] }\n" +
                        $"plane {{ material {{ pigment {pigment} }} }}\n");

                    Assert.IsNull(new LanguageParser(path).Parse(), why);
                }
            }
            finally
            {
                Console.SetOut(was);
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
