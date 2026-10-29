using ImageMagick;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Parser;
using RayTracer.Pigments;
using RayTracer.Renderer;
using RayTracer.Options;

namespace Tests;

/// <summary>
/// These tests cover the sky seen through portals: a sky light looking into a room through the openings the
/// scene marks.  Each is held to an exact answer, or to what the sky light finds looking everywhere with many
/// more samples, since the portals only change where it looks.
/// </summary>
[TestClass]
public class TestSkyPortals
{
    // The room: four meters across X, two point six high, five along Z, its walls two millimeters thick.
    private const double Thin = 0.002;

    // Its openings: two windows in the wall at -X, and a skylight in the ceiling.
    private static readonly (double Y0, double Y1, double Z0, double Z1) WindowA = (0.9, 2.1, 0, 1.2);
    private static readonly (double Y0, double Y1, double Z0, double Z1) WindowB = (0.9, 2.1, -1.8, -0.6);
    private static readonly (double X0, double X1, double Z0, double Z1) Skylight = (0.4, 1.2, -1, 0.2);

    // A place on the floor, looked at from above.
    private static readonly Point OnTheFloor = new (0.5, 0, 0.3);

    private string _directory;

    [TestInitialize]
    public void MakeDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"sky-portal-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        Directory.Delete(_directory, true);
    }

    private static Material Matte() => new ()
    {
        Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0.9, Specular = 0
    };

    /// <summary>
    /// This helper makes a box between two corners.
    /// </summary>
    private static Cube Slab(double x0, double y0, double z0, double x1, double y1, double z1, Material material = null)
    {
        Cube cube = new ()
        {
            Material = material ?? Matte(),
            Transform = Transforms.Translate((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2) *
                        Transforms.Scale((x1 - x0) / 2, (y1 - y0) / 2, (z1 - z0) / 2)
        };

        cube.PrepareForRendering();

        return cube;
    }

    /// <summary>
    /// This helper makes a box marked as a portal that casts no shadow.
    /// </summary>
    private static Cube Marker(double x0, double y0, double z0, double x1, double y1, double z1)
    {
        Cube cube = Slab(x0, y0, z0, x1, y1, z1);

        cube.NoShadow = true;
        cube.OpensToTheSky = true;

        return cube;
    }

    /// <summary>
    /// This helper cuts openings out of a wall, each by a box a little thicker than the wall, which is marked as a
    /// portal if asked.
    /// </summary>
    private static Surface Cut(Surface wall, bool marked, params Cube[] cutters)
    {
        Surface cut = wall;

        foreach (Cube cutter in cutters)
        {
            cutter.OpensToTheSky = marked;
            cut = new CsgSurface { Operation = CsgOperation.Difference, Left = cut, Right = cutter };
        }

        cut.PrepareForRendering();

        return cut;
    }

    /// <summary>
    /// This helper builds the room under the given sky, standing on open ground, with the given openings cut and,
    /// if asked, marked as portals, and a sky light of the given samples.
    /// </summary>
    private static Scene ARoom(
        Pigment sky, int samples, bool marked, bool both = false, bool skylight = false, Material floor = null,
        Material walls = null)
    {
        Scene scene = new () { Background = sky };
        Plane ground = new () { Material = Matte(), Transform = Transforms.Translate(0, -0.01, 0) };
        List<Cube> windows = [Slab(-2 - 2 * Thin, WindowA.Y0, WindowA.Z0, -2 + Thin, WindowA.Y1, WindowA.Z1)];
        List<Cube> roof = [];

        if (both)
            windows.Add(Slab(-2 - 2 * Thin, WindowB.Y0, WindowB.Z0, -2 + Thin, WindowB.Y1, WindowB.Z1));

        if (skylight)
            roof.Add(Slab(Skylight.X0, 2.6 - Thin, Skylight.Z0, Skylight.X1, 2.6 + 2 * Thin, Skylight.Z1));

        ground.PrepareForRendering();
        scene.Surfaces.Add(ground);
        scene.Surfaces.Add(Slab(-2 - Thin, -Thin, -2.5 - Thin, 2 + Thin, 0, 2.5 + Thin, floor));
        scene.Surfaces.Add(Slab(2, 0, -2.5 - Thin, 2 + Thin, 2.6, 2.5 + Thin, walls));
        scene.Surfaces.Add(Slab(-2 - Thin, 0, 2.5, 2 + Thin, 2.6, 2.5 + Thin, walls));
        scene.Surfaces.Add(Slab(-2 - Thin, 0, -2.5 - Thin, 2 + Thin, 2.6, -2.5, walls));
        scene.Surfaces.Add(Cut(Slab(-2 - Thin, 0, -2.5 - Thin, -2, 2.6, 2.5 + Thin, walls), marked, windows.ToArray()));
        scene.Surfaces.Add(Cut(Slab(-2 - Thin, 2.6, -2.5 - Thin, 2 + Thin, 2.6 + Thin, 2.5 + Thin, walls), marked,
            roof.ToArray()));
        scene.Lights.Add(new SkyLight { Pigment = sky, Samples = samples });
        scene.OpenTheSky();

        return scene;
    }

    /// <summary>
    /// This helper shades the point a ray meets.
    /// </summary>
    private static Color Shade(Scene scene, Ray ray, int depth = 1)
    {
        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.Hit();

        Assert.IsNotNull(hit, "the ray met nothing");

        hit.PrepareUsing(ray, hits);

        return scene.GetHitColor(hit, depth);
    }

    /// <summary>
    /// This helper shades a place on a floor or a roof, looked at from straight above.
    /// </summary>
    private static Color Below(Scene scene, Point where) =>
        Shade(scene, new Ray(where + new Vector(0, 0.5, 0), Directions.Down));

    /// <summary>
    /// This helper shades sixteen places on the floor in a row about the given one, each against what it should
    /// show, and returns how far the average of their ratios to that falls from one and how far the worst of them
    /// does: a sky looked at in a few hundred directions, turned at every place, is right on average over a
    /// floor and strays a little from place to place.
    /// </summary>
    private static (double Mean, double Worst) AgainstExpected(Scene scene, Point about, Func<Point, double> expected)
    {
        double sum = 0;
        double worst = 0;

        for (int index = 0; index < 16; index++)
        {
            Point place = about + new Vector(0.012 * index - 0.09, 0, 0.011 * index - 0.08);
            double ratio = Below(scene, place).Red / expected(place);

            sum += ratio;
            worst = Math.Max(worst, Math.Abs(ratio - 1));
        }

        return (Math.Abs(sum / 16 - 1), worst);
    }

    /// <summary>
    /// This helper works out, by Lambert's formula, how much a rectangle covers of what a surface facing the
    /// given way sees, each direction weighed by how squarely it meets the surface: the integral of the cosine
    /// over the solid angle the rectangle subtends.
    /// </summary>
    private static double CosineCovered(Point point, Vector normal, params Point[] corners)
    {
        double sum = 0;

        for (int index = 0; index < corners.Length; index++)
        {
            Vector from = (corners[index] - point).Unit;
            Vector to = (corners[(index + 1) % corners.Length] - point).Unit;
            double angle = Math.Acos(Math.Clamp(from.Dot(to), -1, 1));

            sum += angle * normal.Dot(from.Cross(to).Unit);
        }

        return Math.Abs(sum) / 2;
    }

    private static double CosineThroughWindowA(Point point)
    {
        const double x = -2 - Thin / 2;

        return CosineCovered(
            point, Directions.Up, new Point(x, WindowA.Y0, WindowA.Z0), new Point(x, WindowA.Y1, WindowA.Z0),
            new Point(x, WindowA.Y1, WindowA.Z1), new Point(x, WindowA.Y0, WindowA.Z1));
    }

    private static double CosineThroughWindowB(Point point)
    {
        const double x = -2 - Thin / 2;

        return CosineCovered(
            point, Directions.Up, new Point(x, WindowB.Y0, WindowB.Z0), new Point(x, WindowB.Y1, WindowB.Z0),
            new Point(x, WindowB.Y1, WindowB.Z1), new Point(x, WindowB.Y0, WindowB.Z1));
    }

    private static double CosineThroughTheSkylight(Point point)
    {
        const double y = 2.6 + Thin / 2;

        return CosineCovered(
            point, Directions.Up, new Point(Skylight.X0, y, Skylight.Z0), new Point(Skylight.X1, y, Skylight.Z0),
            new Point(Skylight.X1, y, Skylight.Z1), new Point(Skylight.X0, y, Skylight.Z1));
    }

    /// <summary>
    /// A sky of one brightness everywhere but a small round patch -- a sun, or the bright glow about one -- where
    /// it is brighter.  It is made of no picture, so it is looked at as a smooth sky is.
    /// </summary>
    private class PatchPigment(Vector toward, double halfAngle, double patch) : Pigment
    {
        private readonly double _cos = Math.Cos(halfAngle);

        public override Color GetColorFor(Point point)
        {
            Vector heading = new Vector(point.X, point.Y, point.Z).Unit;
            double light = heading.Dot(toward.Unit) >= _cos ? patch : 1;

            return new Color(light, light, light);
        }

        public override bool Matches(Pigment other) => ReferenceEquals(this, other);
    }

    /// <summary>
    /// This tests the exact answer: under an even white sky, a white floor in a room lit through one window comes
    /// back as bright as the share of the sky it sees through it -- by Lambert's formula for the window -- times
    /// what it takes.  Looked at through the window, a couple of hundred samples settle it: right on average to
    /// a few parts in a thousand, and at every place to a few in a hundred.  Looked at everywhere, as without the
    /// portal, a place sees the window in five of them.
    /// </summary>
    [TestMethod]
    public void TestAnEvenSkyThroughAWindowLightsAFloorAsTheExactAnswerSays()
    {
        SolidPigment sky = new (Colors.White);
        Scene scene = ARoom(sky, 256, true);
        (double mean, double worst) = AgainstExpected(
            scene, OnTheFloor, place => 0.9 * CosineThroughWindowA(place) / Math.PI);

        Assert.AreEqual(1, scene.Lights.OfType<SkyLight>().Single().Openings.Count, "the window should be an opening");
        Assert.IsTrue(mean < 0.008, $"on average the floor strayed {mean} from what the window gives");
        Assert.IsTrue(worst < 0.04, $"a place on the floor strayed {worst} from what the window gives");
    }

    /// <summary>
    /// This tests the exact answer through several openings at once -- two windows in one wall, sharing a map of
    /// the sky, and a skylight in the ceiling with one of its own: the floor sees the sum of the three.
    /// </summary>
    [TestMethod]
    public void TestAnEvenSkyThroughThreeOpeningsLightsAFloorAsTheExactAnswerSays()
    {
        SolidPigment sky = new (Colors.White);
        Scene scene = ARoom(sky, 256, true, true, true);
        (double mean, double worst) = AgainstExpected(
            scene, OnTheFloor, place => 0.9 * (CosineThroughWindowA(place) + CosineThroughWindowB(place) +
                                               CosineThroughTheSkylight(place)) / Math.PI);

        Assert.AreEqual(3, scene.Lights.OfType<SkyLight>().Single().Openings.Count);
        Assert.IsTrue(mean < 0.008, $"on average the floor strayed {mean} from what the openings give");
        Assert.IsTrue(worst < 0.04, $"a place on the floor strayed {worst} from what the openings give");
    }

    /// <summary>
    /// This tests that the light of a small bright patch of sky is found through a window by looking where the
    /// light is: the sky is even but for a patch four degrees across, twenty times as bright, that the floor sees
    /// through the window, and the floor gets what Lambert's formula gives for the window plus what the patch adds
    /// -- π sin² a times the cosine to its middle.  Looked at evenly through the window, the patch would be found
    /// by one look in forty; read at its cells' middles alone, the map took the cells the patch's edge crosses for
    /// plain sky, and a place came out an eighth dark.
    /// </summary>
    [TestMethod]
    public void TestABrightPatchOfSkyThroughAWindowIsFoundByItsLight()
    {
        Point middle = new (-2 - Thin / 2, 1.5, 0.6);
        Vector toward = (middle - OnTheFloor).Unit;
        double halfAngle = 2 * Math.PI / 180;
        PatchPigment sky = new (toward, halfAngle, 20);
        Scene scene = ARoom(sky, 1024, true);
        double sine = Math.Sin(halfAngle);
        (double mean, double worst) = AgainstExpected(
            scene, OnTheFloor, place => 0.9 * (CosineThroughWindowA(place) +
                                               19 * Math.PI * sine * sine * toward.Dot(Directions.Up)) / Math.PI);

        Assert.IsTrue(mean < 0.012, $"on average the floor strayed {mean} from what the sky gives");
        Assert.IsTrue(worst < 0.05, $"a place on the floor strayed {worst} from what the sky gives");
    }

    /// <summary>
    /// This tests that a picture of a sky, with a sun in it the floor sees through the window, lights the floor as
    /// the sky light finds it looking everywhere with many times the samples: a picture's sky is looked at three
    /// ways at once there, through the openings, by its light, and evenly.
    /// </summary>
    [TestMethod]
    public void TestAPicturesSunThroughAWindowLightsAFloorAsLookingEverywhereFinds()
    {
        Vector toward = (new Point(-2 - Thin / 2, 1.4, 0.7) - OnTheFloor).Unit;
        ImagePigment sky = SkyWithASunToward(toward);
        Color found = Below(ARoom(sky, 65_536, false), OnTheFloor);
        Color seen = Below(ARoom(sky, 1024, true), OnTheFloor);

        Assert.IsTrue(found.Red > 0.2, $"looking everywhere, the sun gave the floor only {found.Red}");
        Assert.AreEqual(found.Red, seen.Red, found.Red * 0.015,
            $"through the window the floor showed {seen.Red}; looking everywhere it showed {found.Red}");
    }

    /// <summary>
    /// This tests that the sky's own ways of looking are kept beside the openings: a place on the room's roof,
    /// outside it but on the room's side of the window's wall, sees the whole sky and none of it through the
    /// window, and still comes back as bright as an open floor under the sky.
    /// </summary>
    [TestMethod]
    public void TestAPlaceOutsideOnTheRoomsSideOfAPortalIsLitAsAnywhere()
    {
        SolidPigment sky = new (Colors.White);
        Scene scene = ARoom(sky, 256, true);
        Point onTheRoof = new (0, 2.6 + Thin, 0);
        Color seen = Below(scene, onTheRoof);

        Assert.AreEqual(0.9, seen.Red, 0.01, $"the roof showed {seen.Red}");
    }

    /// <summary>
    /// This tests that a place on the sky's side of every opening is lit exactly as it was without them, sample
    /// for sample: the open ground in front of the window, and the top of a table standing there level with the
    /// window, outside the room.  (Seen from the sky's side, an opening's corners lean back past straight
    /// across, and the angles they make would take in the whole of its map.)
    /// </summary>
    [TestMethod]
    public void TestAPlaceOnTheSkysSideOfEveryPortalIsLitExactlyAsWithoutThem()
    {
        SolidPigment sky = new (Colors.White);
        Scene without = ARoom(sky, 64, false);
        Scene with = ARoom(sky, 64, true);

        without.Surfaces.Add(Slab(-3.8, 1.48, 0.3, -3.2, 1.5, 0.9));
        with.Surfaces.Add(Slab(-3.8, 1.48, 0.3, -3.2, 1.5, 0.9));

        foreach (Point outside in new Point[] { new (-3, -0.01, 0.6), new (-3.5, 1.5, 0.6) })
        {
            Color lit = Below(with, outside);

            Assert.AreEqual(Below(without, outside).Red, lit.Red, 0, $"at {outside}, a portal should change nothing");
            Assert.IsTrue(lit.Red > 0.3, $"outside at {outside}, the sky gave only {lit.Red}");
        }
    }

    /// <summary>
    /// This tests that which side of an opening the sky is on is found by looking, whichever way round the box
    /// that marks it is drawn: a window in a wall opens away from the room, a skylight up.
    /// </summary>
    [TestMethod]
    public void TestTheSkysSideOfAnOpeningIsFoundByLooking()
    {
        SolidPigment sky = new (Colors.White);
        Scene scene = ARoom(sky, 16, true, false, true);
        IReadOnlyList<SkyPortal> openings = scene.Lights.OfType<SkyLight>().Single().Openings;
        SkyPortal window = openings.Single(opening => Math.Abs(opening.Outward.X) > 0.5);
        SkyPortal skylight = openings.Single(opening => Math.Abs(opening.Outward.Y) > 0.5);

        Assert.AreEqual(-1, window.Outward.X, 1e-9, "the window should open toward -X");
        Assert.AreEqual(1, skylight.Outward.Y, 1e-9, "the skylight should open upward");
        Assert.AreEqual(1.2, Math.Max(window.Length, window.Width), 1e-3);
        Assert.AreEqual(-2 - Thin / 2, window.Corner.X, 1e-6, "the opening runs through the middle of its box");
        Assert.AreEqual(1, window.Along.Cross(window.Across).Dot(window.Outward), 1e-9,
            "the opening's frame should be right-handed, facing out");
    }

    /// <summary>
    /// This tests that the chances of looking in each direction through the openings add up to one over the whole
    /// sphere -- each opening's share of the light times its odds within -- and that every direction picked goes
    /// through an opening.
    /// </summary>
    [TestMethod]
    public void TestTheOddsOfLookingThroughTheOpeningsAddUpToOne()
    {
        SkyPortal window = SkyPortal.Of(
            new Point(-2, 0.9, 0), new Vector(0, 1.2, 0), new Vector(0, 0, 1.2), new Vector(-1, 0, 0));
        SkyPortal skylight = SkyPortal.Of(
            new Point(0.4, 2.6, -1), new Vector(0.8, 0, 0), new Vector(0, 0, 1.2), new Vector(0, 1, 0));
        SkyPortals portals = SkyPortals.Of(
            [window, skylight],
            (direction, _, _) => 1 + 5 * Math.Max(0, direction.Y) + 3 * Math.Max(0, -direction.X), 128);
        const int rings = 2000;
        const int spokes = 2000;
        double total = 0;

        for (int ring = 0; ring < rings; ring++)
        {
            double cosine = 1 - 2 * (ring + 0.5) / rings;
            double sine = Math.Sqrt(1 - cosine * cosine);

            for (int spoke = 0; spoke < spokes; spoke++)
            {
                double around = 2 * Math.PI * (spoke + 0.5) / spokes;

                total += portals.OddsOf(OnTheFloor, new Vector(sine * Math.Cos(around), cosine, sine * Math.Sin(around)));
            }
        }

        total *= 4 * Math.PI / (rings * spokes);

        Assert.AreEqual(1, total, 0.01, $"the odds over the sphere came to {total}");

        for (int index = 0; index < 256; index++)
        {
            Vector picked = portals.Pick(OnTheFloor, (index + 0.5) / 256, index * 0.618034 % 1);

            Assert.IsNotNull(picked);
            Assert.IsTrue(Through(window, picked) || Through(skylight, picked), $"{picked} goes through no opening");
        }

        return;

        bool Through(SkyPortal portal, Vector direction)
        {
            (_, _, double z) = portal.Place(OnTheFloor);
            double run = -z / direction.Dot(portal.Outward);

            if (run <= 0)
                return false;

            (double across, double up, _) = portal.Place(OnTheFloor + direction * run);

            return across >= -1e-9 && across <= portal.Length + 1e-9 && up >= -1e-9 && up <= portal.Width + 1e-9;
        }
    }

    /// <summary>
    /// This tests that a rough floor shares a sky seen through a portal between its highlight and its reflection,
    /// the two coming to what its reflection alone finds of the window when the window is not marked.
    /// </summary>
    [TestMethod]
    public void TestARoughFloorSharesTheSkyThroughAPortalBetweenHighlightAndReflection()
    {
        Material Rough(int looks) => new ()
        {
            Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0, Reflective = 1,
            Metallic = 1, Roughness = 0.3, ReflectionSamples = looks
        };

        // Seen from where the floor mirrors the middle of the window into the eye, the walls black so that the
        // window is all there is to see in it.
        SolidPigment sky = new (Colors.White);
        Material black = new () { Pigment = new SolidPigment(Colors.Black), Ambient = 0, Diffuse = 0, Specular = 0 };
        Ray towardTheWindow = new (new Point(1.5, 1.2, 0.6), (new Point(0, 0, 0.6) - new Point(1.5, 1.2, 0.6)).Unit);
        Color found = Shade(ARoom(sky, 4, false, floor: Rough(65_536), walls: black), towardTheWindow, 4);
        Color seen = Shade(ARoom(sky, 256, true, floor: Rough(256), walls: black), towardTheWindow, 4);

        Assert.IsTrue(found.Red > 0.05, $"the rough floor showed only {found.Red} of the window by reflection");
        Assert.AreEqual(found.Red, seen.Red, found.Red * 0.03,
            $"shared, the rough floor showed {seen.Red}; its reflection alone found {found.Red}");
    }

    /// <summary>
    /// This tests that a rough floor's window is smooth when the sky seen through it is shared between its
    /// highlight and its reflection: looking in only four directions, its reflection alone strays a quarter from
    /// what it should show, on average over a stretch of floor; shared, a twentieth.
    /// </summary>
    [TestMethod]
    public void TestARoughFloorsWindowIsSmoothWhenItsSkyIsShared()
    {
        Material Rough(int looks) => new ()
        {
            Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0, Reflective = 1,
            Metallic = 1, Roughness = 0.3, ReflectionSamples = looks
        };

        SolidPigment sky = new (Colors.White);
        Material black = new () { Pigment = new SolidPigment(Colors.Black), Ambient = 0, Diffuse = 0, Specular = 0 };
        Point eye = new (1.5, 1.2, 0.6);
        Scene found = ARoom(sky, 4, false, floor: Rough(16_384), walls: black);
        Scene shared = ARoom(sky, 64, true, floor: Rough(4), walls: black);
        double strayed = 0;

        for (int index = 0; index < 16; index++)
        {
            Point at = new (-0.2 + 0.03 * index, 0, 0.45 + 0.02 * index);
            Ray ray = new (eye, (at - eye).Unit);

            strayed += Math.Abs(Shade(shared, ray, 4).Red / Shade(found, ray, 4).Red - 1) / 16;
        }

        Assert.IsTrue(strayed < 0.1, $"shared, the rough floor's window strayed {strayed} on average");
    }

    /// <summary>
    /// This tests what cannot be a portal: something with the sky on neither side of it, something sheared out of
    /// square, and something without end.
    /// </summary>
    [TestMethod]
    public void TestWhatCannotBeAPortalIsRefused()
    {
        SolidPigment sky = new (Colors.White);
        Scene buried = ARoom(sky, 16, false);

        buried.Surfaces.Add(Marker(-1, 0.5, -1, -0.5, 1.0, -0.98));
        buried.Surfaces.Add(Slab(-1.2, 0.3, -1.2, -0.3, 1.2, -0.7));

        Assert.ThrowsExactly<InvalidOperationException>(() => buried.OpenTheSky(), "an opening shut in on both sides");

        Scene sheared = ARoom(sky, 16, false);
        Cube leaning = Marker(-1, 1, -1, 1, 1.002, 1);

        leaning.Transform = Transforms.Shear(0, 1, 0, 0, 0, 0) * leaning.Transform;
        sheared.Surfaces.Add(leaning);

        Assert.ThrowsExactly<InvalidOperationException>(() => sheared.OpenTheSky(), "an opening out of square");

        Scene endless = ARoom(sky, 16, false);

        endless.Surfaces.Add(new Plane { OpensToTheSky = true, NoShadow = true });

        Assert.ThrowsExactly<InvalidOperationException>(() => endless.OpenTheSky(), "an opening without end");
    }

    /// <summary>
    /// This tests the whole of it from a scene: the cube a window is cut out of a wall with, marked as a portal,
    /// is the opening, and a white floor under an even sky shows what Lambert's formula gives for it -- the sky
    /// twenty times white, so that the answer stands well clear of a picture's steps.  (The cube reaches past both
    /// faces of the wall: a face of it lying on one of the wall's leaves a speckled skin across the opening.)
    /// </summary>
    [TestMethod]
    public void TestACubeCutFromAWallIsAPortalInAScene()
    {
        string path = Path.Combine(_directory, "room.igl");
        string output = Path.Combine(_directory, "room.png");
        double expected = 0.9 * CosineThroughWindowA(OnTheFloor) / Math.PI;

        File.WriteAllText(path,
            "context { width 3  height 3  no gamma }\n" +
            "camera { location [0.5, 1, 0.3]  look at [0.5, 0, 0.3]  up [0, 0, 1]  field of view 1 }\n" +
            "background [20, 20, 20]\n" +
            "sky light { samples 1024 }\n" +
            "difference {\n" +
            $"    cube {{ scale [{2 + Thin}, {1.3 + Thin}, {2.5 + Thin}]  translate [0, 1.3, 0] }}\n" +
            "    cube { scale [2, 1.3, 2.5]  translate [0, 1.3, 0] }\n" +
            $"    cube {{ scale [{2 * Thin}, 0.6, 0.6]  translate [-2, 1.5, 0.6]  portal }}\n" +
            "    material { pigment White  ambient 0  diffuse 0.9  specular 0 }\n" +
            "}\n");

        ImageRenderer renderer = new LanguageParser(path).Parse();

        renderer.Render(new RenderOptions { OutputFileName = output });

        Color seen = new ImageFile(output).Load()[0].GetPixel(1, 1);

        Assert.AreEqual(20 * expected, seen.Red, 0.006, $"the floor showed {seen.Red}; the window gives {20 * expected}");
    }

    /// <summary>
    /// This helper writes a Radiance picture of an even sky of brightness one with a sun a few texels across,
    /// three thousand times as bright, in the given direction, and returns a pigment reading it round a sphere.
    /// </summary>
    private ImagePigment SkyWithASunToward(Vector toward)
    {
        const int width = 512;
        const int height = 256;
        double around = Math.Atan2(toward.Z, toward.X);

        if (around < 0)
            around += 2 * Math.PI;

        int sunX = (int) (around / (2 * Math.PI) * width);
        int sunY = (int) ((0.5 - Math.Asin(toward.Y) / Math.PI) * height);
        string path = Path.Combine(_directory, $"{Guid.NewGuid():N}.hdr");

        using (MagickImage image = new (MagickColors.Black, width, height))
        {
            image.ColorSpace = ColorSpace.RGB;

            using (IPixelCollection<float> pixels = image.GetPixels())
            {
                foreach (IPixel<float> pixel in pixels)
                {
                    bool sun = Math.Abs(pixel.X - sunX) <= 1 && Math.Abs(pixel.Y - sunY) <= 1;
                    float light = (float) ((sun ? 3000 : 1) * 65535);

                    pixel.SetChannel(0, light);
                    pixel.SetChannel(1, light);
                    pixel.SetChannel(2, light);
                }
            }

            image.Write(path, MagickFormat.Hdr);
        }

        ImagePigment pigment = new ()
        {
            ImageReference = new ImageReference { ImageName = Path.GetFileName(path), SourceDirectory = _directory },
            MapType = ImageMapType.Spherical
        };

        pigment.RenderingIsAboutToStart(new RenderContext(), null);

        return pigment;
    }
}
