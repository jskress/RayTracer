using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the tree a scene's many fading lights are held in, so that a place looks at a few, picked by how
/// much each could give it.  What it must be is right on average -- the lights it picks, each weighed by one over its
/// chance, add up to what every light would give -- while costing a handful of lights where every light cost all.
/// </summary>
[TestClass]
public class TestLightTree
{
    /// <summary>
    /// Lamps scattered through a box forty meters across and five up, each fading past two meters.
    /// </summary>
    private static List<Light> Lamps(int count, double fade = 2)
    {
        List<Light> lamps = [];

        for (int index = 0; index < count; index++)
        {
            lamps.Add(new PointLight
            {
                Location = new Point(
                    (ScatterGenerator.At(index, 0) - 0.5) * 40, 1 + ScatterGenerator.At(index, 1) * 5,
                    (ScatterGenerator.At(index, 2) - 0.5) * 40),
                Color = new Color(0.5 + ScatterGenerator.At(index, 3), 1, 1),
                FadeDistance = fade
            });
        }

        return lamps;
    }

    /// <summary>
    /// What a lamp gives a place on the ground facing up, with nothing in its way.
    /// </summary>
    private static double Given(Light light, Point at)
    {
        Vector toward = ((PointLight) light).Location - at;
        double distance = toward.Magnitude;

        return light.Color.Red * light.FadingOver(distance) * light.IntensityToward(at) *
               Math.Max(0, toward.Dot(new Vector(0, 1, 0)) / distance);
    }

    private static Point Place(int index) =>
        new ((ScatterGenerator.At(index, 7) - 0.5) * 50, 0, (ScatterGenerator.At(index, 8) - 0.5) * 50);

    /// <summary>
    /// The lights a tree picks, each weighed as its picking says, add up on average over many places to what every
    /// light gives them: within a percent over four thousand places, each taking sixteen picks among three hundred
    /// lamps; and each place's own answer is not far off its own truth either.
    /// </summary>
    [TestMethod]
    public void TestPicksAreRightOnAverage()
    {
        List<Light> lamps = Lamps(300);
        LightTree tree = LightTree.Of(lamps, 1, false, 16, 0.25);
        List<(Light Light, double Weight, bool Held)> picks = [];
        double estimated = 0;
        double exact = 0;
        double squares = 0;
        int looked = 0;

        Assert.IsNotNull(tree, "no tree was built of three hundred fading lamps");

        for (int index = 0; index < 4000; index++)
        {
            Point at = Place(index);
            double truth = lamps.Sum(lamp => Given(lamp, at));

            picks.Clear();
            tree.Choose(at, new Vector(0, 1, 0), true, 16, picks);

            double guess = picks.Where(pick => pick.Held).Sum(pick => pick.Weight * Given(pick.Light, at));

            estimated += guess;
            exact += truth;
            squares += (guess - truth) * (guess - truth) / (truth * truth);
            looked += picks.Count(pick => pick.Held);
        }

        double error = Math.Sqrt(squares / 4000);

        Assert.AreEqual(1, estimated / exact, 0.01, "the tree's picks were not right on average");
        Assert.IsTrue(error < 0.15, $"a place's own answer was {error:P0} off its truth, as a mean square");
        Assert.IsTrue(looked < 4000 * 60, $"a place looked at {looked / 4000.0:F1} of 300 lamps");
    }

    /// <summary>
    /// Guiding the picks toward where a glossy place mirrors the eye changes only how often each lamp is picked, never
    /// what it is worth once picked: the picks are as right on average with the guide as without.
    /// </summary>
    [TestMethod]
    public void TestAGlossyGuideKeepsThePicksRightOnAverage()
    {
        List<Light> lamps = Lamps(300);
        LightTree tree = LightTree.Of(lamps, 1, false, 16, 0.25);
        LightTree.Lobe lobe = new (new Vector(0.6, 0.8, 0), 60, 8);
        List<(Light Light, double Weight, bool Held)> picks = [];
        double estimated = 0;
        double exact = 0;

        for (int index = 0; index < 4000; index++)
        {
            Point at = Place(index);

            picks.Clear();
            tree.Choose(at, new Vector(0, 1, 0), true, 16, picks, lobe);
            exact += lamps.Sum(lamp => Given(lamp, at));
            estimated += picks.Where(pick => pick.Held).Sum(pick => pick.Weight * Given(pick.Light, at));
        }

        Assert.AreEqual(1, estimated / exact, 0.01, "a glossy guide put the picks off on average");
    }

    /// <summary>
    /// At a split of nought every node is opened, so every light that could light a place is looked at in full, each
    /// worth one: the exact answer, however many there are.
    /// </summary>
    [TestMethod]
    public void TestASplitOfNoughtLooksAtEveryLight()
    {
        List<Light> lamps = Lamps(100);
        LightTree tree = LightTree.Of(lamps, 1, false, 16, 0);
        List<(Light Light, double Weight, bool Held)> picks = [];
        Point at = new (0, 0, 0);

        tree.Choose(at, new Vector(0, 1, 0), false, 16, picks);

        List<(Light Light, double Weight, bool Held)> held = picks.Where(pick => pick.Held).ToList();

        Assert.AreEqual(100, held.Count, "a split of nought left lights out");
        Assert.IsTrue(picks.All(pick => pick.Weight == 1), "a split of nought weighed a light other than whole");
        CollectionAssert.AreEquivalent(lamps, held.Select(pick => pick.Light).ToList());
    }

