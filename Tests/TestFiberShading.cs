using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Patterns;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// Strands of hair shaded in a scene: lit as fibers in the world's own frame and units, lit from behind
/// without shadowing themselves, letting part of a light's way through, and colored at their roots.
/// </summary>
[TestClass]
public class TestFiberShading
{
    /// <summary>
    /// A strand that absorbs nothing, under a sky white in every direction, comes out white: all the
    /// light reaching it from every side is sent on, and the share of it sent toward the eye is what the
    /// eye sees.  This holds the sky's samples to the whole sphere, and their weight and the π in a
    /// light's units to the fiber's own scattering.
    /// </summary>
    [TestMethod]
    public void TestAClearStrandUnderAWhiteSkyComesOutWhite()
    {
        Scene scene = new ();

        scene.Lights.Add(new SkyLight { Color = new Color(1, 1, 1), Samples = 8192 });
        scene.Surfaces.Add(Horizontal(new Material { Melanin = new Melanin(0, 0), Ambient = 0 }));
        scene.ArrangeSurfaces();

        Color seen = scene.GetColorFor(new Ray(new Point(0, 0.03, -5), new Vector(0, 0, 1)));

        Assert.AreEqual(1, seen.Red, 0.02, $"came out {seen}");
    }

    /// <summary>
    /// A strand lit from straight behind glows with what passes through it, and exactly as its own
    /// scattering says: a shadow ray setting off from the strand must not meet the strand itself, or
    /// what passes through it would be taken away a second time.
    /// </summary>
    [TestMethod]
    public void TestABackLitStrandGlowsAndDoesNotShadowItself()
    {
        Material blond = new () { Melanin = Melanin.Shades["blond"], Ambient = 0 };
        Scene scene = new ();

        scene.Lights.Add(new PointLight { Location = new Point(0, 0, 1000) });
        scene.Surfaces.Add(Horizontal(blond));
        scene.ArrangeSurfaces();

        Color seen = scene.GetColorFor(new Ray(new Point(0, 0, -5), new Vector(0, 0, 1)));
        Fiber fiber = Fiber.For(Fiber.DefaultRoughness);
        RgbSpectrum absorption = RgbSpectrum.FromUnbounded(Fiber.AbsorptionFor(blond.Melanin.Value));
        // Looking along +Z at the middle of a strand along +X: the light comes from straight behind it,
        // half a turn round the strand from the eye.
        double expected = Math.PI * fiber.Scattered(fiber.Seeing(0, Math.PI / 2, 0, absorption), 0, -Math.PI / 2)[0];

        Assert.IsTrue(expected > 0.01, "the test's light gives no glow to look for");
        Assert.AreEqual(expected, seen.Red, expected * 1e-6, $"came out {seen}");
    }

    /// <summary>
    /// A strand's highlight is where a cylinder of its width would mirror the lamp: an eye looking past
    /// the strand's middle above it sees a lamp mirrored up and back toward the eye, and not one the same
    /// angle down.
    /// </summary>
    [TestMethod]
    public void TestTheHighlightIsWhereTheStrandMirrorsTheLamp()
    {
        Color Lit(double y)
        {
            Scene scene = new ();

            // Halfway out, the round strand's surface faces 30 degrees up, so the ray along +Z mirrors
            // to 60 degrees up from straight back toward the eye.
            scene.Lights.Add(new PointLight { Location = new Point(0, y * 0.866 * 1000, -0.5 * 1000) });
            scene.Surfaces.Add(Horizontal(new Material { Melanin = Melanin.Shades["black"], Ambient = 0 }));
            scene.ArrangeSurfaces();

            return scene.GetColorFor(new Ray(new Point(0, 0.05, -5), new Vector(0, 0, 1)));
        }

        Color mirrored = Lit(1);
        Color opposite = Lit(-1);

        Assert.IsTrue(mirrored.Red > 3 * opposite.Red, $"mirrored {mirrored}, opposite {opposite}");
    }

    /// <summary>
    /// A shadow ray to a surface beneath hair keeps what each strand it crosses sends onward: next to none
    /// through black hair, most through blond, more through hair with no melanin at all -- and red hair
    /// sends red on.
    /// </summary>
    [TestMethod]
    public void TestAShadowRayKeepsWhatPassesThroughAStrand()
    {
        Color Reaching(Melanin melanin)
        {
            Scene scene = new ();
            Strand strand = new () { Material = new Material { Melanin = melanin } };

            strand.Points.Add(new RibbonControlPoint { Position = new Point(0, 0.5, -1), Width = 0.3 });
            strand.Points.Add(new RibbonControlPoint { Position = new Point(0, 0.5, 1), Width = 0.3 });
            strand.PrepareForRendering();
            scene.Surfaces.Add(strand);
            scene.ArrangeSurfaces();

            return scene.GetLightReaching(Point.Zero, new Vector(0, 1, 0), 10);
        }

        Color clear = Reaching(new Melanin(0, 0));
        Color blond = Reaching(Melanin.Shades["blond"]);
        Color black = Reaching(Melanin.Shades["black"]);
        Color ginger = Reaching(Melanin.Shades["ginger"]);

        Assert.AreEqual(0.9, clear.Red, 0.03, $"clear let through {clear}");
        Assert.IsTrue(blond.Red is > 0.7 and < 0.88, $"blond let through {blond}");
        Assert.IsTrue(black.Red < 0.06, $"black let through {black}");
        Assert.IsTrue(ginger.Red > 2 * ginger.Blue, $"ginger let through {ginger}");
    }

