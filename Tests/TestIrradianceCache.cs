using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Pigments;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the cache of bounced light: the far light worked out at scattered places and
/// blended between them, and what is near looked for at every place itself.
/// </summary>
[TestClass]
public class TestIrradianceCache
{
    private static readonly Vector Up = new (0, 1, 0);

    private static IrradianceCache<RgbSpectrum>.Record RecordAt(Point place, double far, double open = 1) =>
        new (place, Up, RgbSpectrum.From(new Color(far, far, far)), open, 1);

    private static IrradianceCache<RgbSpectrum> CacheOf(params IrradianceCache<RgbSpectrum>.Record[] records)
    {
        IrradianceCache<RgbSpectrum> cache = new (0.2);

        for (int index = 0; index < records.Length; index++)
            cache.Offer(index, records[index]);

        cache.EndPass();

        return cache;
    }

    /// <summary>
    /// This tests that a record covers the places round it that face its way, out to the share of its
    /// reach the cache's error allows, and gives them its far light and how near is near.
    /// </summary>
    [TestMethod]
    public void TestARecordCoversItsNeighborhood()
    {
        IrradianceCache<RgbSpectrum> cache = CacheOf(RecordAt(Point.Zero, 0.5));

        Assert.IsTrue(cache.TryBlend(new Point(0.05, 0, 0.05), Up, out IrradianceCache<RgbSpectrum>.Blend near));
        Assert.AreEqual(0.5, near.Far.ToColor().Red, 1e-9);
        Assert.IsTrue(near.HasFar);
        Assert.AreEqual(0.4, near.Near, 1e-9, "twice as far as it holds good for");
        Assert.IsFalse(cache.TryBlend(new Point(0.3, 0, 0), Up, out _), "beyond its reach");
        Assert.IsFalse(cache.TryBlend(new Point(0.05, 0, 0), new Vector(1, 0, 0), out _), "turned away from it");
        Assert.IsFalse(cache.TryBlend(new Point(0.05, -0.1, 0), Up, out _), "below it, with it in front");
    }

    /// <summary>
    /// This tests that places between records blend them, weighing the nearer more, and that a record's
    /// weight falls to nothing at the edge of its reach rather than stopping there.
    /// </summary>
    [TestMethod]
    public void TestRecordsBlendSmoothly()
    {
        IrradianceCache<RgbSpectrum> cache = CacheOf(RecordAt(Point.Zero, 0.2), RecordAt(new Point(0.3, 0, 0), 0.6));

        double At(double x) => cache.TryBlend(new Point(x, 0, 0), Up, out IrradianceCache<RgbSpectrum>.Blend blend)
            ? blend.Far.ToColor().Red
            : double.NaN;

        Assert.AreEqual(0.4, At(0.15), 1e-9, "halfway between, the two count alike");
        Assert.IsLessThan(0.4, At(0.12), "nearer the dimmer, the dimmer counts more");
        Assert.AreEqual(0.2, At(0.101), 0.005, "just inside the edge of the brighter's reach it counts next to nothing");
    }

    /// <summary>
    /// This tests that a record counts toward the far light in proportion to the share of its directions
    /// that went far, and that a place covered only by records that saw nothing far is covered still, but
    /// told there is no far light to be had.
    /// </summary>
    [TestMethod]
    public void TestARecordThatSawLittleFarSaysLittleOfIt()
    {
        IrradianceCache<RgbSpectrum> cache = CacheOf(
            RecordAt(new Point(-0.05, 0, 0), 0.2), RecordAt(new Point(0.05, 0, 0), 0.6, open: 0.25));

        Assert.IsTrue(cache.TryBlend(Point.Zero, Up, out IrradianceCache<RgbSpectrum>.Blend blend));
        Assert.AreEqual((0.2 + 0.6 * 0.25) / 1.25, blend.Far.ToColor().Red, 1e-9);

        IrradianceCache<RgbSpectrum> shut = CacheOf(RecordAt(Point.Zero, 0, open: 0));

        Assert.IsTrue(shut.TryBlend(new Point(0.05, 0, 0), Up, out IrradianceCache<RgbSpectrum>.Blend none));
        Assert.IsFalse(none.HasFar);
    }

