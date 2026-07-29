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
}