    /// <summary>
    /// A tree is built unasked of more than sixteen lights that fade, and of as few as two when asked; never when
    /// refused.  Lights that do not fade -- a lamp that gives as much a kilometer off, a sun, a sky -- are never in it,
    /// and every place looks at them, whole.
    /// </summary>
    [TestMethod]
    public void TestATreeHoldsOnlyLightsThatFadeAndOnlyEnoughOfThem()
    {
        List<Light> sixteen = Lamps(16);
        List<Light> seventeen = Lamps(17);
        Light sun = new DistantLight { Direction = new Vector(0, -1, 0) };
        Light steady = new PointLight { Location = new Point(0, 3, 0) };

        Assert.IsNull(LightTree.Of(sixteen, 1, false, 16, 0.25), "a tree was built unasked of sixteen lights");
        Assert.IsNotNull(LightTree.Of(seventeen, 1, false, 16, 0.25), "no tree was built of seventeen");
        Assert.IsNotNull(LightTree.Of(Lamps(2), 1, true, 16, 0.25), "no tree was built of two when asked");

        seventeen.Add(sun);
        seventeen.Add(steady);

        LightTree tree = LightTree.Of(seventeen, 1, false, 4, 0.25);
        List<(Light Light, double Weight, bool Held)> picks = [];

        Assert.AreEqual(17, tree.Count, "the tree held a light that does not fade");
        CollectionAssert.AreEqual(new[] { sun, steady }, tree.Always, "a light that does not fade was not always looked at");

        tree.Choose(new Point(100, 0, 100), new Vector(0, 1, 0), true, 4, picks);

        Assert.IsTrue(picks.Take(2).Select(pick => pick.Light).SequenceEqual([sun, steady]) &&
                      picks.Take(2).All(pick => pick.Weight == 1 && !pick.Held),
            "the lights the tree does not hold were not first, whole");
        Assert.IsTrue(ReferenceEquals(picks[2].Light, tree.Ambient) && picks[2].Weight == 1 && !picks[2].Held,
            "the light standing for the held lights' ambient was not given, whole");
    }

    /// <summary>
    /// Every lamp lends a surface ambient light, which no distance thins and no shadow takes away, so the ambient of
    /// all a tree's lamps is given at every place by one light standing for them all, never picked: it carries all
    /// their light, reaches nowhere -- its sample worth nothing -- and a held lamp picked lends none of its own.  Left
    /// to the picks, five hundred lamps' ambient came only from the rare place that picked a far one, weighed as all
    /// of them: a café table under a festoon was dim oak where every lamp made it white, sparkling.
    /// </summary>
    [TestMethod]
    public void TestTheHeldLightsAmbientIsGivenWhole()
    {
        List<Light> lamps = Lamps(40);
        LightTree tree = LightTree.Of(lamps, 1, false, 4, 0.25);
        LightSample sample = tree.Ambient.SampleToward(Point.Zero, 0, new Vector(0, 1, 0));
        RgbSpectrum all = tree.Ambient.EmittedToward<RgbSpectrum>(sample);
        double red = lamps.Sum(lamp => lamp.Color.Red);

        Assert.AreEqual(0, sample.Cone, "the stand-in's sample reaches somewhere");
        Assert.AreEqual(red, all.ToColor().Red, 1e-9, "the stand-in does not carry all the held lights' light");

        string scene =
            "camera { location [0, 6, -9]  look at [0, 0, 0] }\n" +
            "plane { material { pigment [0.8, 0.8, 0.8]  ambient 0.01  specular 0 } }\n" +
            string.Join("\n", Enumerable.Range(0, 60).Select(index =>
                $"point light {{ location [{(index % 10 - 4.5) * 2}, 1.5, {(index / 10 - 2.5) * 2}]  fade distance 0.4  " +
                "color [0.3, 0.3, 0.3] }"));
        Canvas tree8 = Rendered("context { light tree samples 8 }\n" + scene);
        Canvas exact = Rendered("context { no light tree }\n" + scene);
        double differs = 0;

        for (int y = 0; y < exact.Height; y++)
        {
            for (int x = 0; x < exact.Width; x++)
                differs += Math.Abs(tree8.GetPixel(x, y).Red - exact.GetPixel(x, y).Red);
        }

        Assert.AreEqual(MeanOf(exact), MeanOf(tree8), MeanOf(exact) * 0.02, "the tree's ambient was not right");
        Assert.IsTrue(differs / (exact.Width * exact.Height) < 0.02,
            $"pixels differed from every lamp's by {differs / (exact.Width * exact.Height):F3} on average");
    }

