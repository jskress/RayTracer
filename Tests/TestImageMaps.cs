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
/// Pictures that drive more than color: a picture read as a pattern, its bumps, normal maps, and a material's
/// roughness and metalness running by a pattern.
/// </summary>
[TestClass]
public class TestImageMaps
{
    private string _directory;

    [TestInitialize]
    public void MakeDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"image-map-tests-{Guid.NewGuid():N}");

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

        // A canvas holds light premultiplied by how much of each place it covers, and is written straight.
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color color = colorAt(x, y);

                canvas.SetColor(
                    new Color(color.Red * color.Alpha, color.Green * color.Alpha, color.Blue * color.Alpha, color.Alpha),
                    x, y);
            }
        }

        // Written with no gamma, the file holds exactly the values given, as a measurement's would.
        new ImageFile(Path.Combine(_directory, name)).Save(canvas, new RenderContext { ApplyGamma = false });

        return name;
    }

    /// <summary>
    /// This helper returns a pattern reading the named picture, ready to be asked about.
    /// </summary>
    private ImagePattern PatternOf(string name, ImageMapType? map = ImageMapType.Planar,
        ImageChannel channel = ImageChannel.Gray)
    {
        ImagePattern pattern = new ()
        {
            ImageReference = new ImageReference { ImageName = name, SourceDirectory = _directory },
            MapType = map,
            Channel = channel
        };

        pattern.Load();

        return pattern;
    }

    private static Color Gray(double value) => new (value, value, value);

    /// <summary>
    /// A picture read as a pattern is a measurement, read as the file holds it: a mid gray is half, where read as a
    /// pigment's light it would be a fifth.
    /// </summary>
    [TestMethod]
    public void TestAPictureIsReadAsItsFileHoldsIt()
    {
        ImagePattern pattern = PatternOf(Picture(4, 4, (_, _) => Gray(128 / 255.0)));

        Assert.AreEqual(128 / 255.0, pattern.ValueFor(new Point(0.4, 0, 0.6)), 1e-6);
    }

    /// <summary>
    /// A picture's gray is the gray a height field takes its heights from, and each channel may be read alone.
    /// </summary>
    [TestMethod]
    public void TestAPicturesGrayIsAHeightFieldsAndItsChannelsAreItsOwn()
    {
        Color color = new (1, 0.5, 0.2, 0.6);
        string name = Picture(2, 2, (_, _) => color);
        Point point = new (0.3, 0, 0.7);

        Assert.AreEqual(color.AsGray().Red, PatternOf(name).ValueFor(point), 2e-3);
        Assert.AreEqual(0.299 + 0.587 * 0.5 + 0.114 * 0.2, PatternOf(name).ValueFor(point), 3e-3);
        Assert.AreEqual(1, PatternOf(name, channel: ImageChannel.Red).ValueFor(point), 2e-3);
        Assert.AreEqual(0.5, PatternOf(name, channel: ImageChannel.Green).ValueFor(point), 3e-3);
        Assert.AreEqual(0.2, PatternOf(name, channel: ImageChannel.Blue).ValueFor(point), 3e-3);
        Assert.AreEqual(0.6, PatternOf(name, channel: ImageChannel.Alpha).ValueFor(point), 3e-3);
    }

    /// <summary>
    /// A picture's white is the top of a map of pigments, not its start: a map wraps a value of one back round to
    /// its first entry, which made every white place in a picture read as the map's black.
    /// </summary>
    [TestMethod]
    public void TestAPicturesWhiteIsTheTopOfAMap()
    {
        PatternPigment pigment = new ()
        {
            Pattern = PatternOf(Picture(2, 2, (_, _) => Gray(1))),
            PigmentSet = new PigmentSet()
        };

        pigment.PigmentSet.AddEntry(SolidPigment.Black);
        pigment.PigmentSet.AddEntry(SolidPigment.White, 1);

        Assert.AreEqual(1, pigment.GetColorFor(new Point(0.3, 0, 0.6)).Red, 1e-9);
    }

    /// <summary>
    /// An image pigment lets light through only if its picture does, or past the edges of one laid once.  Until
    /// 2026-10-31 every image said it might, and a material colored by a picture could be no metal, take no coat or
    /// sheen, and if rough mirrored nothing at all: it was taken for glass.
    /// </summary>
    [TestMethod]
    public void TestAnOpaquePictureLetsNoLightThrough()
    {
        string opaque = Picture(2, 2, (_, _) => Gray(0.5));
        string clear = Picture(2, 2, (x, _) => new Color(0.5, 0.5, 0.5, x == 0 ? 1 : 0.5));

        Assert.IsFalse(PigmentOf(opaque).MayTransmit);
        Assert.IsTrue(PigmentOf(clear).MayTransmit);
        Assert.IsTrue(PigmentOf(opaque, once: true).MayTransmit, "clear past the edges of a picture laid once");

        Material gold = new () { Pigment = PigmentOf(opaque), Metal = Metal.Named("gold"), Metallic = 1 };
        Material glaze = new () { Pigment = PigmentOf(opaque), Roughness = 0.3, Reflective = 0.04 };

        Assert.IsTrue(gold.MetalApplies, "a metal colored by a picture is a metal");
        Assert.IsTrue(glaze.FresnelApplies, "a rough glaze colored by a picture is not glass");

        ImagePigment PigmentOf(string name, bool once = false)
        {
            ImagePigment pigment = new ()
            {
                ImageReference = new ImageReference { ImageName = name, SourceDirectory = _directory },
                MapType = ImageMapType.Planar,
                Once = once
            };

            pigment.Load(new RenderContext());

            return pigment;
        }
    }

    /// <summary>
    /// On a height field a picture lies as the field lays its heights, texel for texel: at every corner of the
    /// terrain the picture made it from, read there as a pattern with no map said, stands a quarter as tall as the
    /// pattern is bright -- and an image pigment's color there is the picture's own.  Laid as a planar map lays it,
    /// half a texel in from the square's edges, every corner but the middle of the field read the wrong texel's
    /// share.
    /// </summary>
    [TestMethod]
    public void TestAPictureLiesOnAHeightFieldsHeights()
    {
        const int Columns = 7, Rows = 5;
        Func<int, int, double> grayAt = (x, y) => ((x * 37 + y * 101) % 64 + 16) / 255.0;
        string name = Picture(Columns, Rows, (x, y) => Gray(grayAt(x, y)));
        ImageReference reference = new () { ImageName = name, SourceDirectory = _directory };
        HeightField field = new () { ImageReference = reference, Closed = false };
        ImagePattern pattern = PatternOf(name, map: null);
        ImagePigment pigment = new () { ImageReference = reference };

        field.PrepareForRendering();
        pigment.RenderingIsAboutToStart(new RenderContext { ApplyGamma = false }, field);

        foreach (Triangle triangle in field.Surfaces.OfType<Triangle>())
        {
            using Pigment.WornScope _ = Pigment.WornBy(triangle);

            foreach (Point corner in new[] { triangle.Point1, triangle.Point2, triangle.Point3 })
            {
                int x = (int) Math.Round(corner.X * (Columns - 1)), y = (int) Math.Round(corner.Z * (Rows - 1));

                Assert.AreEqual(corner.Y * 4, pattern.ValueFor(corner), 1e-6);
                Assert.AreEqual(grayAt(x, y), pigment.GetColorFor(triangle, corner).Red, 1e-6);
            }
        }
    }

    /// <summary>
    /// A picture's slope is taken from a texel either side.  Between its texels a picture is read along straight
    /// lines, its slope the same from one texel's middle to the next and jumping at each, and taken over a
    /// thousandth of a unit -- as a pattern's slope is -- a fine picture bumped a surface into a grid of facets.  Here
    /// a picture one texel high, climbing as the square of the way across, bumps a floor whose tilt climbs evenly
    /// through the span between two texels' middles, where read between them it would stand still; its texels a
    /// whole unit deep, a slope taken a texel's depth either way would have found none at all.
    /// </summary>
    [TestMethod]
    public void TestAPicturesBumpsTurnSmoothlyBetweenItsTexels()
    {
        const int Width = 8;
        ImagePattern pattern = PatternOf(Picture(Width, 1, (x, _) => Gray(Math.Pow((x + 0.5) / Width, 2))));
        SurfaceNormal bumps = new () { Pattern = pattern, Depth = 1 };
        Vector up = new (0, 1, 0);
        double[] tilts = new[] { 3.6, 3.8, 4.0, 4.2, 4.4 }
            .Select(texels => -bumps.PerturbAt(up, new Point(texels / Width, 0, 0.5)).X)
            .ToArray();

        for (int index = 1; index < tilts.Length; index++)
        {
            Assert.IsTrue(tilts[index] > tilts[index - 1], "the tilt climbs through the span");
            Assert.AreEqual(tilts[1] - tilts[0], tilts[index] - tilts[index - 1], 0.02 * (tilts[^1] - tilts[0]),
                "and climbs evenly");
        }
    }

    /// <summary>
    /// A normal map drawn from a picture of heights tilts a surface as the heights' own bumps do: drawn OpenGL's
    /// way, red across the picture and green up it, it stands within a hair of the bumps at every texel, which pins
    /// the frame it is read in -- the way across, the way up, and which way the surface faces.  Read as DirectX's,
    /// green the other way, it does not.
    /// </summary>
    [TestMethod]
    public void TestANormalMapStandsAsTheBumpsItWasDrawnFrom()
    {
        const int Size = 64;
        const double Depth = 0.3;
        double slope = 0.125 * Depth * Size;
        Func<double, double, double> height = (i, j) =>
            0.5 + 0.2 * Math.Sin(2 * Math.PI * i / Size) * Math.Cos(2 * Math.PI * j / Size);

        // The heights' slopes along the picture's columns and rows, in the bumps' own units.
        Func<int, int, Color> normalAt = (i, j) =>
        {
            double across = 0.2 * 2 * Math.PI / Size * Math.Cos(2 * Math.PI * i / Size) *
                            Math.Cos(2 * Math.PI * j / Size) * slope;
            double down = -0.2 * 2 * Math.PI / Size * Math.Sin(2 * Math.PI * i / Size) *
                          Math.Sin(2 * Math.PI * j / Size) * slope;
            Vector normal = new Vector(-across, down, 1).Unit;

            return new Color((normal.X + 1) / 2, (normal.Y + 1) / 2, (normal.Z + 1) / 2);
        };
        SurfaceNormal bumps = new ()
        {
            Pattern = PatternOf(Picture(Size, Size, (i, j) => Gray(height(i, j)))), Depth = Depth
        };
        string mapName = Picture(Size, Size, normalAt);
        SurfaceNormal map = new () { NormalMap = PatternOf(mapName) };
        SurfaceNormal backward = new () { NormalMap = PatternOf(mapName), GreenDown = true };
        Vector up = new (0, 1, 0);
        double worst = 1, wrongWay = 1;

        for (int i = 3; i < Size; i += 7)
        {
            for (int j = 2; j < Size; j += 5)
            {
                Point point = new ((i + 0.5) / Size, 0, (j + 0.5) / Size);
                Vector bumped = bumps.PerturbAt(up, point);

                worst = Math.Min(worst, bumped.Dot(map.PerturbAt(up, point)));
                wrongWay = Math.Min(wrongWay, bumped.Dot(backward.PerturbAt(up, point)));
            }
        }

        Assert.IsTrue(worst > 0.9998, $"the map and the bumps stand within a degree or so ({worst})");
        Assert.IsTrue(wrongWay < 0.999, $"and green read the other way does not ({wrongWay})");
    }

    /// <summary>
    /// This helper returns a hit on a floor wearing the given material, from straight above the given place.
    /// </summary>
    private static Intersection HitOn(Material material, double x, double z)
    {
        Plane floor = new () { Material = material };
        Ray ray = new (new Point(x, 1, z), new Vector(0, -1, 0));
        Intersection hit = new (floor, 1);

        hit.PrepareUsing(ray, [hit]);

        return hit;
    }

    /// <summary>
    /// A material's roughness running by a pattern is read where the ray met it, in proportion between its two
    /// values; the material made there is kept, and the same one is handed back for the next hit like it.
    /// </summary>
    [TestMethod]
    public void TestARoughnessRunsByAPattern()
    {
        Material material = new ()
        {
            Roughness = 0.2,
            RoughnessBy = new MaterialMap
            {
                From = 0.2, To = 0.8, Pattern = PatternOf(Picture(2, 2, (_, _) => Gray(64 / 255.0)))
            }
        };
        Material there = material.MappedAt(HitOn(material, 0.3, 0.6), null);

        Assert.AreEqual(0.2 + 0.6 * Math.Round(64 / 255.0 * 256) / 256, there.Roughness, 1e-12);
        Assert.IsNull(there.Split, "between two rough ends the material is one roughness");
        Assert.IsNull(there.RoughnessBy, "the material made there is read already");
        Assert.AreSame(there, material.MappedAt(HitOn(material, 0.7, 0.2), null));
    }

    /// <summary>
    /// Between a smooth end of a roughness map and a rough one there is no single material, so the material there
    /// is shaded as each and the two mixed, as a blend between a smooth material and a rough one is.
    /// </summary>
    [TestMethod]
    public void TestASmoothEndOfARoughnessMapIsShadedAsEach()
    {
        Material material = new ()
        {
            RoughnessBy = new MaterialMap
            {
                From = 0, To = 0.6, Pattern = PatternOf(Picture(2, 2, (_, _) => Gray(128 / 255.0)))
            }
        };
        Material there = material.MappedAt(HitOn(material, 0.3, 0.6), null);
        double share = Math.Round(128 / 255.0 * 256) / 256;

        Assert.IsNotNull(there.Split);
        Assert.AreEqual(0, there.Split.Value.From.Roughness);
        Assert.AreEqual(0.6, there.Split.Value.To.Roughness, 1e-12);
        Assert.AreEqual(share, there.Split.Value.Share, 1e-12);
    }

    /// <summary>
    /// How much of a metal is bare runs by a metalness map as roughness runs by its own, each read alone.
    /// </summary>
    [TestMethod]
    public void TestHowMetallicAMaterialIsRunsByAPattern()
    {
        string name = Picture(2, 2, (_, _) => new Color(1, 64 / 255.0, 0));
        Material material = new ()
        {
            Metal = Metal.Named("gold"),
            Metallic = 0,
            Roughness = 0.3,
            RoughnessBy = new MaterialMap { From = 0.3, To = 0.5, Pattern = PatternOf(name, channel: ImageChannel.Red) },
            MetallicBy = new MaterialMap { From = 0, To = 1, Pattern = PatternOf(name, channel: ImageChannel.Green) }
        };
        Material there = material.MappedAt(HitOn(material, 0.5, 0.5), null);

        Assert.AreEqual(0.5, there.Roughness, 1e-12);
        Assert.AreEqual(Math.Round(64 / 255.0 * 256) / 256, there.Metallic, 1e-12);
    }

    /// <summary>
    /// This helper renders a small scene whose ball wears the given material entries, beside the pictures it names,
    /// and returns what went wrong, if anything did.
    /// </summary>
    private string ErrorFrom(string material)
    {
        string path = Path.Combine(_directory, "scene.igl");
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        File.WriteAllText(path,
            "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
            "point light { location [-4, 6, -8] }\n" +
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
    /// Every way of writing a picture that drives more than color reads and renders: a picture's gray picking among
    /// pigments, a channel of it doing so, its bumps, a normal map drawn either way, and a roughness and a metalness
    /// running by one.
    /// </summary>
    [TestMethod]
    public void TestEveryFormReadsAndRenders()
    {
        string picture = Picture(4, 4, (x, y) => new Color(x / 3.0, y / 3.0, 0.5));

        File.Copy(Path.Combine(_directory, picture), Path.Combine(_directory, "map.png"));

        foreach (string material in new[]
                 {
                     "pigment image 'map.png' { [0, Black, 1, White] }",
                     "pigment image 'map.png' spherical channel green { [0, Black, 1, White]  scale 2 }",
                     "pigment image 'map.png' { banded [0, Black, 0.5, White] }",
                     "pigment Red  normal image 'map.png' spherical { depth 0.5 }",
                     "pigment Red  normal map 'map.png' spherical { }",
                     "pigment Red  normal map 'map.png' spherical directx { depth 0.5  scale 2 }",
                     "pigment Red  roughness 0.2 to 0.8 by image 'map.png' spherical channel red { }",
                     "pigment Red  roughness 0 to 0.5 by bozo { scale 0.2 }",
                     "pigment Red  metal 'gold'  metallic 0 to 1 by image 'map.png' spherical { }",
                     "pigment Red  metallic 0.2 to 0.9 by image 'map.png' spherical channel blue { }"
                 })
            Assert.IsNull(ErrorFrom(material), material);
    }

    /// <summary>
    /// What cannot be said is refused, saying why: only roughness and metalness run by a pattern, a range needs the
    /// pattern after it, and a channel makes a picture a pattern, which needs pigments to pick among.
    /// </summary>
    [TestMethod]
    public void TestWhatCannotBeSaidIsRefused()
    {
        File.Copy(Path.Combine(_directory, Picture(2, 2, (_, _) => Gray(0.5))), Path.Combine(_directory, "map.png"));

        StringAssert.Contains(ErrorFrom("ambient 0.1 to 0.3 by bozo { }"),
            "Only a material's roughness, and how metallic it is, may run from one value to another by a pattern.");
        StringAssert.Contains(ErrorFrom("roughness 0.2 to 0.8  reflective 0.1"),
            "Expecting \"by\" and the pattern that sets roughness to follow here.");
        StringAssert.Contains(ErrorFrom("pigment image 'map.png' channel red"),
            "A channel makes a picture a pattern, so a map of pigments must follow it in braces.");
    }
}