    /// <summary>
    /// This tests the cache where the light arriving is the same everywhere -- the furnace of a glowing floor
    /// and ceiling -- where blending records changes nothing and the answer must be exact: the glow, 0.2,
    /// with two bounces giving back half and a quarter of it.
    /// </summary>
    [TestMethod]
    public void TestTheCacheIsExactWhereTheLightIsTheSameEverywhere()
    {
        Material glowing = new ()
        {
            Pigment = new SolidPigment(Colors.White), Ambient = 0, Emission = new Color(0.2, 0.2, 0.2),
            Diffuse = 0.5, Specular = 0
        };
        Plane floor = new () { Material = glowing };
        Plane ceiling = new ()
        {
            Material = glowing, Transform = Transforms.Translate(0, 1, 0) * Transforms.RotateAroundX(Math.PI, true)
        };
        Scene scene = new () { DiffuseBounces = 2, DiffuseSamples = 16, DiffuseCacheError = 0.15 };

        floor.PrepareForRendering();
        ceiling.PrepareForRendering();
        scene.Surfaces.Add(floor);
        scene.Surfaces.Add(ceiling);

        // Looking down at the floor from a grid of places, each ray a pixel wide where it lands.
        Ray Down(int x, int y) => new (new Point(x * 0.05, 0.9, y * 0.05), Directions.Down, spread: 0.01);

        int records = scene.FillTheBounceCache<RgbSpectrum>(Down, 40, 40);

        Assert.IsGreaterThan(0, records, "the cache should hold records");
        Assert.IsLessThan(40 * 40, records, "a floor that is the same everywhere should need fewer records than places");
        Assert.AreEqual(0.35, Shade(scene, Down(13, 7)), 1e-9);
    }

    /// <summary>
    /// This tests that what is near a place is seen by the place itself, where the cache's records would
    /// lend it light it cannot see: the floor just under the edge of a sheet hung a little above it, as a
    /// rug lies on a floor.  The only light is a glowing ceiling and the sheet is black, so the floor
    /// there takes its half of the glow from just the directions that pass out from under the sheet's edge
    /// -- those that rise no faster than they run out, from a place as far in as the sheet is high: a share
    /// (1 - 1/√2)/2 of a matte surface's view.  The records beside the sheet, which see the glow almost
    /// everywhere, cover the place; blended whole, they gave it six times its light.
    /// </summary>
    [TestMethod]
    public void TestThePlaceUnderAnEdgeSeesTheEdgeItself()
    {
        const double height = 0.002;
        double expected = 0.5 * 0.2 * (1 - 1 / Math.Sqrt(2)) / 2;

        Plane floor = new ()
        {
            Material = new Material
            {
                Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0.5, Specular = 0
            }
        };
        Plane ceiling = new ()
        {
            Material = new Material
            {
                Pigment = new SolidPigment(Colors.White), Ambient = 0, Emission = new Color(0.2, 0.2, 0.2),
                Diffuse = 0, Specular = 0
            },
            Transform = Transforms.Translate(0, 1, 0) * Transforms.RotateAroundX(Math.PI, true)
        };
        Parallelogram sheet = new ()
        {
            Point = new Point(-10, height, -10), Side1 = new Vector(10, 0, 0), Side2 = new Vector(0, 0, 20),
            Material = new Material
            {
                Pigment = new SolidPigment(Colors.Black), Ambient = 0, Diffuse = 0, Specular = 0
            }
        };
        Scene scene = new () { DiffuseBounces = 1, DiffuseSamples = 4096, DiffuseCacheError = 0.15 };

        foreach (Surface surface in new Surface[] { floor, ceiling, sheet })
        {
            surface.PrepareForRendering();
            scene.Surfaces.Add(surface);
        }

        // Looking down at the open floor beside the sheet, each ray two sheet-heights wide where it lands.
        Ray Down(int x, int y) => new (
            new Point(height + x * 2 * height, 0.9, (y - 25) * 2 * height), Directions.Down, spread: 0.0045);

        scene.FillTheBounceCache<RgbSpectrum>(Down, 50, 50);

        Assert.IsTrue(
            scene.CacheFor<RgbSpectrum>().TryBlend(new Point(-height, 0, 0), Up, out _),
            "the records beside the sheet should cover the floor under its edge");
        Assert.AreEqual(
            expected, Shade(scene, new Ray(new Point(-height, height / 2, 0), Directions.Down, spread: 0.0045)),
            0.04 * expected);
    }