    /// <summary>
    /// A spotlight turned away from a place, and a lamp behind an opaque surface, can give it nothing, and are never
    /// picked for it.
    /// </summary>
    [TestMethod]
    public void TestALightThatCannotReachAPlaceIsNeverPicked()
    {
        List<Light> lights = Lamps(30, 50);

        lights.Add(new Spotlight
        {
            Location = new Point(0, 2, 0), PointAt = new Point(0, 10, 0), Falloff = 0.3, Radius = 0.2,
            FadeDistance = 50, Color = new Color(100, 100, 100)
        });

        Light below = new PointLight { Location = new Point(0, -1, 0), FadeDistance = 50, Color = new Color(100, 100, 100) };

        lights.Add(below);

        LightTree tree = LightTree.Of(lights, 1, false, 64, 0.25);
        List<(Light Light, double Weight, bool Held)> picks = [];

        for (int index = 0; index < 200; index++)
        {
            picks.Clear();
            tree.Choose(Place(index), new Vector(0, 1, 0), true, 64, picks);

            Assert.IsFalse(picks.Any(pick => pick.Light is Spotlight), "a spotlight aimed away from the place was picked");
            Assert.IsFalse(picks.Any(pick => ReferenceEquals(pick.Light, below)), "a lamp under the ground was picked");
        }
    }

    /// <summary>
    /// A place picks the same lights every time it is asked: what is picked is drawn from the place itself.
    /// </summary>
    [TestMethod]
    public void TestAPlacePicksTheSameLightsEveryTime()
    {
        LightTree tree = LightTree.Of(Lamps(200), 1, false, 8, 0.25);
        List<(Light Light, double Weight, bool Held)> once = [];
        List<(Light Light, double Weight, bool Held)> again = [];
        Point at = new (3.25, 0, -7.5);

        tree.Choose(at, new Vector(0, 1, 0), true, 8, once);
        tree.Choose(at, new Vector(0, 1, 0), true, 8, again);

        CollectionAssert.AreEqual(once, again, "the same place picked other lights");
    }

    /// <summary>
    /// A scene with more than sixteen fading lamps renders by a tree of them, right on average against the same scene
    /// with `no light tree`, which looks at every lamp.  A scene with too few for a tree renders exactly as it did,
    /// pixel for pixel, whatever it says.  And what a context may not say is refused.
    /// </summary>
    [TestMethod]
    public void TestAScenesLampsAreHeldInATreeUnlessItSaysNot()
    {
        string tree = MeanOf(Rendered("context { light tree samples 8 }\n" + LampsScene(40))).ToString("R");
        double exact = MeanOf(Rendered("context { no light tree }\n" + LampsScene(40)));
        Canvas few = Rendered(LampsScene(12));
        Canvas refused = Rendered("context { no light tree }\n" + LampsScene(12));

        Assert.AreEqual(exact, double.Parse(tree), exact * 0.02, "the tree's scene was not right on average");

        for (int y = 0; y < few.Height; y++)
        {
            for (int x = 0; x < few.Width; x++)
            {
                Color one = refused.GetPixel(x, y), other = few.GetPixel(x, y);

                Assert.IsTrue(one.Red == other.Red && one.Green == other.Green && one.Blue == other.Blue,
                    "too few lamps for a tree rendered otherwise");
            }
        }

        StringAssert.Contains(Refusal("context { light tree samples 0 }"), "at least one");
        StringAssert.Contains(Refusal("context { light tree split -1 }"), "less than nothing");
    }

    private static string LampsScene(int lamps) =>
        "camera { location [0, 6, -9]  look at [0, 0, 0] }\n" +
        "plane { material { pigment [0.8, 0.8, 0.8]  ambient 0  specular 0 } }\n" +
        string.Join("\n", Enumerable.Range(0, lamps).Select(index =>
            $"point light {{ location [{(index % 8 - 3.5) * 2}, 1.5, {(index / 8 - 2) * 2}]  fade distance 1  color [0.6, 0.6, 0.6] }}"));

    private static Canvas Rendered(string scene)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"tree-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "scene.igl");

            File.WriteAllText(path, scene);
            new LanguageParser(path).Parse().Render(new RenderOptions
            {
                OutputFileName = Path.Combine(directory, "out.png"), Width = 64, Height = 48
            });

            return new RayTracer.ImageIO.ImageFile(Path.Combine(directory, "out.png")).Load()[0];
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static double MeanOf(Canvas canvas)
    {
        double sum = 0;

        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
                sum += canvas.GetPixel(x, y).Red;
        }

        return sum / (canvas.Width * canvas.Height);
    }

    private static string Refusal(string context)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"tree-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        string path = Path.Combine(directory, "scene.igl");
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        File.WriteAllText(path, context + "\ncamera { location [0, 1, -3]  look at [0, 0, 0] }\npoint light { location [0, 5, -5] }\n");
        Console.SetOut(captured);

        try
        {
            new LanguageParser(path).Parse()?.Render(new RenderOptions
            {
                OutputFileName = Path.Combine(directory, "out.png"), Width = 4, Height = 4
            });
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
        finally
        {
            Console.SetOut(was);
            Directory.Delete(directory, true);
        }

        return captured.ToString();
    }
}
