using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
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
/// Pictures laid over a whole plane in the two ways that read them more than once at a place: triplanar, the
/// picture laid square to each axis and blended by the way the surface faces, at the world's size; and shuffled, the
/// picture cut into tiles each shifted by an amount of its own so that it never repeats.
/// </summary>
[TestClass]
public class TestImageLaying
{
    private string _directory;

    [TestInitialize]
    public void MakeDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"image-laying-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        Directory.Delete(_directory, true);
    }

    /// <summary>
    /// This helper writes an eight-bit PNG of the given colors as a picture file holds them, and returns its name.
    /// </summary>
    private string Picture(int width, int height, Func<int, int, Color> colorAt)
    {
        string name = $"{Guid.NewGuid():N}.png";
        Canvas canvas = new (width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                canvas.SetColor(colorAt(x, y), x, y);
        }

        new ImageFile(Path.Combine(_directory, name)).Save(canvas, new RenderContext { ApplyGamma = false });

        return name;
    }

    /// <summary>
    /// This helper writes a picture whose every texel is its own, so that any two places read from it can be told
    /// apart, and whose two halves cannot be swapped without showing: a ramp across in red, a ramp down in green.
    /// </summary>
    private string Ramps() => Picture(16, 16, (x, y) => new Color(x / 15.0, y / 15.0, 0.5));

    /// <summary>
    /// This helper writes a picture with no seam where it repeats, and no edge anywhere in it: so a jump read from it
    /// can only be a seam in how it is laid.
    /// </summary>
    private string Seamless() => Picture(32, 32, (x, y) => new Color(
        0.5 + 0.5 * Math.Sin(2 * Math.PI * x / 32), 0.5 + 0.5 * Math.Cos(2 * Math.PI * y / 32),
        0.5 + 0.25 * Math.Sin(2 * Math.PI * (x + y) / 32)));

    /// <summary>
    /// This helper returns an image pigment reading the named picture, ready for the given surface.
    /// </summary>
    private ImagePigment PigmentOf(
        string name, ImageMapType map, Surface surface = null, Shuffle shuffle = null, double blend = 0.25)
    {
        ImagePigment pigment = new ()
        {
            ImageReference = new ImageReference { ImageName = name, SourceDirectory = _directory },
            MapType = map,
            Shuffle = shuffle,
            Blend = blend
        };

        pigment.RenderingIsAboutToStart(new RenderContext(), surface);

        return pigment;
    }

    /// <summary>
    /// This helper returns a pattern reading the named picture.
    /// </summary>
    private ImagePattern PatternOf(string name, ImageMapType map, Shuffle shuffle = null)
    {
        ImagePattern pattern = new ()
        {
            ImageReference = new ImageReference { ImageName = name, SourceDirectory = _directory },
            MapType = map,
            Shuffle = shuffle
        };

        pattern.Load();

        return pattern;
    }

    /// <summary>
    /// This helper returns the color a material wearing the pigment shows at a place on a surface facing the given
    /// way, as shading asks for it.
    /// </summary>
    private static Color Seen(Pigment pigment, Surface surface, Point point, Vector normal = null) =>
        new Material { Pigment = pigment }.GetColorFor(surface, point, normal);

    private static void AssertSame(Color expected, Color actual, string what, double within = 1e-9)
    {
        Assert.AreEqual(expected.Red, actual.Red, within, $"{what}: red");
        Assert.AreEqual(expected.Green, actual.Green, within, $"{what}: green");
        Assert.AreEqual(expected.Blue, actual.Blue, within, $"{what}: blue");
    }

    /// <summary>
    /// On each face of a cube a triplanar picture is the picture laid flat on that face: across X and down Z on the
    /// top, as a planar picture lies on a floor, and on the sides upright, its rows running down -- and on every face
    /// it reads as it does from outside, not mirrored on the far side.
    /// </summary>
    [TestMethod]
    public void TestATriplanarPictureLiesFlatOnEachFace()
    {
        string name = Ramps();
        Cube cube = new ();
        ImagePigment triplanar = PigmentOf(name, ImageMapType.Triplanar, cube);
        ImagePigment planar = PigmentOf(name, ImageMapType.Planar);
        (double a, double b) = (0.3, 0.65);

        foreach ((string face, Point point, Vector normal, Point flat) in new[]
                 {
                     ("+X", new Point(1, a, b), new Vector(1, 0, 0), new Point(-b, 0, -a)),
                     ("-X", new Point(-1, a, b), new Vector(-1, 0, 0), new Point(b, 0, -a)),
                     ("+Y", new Point(a, 1, b), new Vector(0, 1, 0), new Point(a, 0, b)),
                     ("-Y", new Point(a, -1, b), new Vector(0, -1, 0), new Point(-a, 0, b)),
                     ("+Z", new Point(a, b, 1), new Vector(0, 0, 1), new Point(a, 0, -b)),
                     ("-Z", new Point(a, b, -1), new Vector(0, 0, -1), new Point(-a, 0, -b))
                 })
            AssertSame(planar.GetColorFor(flat), Seen(triplanar, cube, point, normal), face);
    }

    /// <summary>
    /// A triplanar picture is laid at the world's size, whatever the shape is stretched by -- its own or its
    /// parents' -- so a ball squashed into a boulder, or a height field stretched over the ground, wears the picture
    /// as evenly as a ball does.  Laid in the shape's own space, a ball stretched four times along X would wear it
    /// four times as wide.
    /// </summary>
    [TestMethod]
    public void TestATriplanarPictureIsLaidAtTheWorldsSize()
    {
        string name = Ramps();
        ImagePigment planar = PigmentOf(name, ImageMapType.Planar);
        Sphere stretched = new () { Transform = Transforms.Scale(4, 1, 1) };
        Sphere held = new ();
        Group group = new () { Transform = Transforms.Scale(2) };

        group.Add(held);

        ImagePigment onStretched = PigmentOf(name, ImageMapType.Triplanar, stretched);
        ImagePigment onHeld = PigmentOf(name, ImageMapType.Triplanar, held);

        AssertSame(planar.GetColorFor(new Point(2.3, 0, 0.4)),
            Seen(onStretched, stretched, new Point(2.3, 1, 0.4), new Vector(0, 1, 0)), "on top of the stretched ball");
        AssertSame(planar.GetColorFor(new Point(2.3, 0, -0.2)),
            Seen(onStretched, stretched, new Point(2.3, 0.2, 1), new Vector(0, 0, 1)), "on its side");
        Assert.IsFalse(planar.GetColorFor(new Point(2.3 / 4, 0, 0.4))
            .Matches(Seen(onStretched, stretched, new Point(2.3, 1, 0.4), new Vector(0, 1, 0))),
            "laid in the ball's own space it would be read a quarter as far along");
        AssertSame(planar.GetColorFor(new Point(1.4, 0, 0.6)),
            Seen(onHeld, held, new Point(1.4, 2, 0.6), new Vector(0, 1, 0)), "on a ball doubled by its group");
    }

    /// <summary>
    /// A triplanar picture's own transforms size and turn it in that measure, and the way the surface faces is carried
    /// through them with the point: turned on its side, the picture's projection square to its own Y lies on the
    /// cube's side, and the cube's top takes the projection square to the picture's own Z.
    /// </summary>
    [TestMethod]
    public void TestATriplanarPictureTurnsWithItsTransforms()
    {
        string name = Ramps();
        Cube cube = new ();
        ImagePigment planar = PigmentOf(name, ImageMapType.Planar);
        ImagePigment sized = PigmentOf(name, ImageMapType.Triplanar, cube);
        ImagePigment turned = PigmentOf(name, ImageMapType.Triplanar, cube);
        Matrix turn = Transforms.RotateAroundX(90);
        Point top = new (0.6, 1, 0.8);

        sized.Transform = Transforms.Scale(2);
        turned.Transform = turn;

        AssertSame(planar.GetColorFor(new Point(0.3, 0, 0.4)), Seen(sized, cube, top, new Vector(0, 1, 0)), "sized");

        // In the turned picture's space the top faces along its Z, and is read as a face square to Z is.
        Point there = turn.Invert() * top;
        Vector facing = turn.Transpose() * new Vector(0, 1, 0);
        double side = facing.Z >= 0 ? 1 : -1;

        Assert.AreEqual(1, Math.Abs(facing.Z), 1e-9);
        AssertSame(planar.GetColorFor(new Point(side * there.X, 0, -there.Y)),
            Seen(turned, cube, top, new Vector(0, 1, 0)), "turned");
    }

    /// <summary>
    /// Where a surface faces two axes at once the projections blend by how squarely it faces each, raised to the
    /// power one over the blend -- the fourth unless told -- so the turn between two faces is a blend and not an edge,
    /// and a wider blend mixes more.
    /// </summary>
    [TestMethod]
    public void TestATriplanarPictureBlendsWhereTheSurfaceTurns()
    {
        string name = Ramps();
        Sphere ball = new ();
        ImagePigment planar = PigmentOf(name, ImageMapType.Planar);
        Point point = new (0.6, 0.8, 0);
        Vector normal = new (0.6, 0.8, 0);
        Color sideways = planar.GetColorFor(new Point(0, 0, -0.8));
        Color upward = planar.GetColorFor(new Point(0.6, 0, 0));

        foreach ((double blend, double power) in new[] { (0.25, 4.0), (1, 1.0), (0.5, 2.0) })
        {
            double x = Math.Pow(0.6, power), y = Math.Pow(0.8, power);
            Color expected = sideways * (x / (x + y)) + upward * (y / (x + y));

            AssertSame(expected, Seen(PigmentOf(name, ImageMapType.Triplanar, ball, blend: blend), ball, point, normal),
                $"blend {blend}");
        }
    }

    /// <summary>
    /// Read with no facing to go by -- a strand's root, a shadow ray through a picture's clear parts -- a triplanar
    /// picture takes its three projections alike.
    /// </summary>
    [TestMethod]
    public void TestATriplanarPictureReadWithNoFacingTakesAllThree()
    {
        string name = Ramps();
        Cube cube = new ();
        ImagePigment planar = PigmentOf(name, ImageMapType.Planar);
        Color expected = (planar.GetColorFor(new Point(-0.7, 0, -0.4)) + planar.GetColorFor(new Point(0.2, 0, 0.7)) +
                          planar.GetColorFor(new Point(0.2, 0, -0.4))) * (1 / 3.0);

        AssertSame(expected, Seen(PigmentOf(name, ImageMapType.Triplanar, cube), cube, new Point(0.2, 0.4, 0.7)),
            "with no facing");
    }

    /// <summary>
    /// A shuffled picture does not repeat: a picture apart, a plain one shows the same color and a shuffled one,
    /// nearly everywhere, does not.
    /// </summary>
    [TestMethod]
    public void TestAShuffledPictureDoesNotRepeat()
    {
        string name = Ramps();
        ImagePigment plain = PigmentOf(name, ImageMapType.Planar);
        ImagePigment shuffled = PigmentOf(name, ImageMapType.Planar, shuffle: new Shuffle(Shuffle.DefaultSize, 0));
        int places = 0, repeated = 0;

        for (double x = 0.05; x < 3; x += 0.21)
        {
            for (double z = 0.07; z < 3; z += 0.19)
            {
                Point here = new (x, 0, z), apart = new (x + 1, 0, z);

                Assert.IsTrue(plain.GetColorFor(here).Matches(plain.GetColorFor(apart)), "a plain picture repeats");

                places++;

                if (shuffled.GetColorFor(here).Matches(shuffled.GetColorFor(apart)))
                    repeated++;
            }
        }

        Assert.IsTrue(repeated < places / 20, $"{repeated} of {places} places repeated a picture apart");
    }

    /// <summary>
    /// A shuffled picture is unbroken: walked in small steps across many tiles, a picture with no seam of its own
    /// never jumps, since each tile gives way to the next smoothly.
    /// </summary>
    [TestMethod]
    public void TestAShuffledPictureIsUnbroken()
    {
        string name = Seamless();

        foreach (double turn in new[] { 0, Math.PI })
        {
            ImagePigment shuffled = PigmentOf(name, ImageMapType.Planar, shuffle: new Shuffle(0.3, turn));
            Color was = shuffled.GetColorFor(new Point(0, 0, 0.123));
            double worst = 0;

            for (double x = 1e-4; x < 3; x += 1e-4)
            {
                Color now = shuffled.GetColorFor(new Point(x, 0, 0.123 + x * 0.37));

                worst = Math.Max(worst, Math.Max(Math.Abs(now.Red - was.Red),
                    Math.Max(Math.Abs(now.Green - was.Green), Math.Abs(now.Blue - was.Blue))));
                was = now;
            }

            Assert.IsTrue(worst < 0.01, $"turned {turn}: the largest step was {worst}");
        }
    }

    /// <summary>
    /// A shuffled picture's tiles are drawn from its seed and the tile, never in order: the same seed shuffles it the
    /// same way every time, and another seed another way; and a picture told to turn its tiles by nothing is shuffled
    /// exactly as one told nothing of turning.
    /// </summary>
    [TestMethod]
    public void TestAShuffledPictureKeepsItsTiles()
    {
        string name = Ramps();
        ImagePigment first = PigmentOf(name, ImageMapType.Planar, shuffle: new Shuffle(0.3, 0));
        ImagePigment again = PigmentOf(name, ImageMapType.Planar, shuffle: new Shuffle(0.3, 0));
        ImagePigment other = PigmentOf(name, ImageMapType.Planar, shuffle: new Shuffle(0.3, 0));
        ImagePigment turned = PigmentOf(name, ImageMapType.Planar, shuffle: new Shuffle(0.3, Math.PI / 2));
        int places = 0, differing = 0, turnedDiffering = 0;

        other.Seed = 7;

        for (double x = 0.05; x < 2; x += 0.13)
        {
            for (double z = 0.03; z < 2; z += 0.17)
            {
                Point here = new (x, 0, z);
                Color color = first.GetColorFor(here);

                Assert.IsTrue(color.Matches(again.GetColorFor(here)), "shuffled alike");

                places++;

                if (!color.Matches(other.GetColorFor(here)))
                    differing++;

                if (!color.Matches(turned.GetColorFor(here)))
                    turnedDiffering++;
            }
        }

        Assert.IsTrue(differing > places * 9 / 10, $"another seed changed only {differing} of {places}");
        Assert.IsTrue(turnedDiffering > places * 9 / 10, $"turning changed only {turnedDiffering} of {places}");
    }

    /// <summary>
    /// However a picture is laid over a plane, and however it is filtered, its readings count for one between them:
    /// a picture of one color reads as that color everywhere.
    /// </summary>
    [TestMethod]
    public void TestAPictureOfOneColorIsThatColorHoweverItIsLaid()
    {
        string name = Picture(8, 8, (_, _) => new Color(0.3, 0.6, 0.2));
        Sphere ball = new ();
        ImagePigment plain = PigmentOf(name, ImageMapType.Planar);
        Color expected = plain.GetColorFor(new Point(0.2, 0, 0.3));
        Footprint patch = new (new Vector(0.03, 0.01, 0), new Vector(0, 0.02, 0.04));

        foreach (ImagePigment pigment in new[]
                 {
                     PigmentOf(name, ImageMapType.Triplanar, ball),
                     PigmentOf(name, ImageMapType.Triplanar, ball, new Shuffle(0.2, Math.PI)),
                     PigmentOf(name, ImageMapType.Planar, shuffle: new Shuffle(0.25, 1))
                 })
        {
            for (int index = 0; index < 20; index++)
            {
                Vector normal = new Vector(Math.Sin(index), Math.Cos(index * 1.7), Math.Sin(index * 0.3 + 1)).Unit;
                Point point = Point.Zero + normal;

                AssertSame(expected, Seen(pigment, ball, point, normal), $"at {point}");
                AssertSame(expected,
                    new Material { Pigment = pigment }.GetColorFor(ball, point, normal, patch), $"over a patch at {point}");
            }
        }
    }

    /// <summary>
    /// A triplanar normal map tilts the normal on each face in the frame its projection is laid in there: on top as a
    /// planar map does on a floor, and on a side toward the way across the picture runs on that side.  A map that
    /// tilts nothing leaves every facing alone, wherever the projections blend.
    /// </summary>
    [TestMethod]
    public void TestATriplanarNormalMapTiltsInEachFacesFrame()
    {
        double lean = 0.4;
        string tilted = Picture(4, 4, (_, _) => new Color((Math.Sin(lean) + 1) / 2, 0.5, (Math.Cos(lean) + 1) / 2));
        string flat = Picture(4, 4, (_, _) => new Color(0.5, 0.5, 1));
        SurfaceNormal triplanar = new () { NormalMap = PatternOf(tilted, ImageMapType.Triplanar) };
        SurfaceNormal planar = new () { NormalMap = PatternOf(tilted, ImageMapType.Planar) };
        SurfaceNormal still = new () { NormalMap = PatternOf(flat, ImageMapType.Triplanar) };
        Point point = new (0.3, 0.2, 0.6);

        Assert.IsTrue(planar.PerturbAt(new Vector(0, 1, 0), point)
            .Matches(triplanar.PerturbAt(new Vector(0, 1, 0), point)), "on top, as on a floor");

        Vector onTheSide = triplanar.PerturbAt(new Vector(1, 0, 0), point);
        Vector onTheFarSide = triplanar.PerturbAt(new Vector(-1, 0, 0), point);

        Assert.IsTrue(onTheSide.Z < -0.3 && onTheSide.X > 0.8 && Math.Abs(onTheSide.Y) < 0.05,
            $"on the side toward +X it leans the way across, -Z ({onTheSide})");
        Assert.IsTrue(onTheFarSide.Z > 0.3 && onTheFarSide.X < -0.8 && Math.Abs(onTheFarSide.Y) < 0.05,
            $"on the far side, +Z ({onTheFarSide})");
        Assert.AreEqual(lean, Math.Atan2(-onTheSide.Z, onTheSide.X), 0.02, "by as much as on top");

        for (int index = 0; index < 12; index++)
        {
            Vector normal = new Vector(Math.Sin(index), Math.Cos(index * 1.3), Math.Sin(index * 0.7 + 2)).Unit;

            // Eight bits hold a half as 128 of 255, which leans every texel of a flat map by a third of a degree.
            Assert.IsTrue(normal.Dot(still.PerturbAt(normal, point)) > 0.9999, $"a flat map left {normal} alone");
        }
    }

    /// <summary>
    /// A triplanar picture's bumps lie on a floor as a planar picture's do: its slope is the projections' slopes, each
    /// carried out of the picture by how fast its reading moves, weighed as they count.
    /// </summary>
    [TestMethod]
    public void TestATriplanarPicturesBumpsAreAPlanarOnesOnAFloor()
    {
        string name = Seamless();
        SurfaceNormal planar = new () { Pattern = PatternOf(name, ImageMapType.Planar), Depth = 0.5 };
        SurfaceNormal triplanar = new () { Pattern = PatternOf(name, ImageMapType.Triplanar), Depth = 0.5 };
        Vector up = new (0, 1, 0);

        for (double x = 0.05; x < 1; x += 0.17)
        {
            for (double z = 0.02; z < 1; z += 0.13)
            {
                Point point = new (x, 0, z);
                Vector expected = planar.PerturbAt(up, point), actual = triplanar.PerturbAt(up, point);

                Assert.IsTrue(expected.Dot(actual) > 1 - 1e-9, $"at {point}: {expected} against {actual}");
                Assert.IsTrue(expected.Dot(up) < 0.9999, "and the bumps tilt it");
            }
        }
    }

    /// <summary>
    /// A shuffled picture's bumps run as deep as the picture's own: each place's slope is its tiles' slopes, weighed
    /// as they count between them, so where one tile holds a place alone its slope is the picture's, and only the
    /// narrow seams between tiles, where slopes leaning different ways meet, run shallower.
    /// </summary>
    [TestMethod]
    public void TestAShuffledPicturesBumpsAreAsDeepAsItsOwn()
    {
        string name = Seamless();
        SurfaceNormal plain = new () { Pattern = PatternOf(name, ImageMapType.Planar), Depth = 0.5 };
        SurfaceNormal shuffled = new ()
        {
            Pattern = PatternOf(name, ImageMapType.Planar, new Shuffle(0.3, Math.PI)), Depth = 0.5
        };
        Vector up = new (0, 1, 0);
        double plainTilt = 0, shuffledTilt = 0;

        for (double x = 0.013; x < 3; x += 0.047)
        {
            for (double z = 0.029; z < 3; z += 0.053)
            {
                Point point = new (x, 0, z);

                plainTilt += Math.Acos(Math.Min(1, up.Dot(plain.PerturbAt(up, point))));
                shuffledTilt += Math.Acos(Math.Min(1, up.Dot(shuffled.PerturbAt(up, point))));
            }
        }

        Assert.AreEqual(1, shuffledTilt / plainTilt, 0.25, $"the shuffled bumps' mean tilt over the plain's");
    }

    /// <summary>
    /// This helper renders a small scene whose ball wears the given material entries, beside a picture named
    /// 'map.png', and returns what went wrong, if anything did.
    /// </summary>
    private string ErrorFrom(string material, string background = "")
    {
        string path = Path.Combine(_directory, "scene.igl");
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        if (!File.Exists(Path.Combine(_directory, "map.png")))
            File.Copy(Path.Combine(_directory, Ramps()), Path.Combine(_directory, "map.png"));

        File.WriteAllText(path,
            "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
            "point light { location [-4, 6, -8] }\n" + background +
            $"sphere {{ material {{ {material} }} }}");

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            if (renderer is null)
                return captured.ToString();

            renderer.Render(new RenderOptions
            {
                OutputFileName = Path.Combine(_directory, "out.png"), Width = 8, Height = 8
            });

            return captured.ToString().Contains("Error") ? captured.ToString() : null;
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    /// <summary>
    /// Every way of laying a picture over a plane reads and renders: triplanar, with a blend, shuffled, both, as a
    /// pigment, as a pattern picking among pigments, as bumps, as a normal map, as a roughness -- and a triplanar
    /// image standing among other pigments does not take the next one's word for its blend.
    /// </summary>
    [TestMethod]
    public void TestEveryFormReadsAndRenders()
    {
        foreach (string material in new[]
                 {
                     "pigment image 'map.png' triplanar",
                     "pigment image 'map.png' triplanar { scale 0.5 }",
                     "pigment image 'map.png' triplanar blend 0.5 { scale 0.5  rotate Y 30 }",
                     "pigment image 'map.png' planar shuffled",
                     "pigment image 'map.png' planar shuffled size 0.4 turned 180 { brightness 0.8 }",
                     "pigment image 'map.png' triplanar blend 0.4 shuffled turned 30",
                     "pigment image 'map.png' triplanar shuffled size 0.2 { [0, Black, 1, White]  scale 2 }",
                     "pigment blend { image 'map.png' triplanar, blend { color Red, color Blue } }",
                     "pigment Red  normal image 'map.png' triplanar shuffled { depth 0.5 }",
                     "pigment Red  normal map 'map.png' triplanar shuffled directx { depth 0.5  scale 2 }",
                     "pigment Red  roughness 0.2 to 0.8 by image 'map.png' triplanar channel red { }",
                     "pigment Red  metallic 0.2 to 0.9 by image 'map.png' planar shuffled channel blue { }"
                 })
            Assert.IsNull(ErrorFrom(material), material);
    }

    /// <summary>
    /// What cannot be said is refused: a blend outside nought to one, tiles of no size or turned by less than nothing,
    /// a picture shuffled that does not lie over a plane, and a triplanar background, which faces no way at all.
    /// </summary>
    [TestMethod]
    public void TestWhatCannotBeSaidIsRefused()
    {
        StringAssert.Contains(ErrorFrom("pigment image 'map.png' triplanar blend 0"),
            "A triplanar image's blend runs from more than nothing to one.");
        StringAssert.Contains(ErrorFrom("pigment image 'map.png' triplanar blend 1.5"),
            "A triplanar image's blend runs from more than nothing to one.");
        StringAssert.Contains(ErrorFrom("pigment image 'map.png' planar shuffled size 0"),
            "A shuffled image's size must be more than nothing.");
        StringAssert.Contains(ErrorFrom("pigment image 'map.png' planar shuffled turned -10"),
            "How far a shuffled image's tiles turn may not be less than nothing.");
        Assert.IsNotNull(ErrorFrom("pigment image 'map.png' spherical shuffled"), "a ball is not a plane");
        Assert.IsNotNull(ErrorFrom("pigment image 'map.png' planar once shuffled"), "once is not shuffled");
        StringAssert.Contains(ErrorFrom("pigment Red", "background image 'map.png' triplanar\n"),
            "A triplanar image is laid by the way a surface faces, so it cannot be a background.");
    }
}