    /// <summary>
    /// This tests that a place the cache covers looks no further than what is near: a direction meeting
    /// nothing nearer takes the far light the cache gives, rather than finding what is there.  Here a record
    /// planted under a glowing ceiling says the far light is 0.7 where the ceiling gives 0.2; the floor,
    /// glowing 0.2 of its own and taking half, shows 0.2 + 0.35, not 0.2 + 0.1.
    /// </summary>
    [TestMethod]
    public void TestADirectionMeetingNothingNearTakesTheFarLight()
    {
        Material glowing = new ()
        {
            Pigment = new SolidPigment(Colors.White), Ambient = 0, Emission = new Color(0.2, 0.2, 0.2),
            Diffuse = 0.5, Specular = 0
        };
        Plane floor = new () { Material = glowing };
        Plane ceiling = new ()
        {
            Material = glowing, Transform = Transforms.Translate(0, 1, 0) * Transforms.RotateAroundX(Math.PI, true)
        };
        Scene scene = new () { DiffuseBounces = 1, DiffuseSamples = 256, DiffuseCacheError = 0.15 };

        floor.PrepareForRendering();
        ceiling.PrepareForRendering();
        scene.Surfaces.Add(floor);
        scene.Surfaces.Add(ceiling);

        IrradianceCache<RgbSpectrum> cache = scene.CacheFor<RgbSpectrum>();

        cache.Offer(0, RecordAt(Point.Zero, 0.7));
        cache.EndPass();

        Assert.AreEqual(0.2 + 0.5 * 0.7, Shade(scene, new Ray(new Point(0.05, 0.5, 0), Directions.Down)), 1e-9);
    }

    private static double Shade(Scene scene, Ray ray)
    {
        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.Hit();

        hit.PrepareUsing(ray, hits);

        return scene.GetHitColor(hit, 4).Red;
    }

    /// <summary>
    /// This tests the cache against the light worked out afresh at every place, in a Cornell box: region by
    /// region the two must agree, within a few hundredths, and two renders with the cache must be the same
    /// picture -- the cache being filled in passes that read only what earlier passes set down.
    /// </summary>
    [TestMethod]
    public void TestTheCacheAgreesWithTheLightWorkedOutAfresh()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"cache-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            Canvas Render(string settings, string name)
            {
                string path = Path.Combine(directory, $"{name}.igl");
                string output = Path.Combine(directory, $"{name}.png");

                File.WriteAllText(path,
                    $"context {{ no gamma  diffuse bounces 2  {settings} }}\n" +
                    "camera { location [0, 1, -3.4]  look at [0, 1, 0]  field of view 40 }\n" +
                    "point light { location [0, 1.9, 0] }\n" +
                    "plane { material { pigment White  diffuse 0.8  specular 0 } }\n" +
                    "plane { material { pigment White  diffuse 0.8  specular 0 }  rotate X 180  translate Y 2 }\n" +
                    "plane { material { pigment White  diffuse 0.8  specular 0 }  rotate X -90  translate Z 1 }\n" +
                    "plane { material { pigment Red  diffuse 0.8  specular 0 }  rotate Z -90  translate X -1 }\n" +
                    "plane { material { pigment Green  diffuse 0.8  specular 0 }  rotate Z 90  translate X 1 }\n" +
                    "cube { material { pigment White  diffuse 0.8  specular 0 }  scale 0.3  translate [0.2, 0.3, 0] }\n");

                ImageRenderer renderer = new LanguageParser(path).Parse();

                Assert.IsNotNull(renderer, "the scene did not parse");

                renderer.Render(new RenderOptions
                {
                    OutputFileName = output, Width = 48, Height = 48, BitsPerChannel = 16, ProgressStyleText = "none"
                });

                return new ImageFile(output).Load()[0];
            }

            Canvas fresh = Render("diffuse samples 1024", "fresh");
            Canvas cached = Render("diffuse samples 1024  diffuse cache", "cached");
            Canvas again = Render("diffuse samples 1024  diffuse cache", "again");

            for (int y = 0; y < cached.Height; y++)
            {
                for (int x = 0; x < cached.Width; x++)
                    Assert.IsTrue(cached.GetPixel(x, y).Matches(again.GetPixel(x, y)), $"two renders differed at ({x}, {y})");
            }

            // Regions of six by six pixels, compared by their average light: the worst is off by about 2%.
            for (int top = 0; top < 48; top += 6)
            {
                for (int left = 0; left < 48; left += 6)
                {
                    double one = 0, other = 0;

                    for (int y = top; y < top + 6; y++)
                    {
                        for (int x = left; x < left + 6; x++)
                        {
                            Color a = fresh.GetPixel(x, y), b = cached.GetPixel(x, y);

                            one += a.Red + a.Green + a.Blue;
                            other += b.Red + b.Green + b.Blue;
                        }
                    }

                    Assert.AreEqual(one, other, Math.Max(0.05 * one, 0.02), $"the region at ({left}, {top})");
                }
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
