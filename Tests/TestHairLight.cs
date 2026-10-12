using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// The light scattered among the strands of a head of hair, gathered ahead of time: that the grid holds
/// what it is given, that gathering it comes out the same every time, and that a head of hair lit with it
/// comes out as bright as following the light the long way round says.
/// </summary>
[TestClass]
public class TestHairLight
{
    /// <summary>
    /// A beam of light across the grid, of one unit of irradiance, has a fluence of one, all of it flowing
    /// the beam's way: so light arrives from behind the beam four times as brightly as on average, and none
    /// arrives from ahead of it.
    /// </summary>
    [TestMethod]
    public void TestABeamIsHeldAsItsFluenceAndFlow()
    {
        HairLight grid = new (Point.Zero, new Point(1, 1, 1), 0.1);
        float[] tally = new float[HairLight.Quantities * grid.Size];
        const int lines = 200;

        for (int across = 0; across < lines; across++)
        {
            for (int up = 0; up < lines; up++)
            {
                double y = (across + 0.5) / lines, z = (up + 0.5) / lines;

                grid.Record(
                    tally, new Point(-0.5, y, z), new Point(1.5, y, z), RgbSpectrum.White * (1.0 / (lines * lines)));
            }
        }

        grid.Settle([tally]);

        HairLight.Here middle = grid.At(new Point(0.5, 0.5, 0.5));

        Assert.AreEqual(1 / (4 * Math.PI), middle.Everywhere<RgbSpectrum>()[0], 1e-4);
        Assert.AreEqual(4 / (4 * Math.PI), middle.From<RgbSpectrum>(new Vector(-1, 0, 0))[0], 1e-3);
        Assert.AreEqual(0, middle.From<RgbSpectrum>(new Vector(1, 0, 0))[0], 1e-9);
        Assert.AreEqual(1 / (4 * Math.PI), middle.From<RgbSpectrum>(new Vector(0, 1, 0))[0], 1e-4);
    }

    /// <summary>
    /// Gathering the light among the strands comes out the same every time, though it is gathered on as
    /// many threads as there are.
    /// </summary>
    [TestMethod]
    public void TestGatheringComesOutTheSameEveryTime()
    {
        (Scene first, Sphere one) = FurBall(Melanin.Shades["blond"], 3000, 0.4);
        (Scene second, Sphere other) = FurBall(Melanin.Shades["blond"], 3000, 0.4);
        HairLight light = first.LightAmong(one.Hair, null);
        HairLight again = second.LightAmong(other.Hair, null);

        for (double x = -1.2; x <= 1.2; x += 0.3)
        {
            Point point = new (x, 0.4, -0.9);

            Assert.AreEqual(
                light.At(point).From<RgbSpectrum>(new Vector(0, 0, -1)), again.At(point).From<RgbSpectrum>(new Vector(0, 0, -1)));
        }
    }

    /// <summary>
    /// Fur lit with the light gathered among its strands comes out as bright as following every path of the
    /// light the long way round, strand to strand, says it should -- for fur with no melanin, where nearly
    /// all of the light is light bounced among the strands, and for blond and brown.  The long way round
    /// treats every strand as stopping light outright and follows each path from strand to strand by
    /// <see cref="Fiber.Sample{TS}"/>, so it shares nothing with the gathering but the strands' scattering.
    /// </summary>
    [TestMethod]
    public void TestFurIsAsBrightAsTheLongWayRoundSays()
    {
        foreach (Melanin melanin in new[] { new Melanin(0, 0), Melanin.Shades["blond"], Melanin.Shades["brown"] })
        {
            (Scene scene, _) = FurBall(melanin, 6000, 0.5);
            Random random = new (5);
            RgbSpectrum gathered = RgbSpectrum.Black, longWay = RgbSpectrum.Black, once = RgbSpectrum.Black;
            const int grid = 24, paths = 24;

            for (int i = 0; i < grid; i++)
            {
                for (int j = 0; j < grid; j++)
                {
                    Ray ray = new (
                        new Point(-1.6 + 3.2 * (i + 0.5) / grid, -1.6 + 3.2 * (j + 0.5) / grid, -10), new Vector(0, 0, 1));

                    gathered += scene.GetColorFor<RgbSpectrum>(ray);

                    for (int path = 0; path < paths; path++)
                    {
                        (RgbSpectrum all, RgbSpectrum first) = TheLongWay(scene, ray, random);

                        longWay += all * (1.0 / paths);
                        once += first * (1.0 / paths);
                    }
                }
            }

            Assert.AreEqual(longWay[1], gathered[1], longWay[1] * 0.15, $"{melanin}: gathered {gathered.ToColor()}, the long way {longWay.ToColor()}");

            if (melanin.Eumelanin == 0)
                Assert.IsTrue(longWay[1] > 4 * once[1], "the light among the strands was not most of the light");
        }
    }