    /// <summary>
    /// Hair wearing its skin's material is colored as the skin is where each strand grows, read in the
    /// skin's own space, however far from there the strand has strayed: a sphere striped red and blue
    /// across its middle grows red fur on one half and blue on the other, even where a strand leans
    /// across onto the other half.
    /// </summary>
    [TestMethod]
    public void TestFurIsColoredAsTheSkinWhereItGrows()
    {
        PigmentSet stripes = new ();

        stripes.AddEntry(new SolidPigment(new Color(0.2, 0.3, 0.8)));
        stripes.AddEntry(new SolidPigment(new Color(0.8, 0.3, 0.2)));

        Sphere sphere = new ()
        {
            Material = new Material { Pigment = new PatternPigment { Pattern = new StripedPattern(), PigmentSet = stripes } },
            Transform = Transforms.Scale(0.5),
            HairGrowth = new HairGrowth { Count = 2000, Length = 0.8, Lean = 1.3, Thickness = 0.05 }
        };
        Scene scene = new ();
        Random random = new (3);
        int strayed = 0;

        sphere.PrepareForRendering();
        sphere.Hair.WearWhatItGrewFrom();
        scene.Surfaces.Add(sphere);
        scene.ArrangeSurfaces();

        List<List<Point>> paths = sphere.Hair.Paths();

        for (int index = 0; index < 600; index++)
        {
            Ray ray = new (
                new Point(random.NextDouble() * 2.6 - 1.3, random.NextDouble() * 2.6 - 1.3, -5), new Vector(0, 0, 1));

            foreach (HairIntersection hit in scene.Intersect(ray).OfType<HairIntersection>())
            {
                Point where = ray.At(hit.Distance);
                bool red = paths[hit.Strand][0].X < 0;
                Color matched = scene.DyeOf(hit, where).Matched;

                Assert.AreEqual(red, matched.Red > matched.Blue, $"a strand rooted at {paths[hit.Strand][0]} came out {matched}");

                if (where.X < 0 != red)
                    strayed++;
            }
        }

        Assert.IsTrue(strayed > 20, "too few strands strayed across to tell root from tip");
    }

    /// <summary>
    /// A spectral render of hair under white light comes out about the color an ordinary one does, lit
    /// from behind, where all there is to see is light passed through the strand, and lit flat, where all
    /// there is is the color the hair is matched to.  Black hair lit flat stays black: every smooth
    /// spectrum fitted to the color it is matched to, which is very dark and very red, came out bright
    /// at both ends of the visible range and turned it purple.
    /// </summary>
    [TestMethod]
    public void TestASpectralRenderOfHairMatchesAnOrdinaryOne()
    {
        foreach (string shade in new[] { "black", "brown", "ginger", "blond" })
        {
            Compare(shade, "from behind", new Material { Melanin = Melanin.Shades[shade], Ambient = 0 }, new Point(0, 0, 1000));
            Compare(shade, "flat", new Material { Melanin = Melanin.Shades[shade], Ambient = 1 }, new Point(0, -1000, 0));
        }

        (_, Color black) = Seen(new Material { Melanin = Melanin.Shades["black"], Ambient = 1 }, new Point(0, -1000, 0));

        Assert.IsTrue(black.Red < 0.001 && black.Green < 0.001 && black.Blue < 0.001, $"black hair came out {black}");

        return;

        static void Compare(string shade, string lit, Material material, Point light)
        {
            (Color ordinary, Color spectral) = Seen(material, light);

            // Absorption is exponential, so it does not come out exactly the same summed over a channel's
            // wavelengths as worked out for the channel at once; the more saturated the hair, the more so.
            double allowed = 0.15 * Math.Max(ordinary.Red, Math.Max(ordinary.Green, ordinary.Blue)) + 0.008;

            foreach ((double one, double other) in new[]
                     {
                         (ordinary.Red, spectral.Red), (ordinary.Green, spectral.Green), (ordinary.Blue, spectral.Blue)
                     })
                Assert.AreEqual(one, other, allowed, $"{shade} lit {lit}: {ordinary} against {spectral}");
        }

        static (Color Ordinary, Color Spectral) Seen(Material material, Point light)
        {
            Scene scene = new ();

            scene.Lights.Add(new PointLight { Location = light });
            scene.Surfaces.Add(Horizontal(material));
            scene.ArrangeSurfaces();

            Ray ray = new (new Point(0, 0.02, -5), new Vector(0, 0, 1));

            return (scene.GetColorFor<RgbSpectrum>(ray).ToColor(), scene.GetColorFor<BandSpectrum>(ray).ToColor());
        }
    }

    /// <summary>
    /// This method returns a strand two long along X, through the origin, a fifth wide.
    /// </summary>
    private static Strand Horizontal(Material material)
    {
        Strand strand = new () { Material = material };

        strand.Points.Add(new RibbonControlPoint { Position = new Point(-1, 0, 0), Width = 0.2 });
        strand.Points.Add(new RibbonControlPoint { Position = new Point(1, 0, 0), Width = 0.2 });
        strand.PrepareForRendering();

        return strand;
    }
}
