using ImageMagick;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.ImageIO;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the settings a scene's own context block may fix, and -- the part that has
/// actually gone wrong -- that the command line and the scene take precedence over one another in
/// the right order.
/// </summary>
[TestClass]
public class TestContextClauses
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"context-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// Renders a trivial scene with the given context body, at whatever size the options ask for,
    /// and reports the size the image actually came out.
    /// </summary>
    private (int Width, int Height) RenderedSize(string contextBody, int? width = null, int? height = null)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");

        File.WriteAllText(path,
            $"context {{ no gamma {contextBody} }}\n" +
            "camera { location [0, 1.5, -5]  look at [0, 1, 0] }\n" +
            "point light { location [-10, 10, -10]  color White }\n" +
            "sphere { translate [0, 1, 0] }");

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            Assert.IsNotNull(renderer, $"the scene did not parse: {captured}");

            renderer.Render(new RenderOptions
            {
                OutputFileName = output, Width = width, Height = height
            });

            Canvas image = new ImageFile(output).Load()[0];

            return (image.Width, image.Height);
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    [TestMethod]
    public void TestASceneMaySettleItsOwnSize()
    {
        // The scene asks; the command line says nothing; the scene should get what it asked for.
        // This did not work at all for a long while: the width and height options carried a
        // default, so they were never absent, and the scene's own size was quietly overwritten on
        // every render.
        Assert.AreEqual((320, 240), RenderedSize("width 320  height 240"));
    }

    [TestMethod]
    public void TestTheCommandLineOverridesTheSceneSize()
    {
        Assert.AreEqual((640, 480), RenderedSize("width 320  height 240", 640, 480));
    }

    [TestMethod]
    public void TestEitherDimensionMayBeSettledOnItsOwn()
    {
        // Only one of the two given, from either side; the other falls back as it should.
        Assert.AreEqual((320, 600), RenderedSize("width 320"));
        Assert.AreEqual((800, 240), RenderedSize("height 240"));
        Assert.AreEqual((640, 240), RenderedSize("height 240", width: 640));
    }

    [TestMethod]
    public void TestSaidNowhereTheSizeIsTheUsualOne()
    {
        Assert.AreEqual((800, 600), RenderedSize(""));
    }

    [TestMethod]
    public void TestASceneMaySettleItsOwnGamma()
    {
        // Gamma always did work this way -- its option carries no default -- and it is worth a
        // test beside the size so that the two cannot drift apart again.
        RenderContext context = new ();

        context.ApplyOptions(new RenderOptions(), 0);

        Assert.AreEqual(2.2, context.Gamma, 1e-9);

        context = new RenderContext { Gamma = 1.8 };

        context.ApplyOptions(new RenderOptions(), 0);

        Assert.AreEqual(1.8, context.Gamma, 1e-9, "the scene's gamma should have survived");

        context = new RenderContext { Gamma = 1.8 };

        context.ApplyOptions(new RenderOptions { Gamma = 2.6 }, 0);

        Assert.AreEqual(2.6, context.Gamma, 1e-9, "the command line should have won");
    }
    // -- color depth and grayscale -------------------------------------------------------------------

    /// <summary>
    /// What reaches the file is the scene's to settle and the command line's to overrule, the same
    /// way antialiasing is.  Both of these used to be assigned over the top of whatever the scene
    /// had said, which meant a scene could not have kept them even had it been able to say them.
    /// </summary>
    [TestMethod]
    public void TestASceneMaySettleWhatReachesTheFile()
    {
        RenderContext context = new ();

        context.ApplyOptions(new RenderOptions(), 0);

        Assert.AreEqual(8, context.BitsPerChannel, "said nowhere, eight bits is the usual depth");
        Assert.IsFalse(context.Grayscale, "and an image has its color");

        context = new RenderContext { BitsPerChannel = 16, Grayscale = true };

        context.ApplyOptions(new RenderOptions(), 0);

        Assert.AreEqual(16, context.BitsPerChannel,
            "the scene's channel depth should have survived a silent command line");
        Assert.IsTrue(context.Grayscale, "and so should its having asked for no color");

        context = new RenderContext { BitsPerChannel = 16 };

        context.ApplyOptions(new RenderOptions { BitsPerChannel = 8 }, 0);

        Assert.AreEqual(8, context.BitsPerChannel, "the command line should have won");

        // Grayscale is a switch, so it goes one way only -- exactly as `no gamma` and `no shadows`
        // do.  Asking for it on the command line turns it on; not asking says nothing.
        context = new RenderContext();

        context.ApplyOptions(new RenderOptions { Grayscale = true }, 0);

        Assert.IsTrue(context.Grayscale, "the command line should be able to ask for no color");
    }

    /// <summary>
    /// And both reach the file the scene is written to.
    /// </summary>
    [TestMethod]
    public void TestWhatTheSceneSaysReachesTheImageFile()
    {
        string path = Path.Combine(_directory, "written.igl");
        string output = Path.Combine(_directory, "written.png");

        File.WriteAllText(path,
            "context { width 20  height 15  color depth 16  grayscale }\n" +
            "camera { location [0, 0, -5]  look at [0, 0, 0]  field of view 60 }\n" +
            "point light { location [-5, 5, -5] }\n" +
            "sphere { material { pigment [0.9, 0.2, 0.1] } }\n");

        ImageRenderer renderer = new LanguageParser(path).Parse();

        Assert.IsNotNull(renderer, "the scene did not parse");

        renderer.Render(new RenderOptions { OutputFileName = output });

        using MagickImage written = new (output);

        Assert.AreEqual(16u, written.Depth, "the scene asked for sixteen bits");

        // Which of the two gray containers it gets depends on whether anything in the picture is
        // less than opaque, which is not what this test is about; that it is gray at all is.
        Assert.IsTrue(written.ColorType is ColorType.Grayscale or ColorType.GrayscaleAlpha,
            $"the scene asked for no color and got {written.ColorType}");
    }

    // -- antialiasing --------------------------------------------------------------------------------

    /// <summary>
    /// Antialiasing is the scene's to ask for and the command line's to overrule, and the middle
    /// case is the one that matters: a command line that says nothing on the subject must leave the
    /// scene's own setting standing.  It did not, and that is how a sweep came to re-render three
    /// gallery pictures without the antialiasing they were made with.
    /// </summary>
    [TestMethod]
    public void TestASceneMaySettleItsOwnAntiAliasing()
    {
        RenderContext context = new ();

        context.ApplyOptions(new RenderOptions(), 0);

        Assert.AreEqual("off", context.AntiAliasing.ToString(),
            "said nowhere, there should be no antialiasing");

        context = new RenderContext();
        context.AntiAliasing.AdaptiveDepth = 1;

        context.ApplyOptions(new RenderOptions(), 0);

        Assert.AreEqual("adaptive:1", context.AntiAliasing.ToString(),
            "the scene's antialiasing should have survived a command line that said nothing");

        context = new RenderContext();
        context.AntiAliasing.AdaptiveDepth = 1;

        context.ApplyOptions(new RenderOptions { AntiAliasingText = "adaptive:4" }, 0);

        Assert.AreEqual("adaptive:4", context.AntiAliasing.ToString(),
            "the command line should have won");

        // And it must be able to win by turning the thing off, which is why "said nothing" cannot
        // simply be spelled "off".
        context = new RenderContext();
        context.AntiAliasing.AdaptiveDepth = 1;

        context.ApplyOptions(new RenderOptions { AntiAliasingText = "off" }, 0);

        Assert.AreEqual("off", context.AntiAliasing.ToString(),
            "the command line should be able to turn it off again");
    }

    /// <summary>
    /// And the setting reaches the picture: a scene asking for antialiasing renders differently from
    /// one that does not.  The subject is a sphere against a background, so the only thing to
    /// antialias is its edge.
    /// </summary>
    [TestMethod]
    public void TestAntiAliasingAskedForInASceneReachesThePicture()
    {
        Canvas plain = RenderedWith("");
        Canvas smoothed = RenderedWith("antialiasing depth 2");
        int different = 0;

        for (int y = 0; y < plain.Height; y++)
        {
            for (int x = 0; x < plain.Width; x++)
            {
                if (!plain.GetPixel(x, y).Red.Near(smoothed.GetPixel(x, y).Red, 1e-6))
                    different++;
            }
        }

        Assert.IsTrue(different > 20,
            $"only {different} pixels changed; the scene's antialiasing did not reach the render");
    }

    /// <summary>
    /// The threshold on its own asks for the sampler too, since the number means nothing without it.
    /// </summary>
    [TestMethod]
    public void TestTheThresholdAloneAsksForTheSampler()
    {
        RenderContext context = new ();

        context.AntiAliasing.AdaptiveThreshold = 0.5;

        Assert.AreEqual("adaptive:5:0.5", context.AntiAliasing.ToString());
    }

    /// <summary>
    /// Renders a sphere against a plain background with the given context body, and hands back the
    /// image.
    /// </summary>
    private Canvas RenderedWith(string contextBody)
    {
        string path = Path.Combine(_directory, $"aa-{contextBody.Length}.igl");
        string output = Path.Combine(_directory, $"aa-{contextBody.Length}.png");

        File.WriteAllText(path,
            $"context {{ no gamma  width 80  height 60  {contextBody} }}\n" +
            "camera { location [0, 0, -5]  look at [0, 0, 0]  field of view 60 }\n" +
            "point light { location [-5, 5, -5] }\n" +
            "background [0, 0, 0]\n" +
            "sphere { material { pigment White } }\n");

        ImageRenderer renderer = new LanguageParser(path).Parse();

        Assert.IsNotNull(renderer, "the scene did not parse");

        renderer.Render(new RenderOptions { OutputFileName = output });

        return new ImageFile(output).Load()[0];
    }

    // -- scale ambient by ----------------------------------------------------------------------------

    /// <summary>
    /// Renders a scene of `count` spheres that all share one material, and reports how bright the side
    /// of them facing away from the only light came out -- which is ambient and nothing else.
    /// </summary>
    private double ShadedSide(string contextBody, int count, bool orphan = false)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");
        // An `orphan` sphere names no material at all, which is what puts it on the shared fallback
        // material rather than on one of its own.
        string wearing = orphan ? "" : "material Shared  ";
        string spheres = string.Join("\n", Enumerable.Range(0, count)
            // Centred on the origin whatever the count, so the *middle* sphere is in the same place in
            // both scenes and the same patch of picture is measuring the same thing.
            .Select(index => $"sphere {{ {wearing}translate [{index * 2.4 - (count - 1) * 1.2}, 1, 0] }}"));

        File.WriteAllText(path,
            $"context {{ no gamma {contextBody} }}\n" +
            "camera { location [0, 1.4, -6]  look at [0, 1, 0]  field of view 44 }\n" +
            "background [0, 0, 0]\n" +
            // One light, well off to the left, so the right of each sphere is in its own shadow.
            "point light { location [-14, 3, -3]  color [0.5, 0.5, 0.5] }\n" +
            "Shared = material { pigment [0.8, 0.8, 0.8]  ambient 0.30 }\n" +
            spheres);

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            Assert.IsNotNull(renderer, $"the scene did not parse: {captured}");

            renderer.Render(new RenderOptions { OutputFileName = output, Width = 160, Height = 120 });

            Canvas image = new ImageFile(output).Load()[0];
            double total = 0;
            int counted = 0;

            // The right-hand half of the middle sphere, which the light never reaches.
            for (int x = image.Width / 2; x < image.Width / 2 + 18; x++)
            for (int y = 50; y < 78; y++)
            {
                total += image.GetPixel(x, y).Red;
                counted++;
            }

            return total / counted;
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    [TestMethod]
    public void TestAmbientMayBeScaledForTheWholeScene()
    {
        // Ambient stands in for light this renderer does not trace, and a scene lit by objects in it --
        // a street at night, a room with one lamp -- wants far less of that stand-in than the materials
        // assume.  A scene-wide *value* could not do it: it would only settle materials that said
        // nothing, and a curated library names its own ambient almost everywhere.  So this multiplies.
        double full = ShadedSide("", 1);
        double half = ShadedSide("scale ambient by 0.5", 1);
        double none = ShadedSide("scale ambient by 0", 1);

        Assert.IsTrue(full > none + 0.05,
            $"the shaded side measured {full:F4} unscaled and {none:F4} at zero; scaling is doing nothing");

        // Linear, and checked as such: halving the multiplier should halve what ambient contributes,
        // which is what is left after taking away the part the light itself is responsible for.
        double fullPart = full - none;
        double halfPart = half - none;

        Assert.AreEqual(fullPart * 0.5, halfPart, fullPart * 0.12,
            $"ambient contributed {fullPart:F4} unscaled and {halfPart:F4} at a half; the second should " +
            "be half the first");
    }

    [TestMethod]
    public void TestScalingAmbientDoesNotCompoundAcrossRenders()
    {
        // A surface that names no material at all falls back on one the renderer keeps as a *static*,
        // so every orphan in every scene rendered by this process is wearing the very same object.
        // Settling its ambient with `??=` is safe on a shared thing -- it happens once and stays -- but
        // *multiplying* it is not, and a second render would scale what the first already scaled.
        //
        // Nothing catches this from a command line, where one process renders one picture.  It bites a
        // test suite, and it would bite any batch or animation.
        double first = ShadedSide("scale ambient by 0.5", 1, orphan: true);
        double second = ShadedSide("scale ambient by 0.5", 1, orphan: true);

        Assert.AreEqual(first, second, 1e-9,
            $"the same scene rendered twice measured {first:F4} then {second:F4}; the scaling is " +
            "compounding on the material shared between renders");
    }

    [TestMethod]
    public void TestAmbientCannotBeScaledByLessThanNothing()
    {
        string path = Path.Combine(_directory, "scene.igl");

        File.WriteAllText(path,
            "context { scale ambient by -1 }\n" +
            "camera { location [0, 0, -4]  look at [0, 0, 1] }\n" +
            "point light { location [0, 3, -3] }\n" +
            "sphere { }");

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            // The term is resolved when the instruction runs, not when it parses, so the complaint
            // arrives at render time rather than at parse time.
            renderer?.Render(new RenderOptions
            {
                OutputFileName = Path.Combine(_directory, "out.png"), Width = 40, Height = 40
            });
        }
        catch (Exception exception)
        {
            Console.Write(exception);
        }
        finally
        {
            Console.SetOut(was);
        }

        Assert.Contains("Ambient cannot be scaled by less than nothing", captured.ToString());
    }


}