    private static readonly Point Lamp = new (-10, 10, -10);

    /// <summary>
    /// This method returns a ball of black skin, of radius one, growing fur of the given melanin, lit by
    /// one lamp.
    /// </summary>
    private static (Scene Scene, Sphere Ball) FurBall(Melanin melanin, int strands, double length)
    {
        Sphere sphere = new ()
        {
            Material = new Material { Pigment = new SolidPigment(new Color(0, 0, 0)), Ambient = 0, Diffuse = 0, Specular = 0 },
            HairGrowth = new HairGrowth
            {
                Count = strands, Length = length, Thickness = 0.012, Lean = 0.5,
                Material = new Material { Melanin = melanin, Ambient = 0 }
            }
        };
        Scene scene = new ();

        scene.Lights.Add(new PointLight { Location = Lamp });
        sphere.PrepareForRendering();
        scene.Surfaces.Add(sphere);
        scene.ArrangeSurfaces();

        return (scene, sphere);
    }

    /// <summary>
    /// This method follows one path of light back from the eye, strand to strand, adding the lamp's light
    /// wherever it can see the lamp past everything: the light the eye gets along it, and the part of that
    /// from the first strand alone.
    /// </summary>
    private static (RgbSpectrum All, RgbSpectrum First) TheLongWay(Scene scene, Ray ray, Random random)
    {
        RgbSpectrum light = RgbSpectrum.Black, first = RgbSpectrum.Black, carried = RgbSpectrum.White;
        (List<Intersection> hits, HairIntersection hit) = Nearest(scene, ray);

        for (int bounce = 0; bounce < 256 && hit is not null; bounce++)
        {
            hit.PrepareUsing(ray, hits);

            Scene.StrandFrame frame = Scene.FrameOf(hit);
            Scene.Dye dye = scene.DyeOf(hit, hit.Point);
            Fiber fiber = Fiber.For(dye.Roughness);
            Fiber.Seen<RgbSpectrum> seen = fiber.Seeing(frame.SineOf(hit.Eye), Math.PI / 2, frame.H, dye.Absorbing<RgbSpectrum>());
            Vector toward = Lamp - frame.Center;
            double distance = toward.Magnitude;

            toward = toward.Unit;

            if (!Nearest(scene, new Ray(frame.Center + toward * frame.Radius, toward)).Hits.Any(
                    crossing => crossing.Distance > 0 && crossing.Distance < distance - frame.Radius))
            {
                RgbSpectrum direct = fiber.Scattered(seen, frame.SineOf(toward), frame.AngleOf(toward)) * Math.PI;

                light += carried * direct;

                if (bounce == 0)
                    first = direct;
            }

            (double sinTheta, double phi, double odds) = fiber.Sample(
                seen, random.NextDouble(), random.NextDouble(), random.NextDouble(), random.NextDouble());

            if (odds <= 0)
                break;

            carried *= fiber.Scattered(seen, sinTheta, phi) * (1 / odds);

            if (bounce >= 3)
            {
                double survives = Math.Min(0.95, Math.Max(carried[0], Math.Max(carried[1], carried[2])));

                if (random.NextDouble() >= survives)
                    break;

                carried *= 1 / survives;
            }

            Vector onward = frame.Toward(sinTheta, phi);

            ray = new Ray(frame.Center + onward * frame.Radius, onward);
            (hits, hit) = Nearest(scene, ray);
        }

        return (light, first);
    }

    private static (List<Intersection> Hits, HairIntersection Hit) Nearest(Scene scene, Ray ray)
    {
        List<Intersection> hits = scene.Intersect(ray);

        return (hits, hits.FirstOrDefault(crossing => crossing.Distance > 0) as HairIntersection);
    }
}
