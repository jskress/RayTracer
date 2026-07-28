using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover how a scene writes a pigment, checked by rendering, since a clause that
/// parses may still build the wrong thing.  What each pattern looks like is covered by
/// <see cref="TestPatterns"/>; these are about the words a scene is allowed to write.
/// </summary>
[TestClass]
public class TestPigmentClauses
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"pigment-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// Renders a ball wearing the given pigment, and reports the error that stopped it, if any.
    /// </summary>
    private string ErrorFrom(string pigment)
    {
        string path = Path.Combine(_directory, "scene.igl");

        File.WriteAllText(path,
            "context { no gamma }\n" +
            "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
            "point light { location [-4, 6, -8] }\n" +
            $"sphere {{ material {{ {pigment} }} }}");

        StringWriter output = new ();
        TextWriter was = Console.Out;

        Console.SetOut(output);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            if (renderer is null)
                return output.ToString();

            renderer.Render(new RenderOptions
            {
                OutputFileName = Path.Combine(_directory, "out.png"), Width = 8, Height = 8
            });

            return output.ToString().Contains("Error") ? output.ToString() : null;
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

    [TestMethod]
    public void TestAnAgateNeedsNoTurbulence()
    {
        // Turbulence is what gives agate its wandering bands, so a scene nearly always asks for
        // some -- but leaving it off used to take the render down with a null reference rather
        // than simply drawing the unstirred banding.  Marble and wood had always coped.
        Assert.IsNull(ErrorFrom("pigment agate { [0, Red, 1, Blue] }"));
        Assert.IsNull(ErrorFrom("pigment marble { [0, Red, 1, Blue] }"));
        Assert.IsNull(ErrorFrom("pigment wood { [0, Red, 1, Blue] }"));
    }

    [TestMethod]
    public void TestAnImagePigmentMayBeAskedForUncached()
    {
        // "uncached" makes it the first word of the clause, and the pigment parser dispatches on
        // that first word -- so this used to be mistaken for the name of a pattern and rejected
        // with "Unsupported uncached pattern found."
        string image = Path.Combine(_directory, "texture.png");

        // Any real image will do; render a tiny one to point at.
        File.WriteAllText(Path.Combine(_directory, "make.igl"),
            "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
            "point light { location [-4, 6, -8] }\n" +
            "sphere { }");
        new LanguageParser(Path.Combine(_directory, "make.igl")).Parse()
            .Render(new RenderOptions { OutputFileName = image, Width = 8, Height = 8 });

        Assert.IsNull(ErrorFrom($"pigment image '{image}'"));
        Assert.IsNull(ErrorFrom($"pigment uncached image '{image}'"));
        Assert.IsNull(ErrorFrom($"pigment uncached image '{image}' spherical"));
    }

    [TestMethod]
    public void TestAPlanarImageMapTilesRatherThanFallingOffTheImage()
    {
        // Left to repeat, a planar map tiles, so a surface reaching to the left of the origin --
        // which a cube at the origin does -- asks for a negative coordinate.  C#'s remainder
        // keeps the sign of its left operand, so that used to index off the front of the image
        // and take the render down.  A second bug sat beside it: "&&" binds tighter than "||",
        // so the Z guard applied whether or not "once" had been asked for.
        string image = Path.Combine(_directory, "texture.png");

        File.WriteAllText(Path.Combine(_directory, "make.igl"),
            "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
            "point light { location [-4, 6, -8] }\n" +
            "sphere { material { pigment checker { Red, Blue } } }");
        new LanguageParser(Path.Combine(_directory, "make.igl")).Parse()
            .Render(new RenderOptions { OutputFileName = image, Width = 16, Height = 16 });

        Assert.IsNull(ErrorFrom($"pigment image '{image}' planar"),
            "a tiling planar map must cope with negative coordinates");
        Assert.IsNull(ErrorFrom($"pigment image '{image}' planar once"));
    }

    [TestMethod]
    public void TestADiscretePatternTakesExactlyTheColorsItNeeds()
    {
        // Each of these wants a fixed number of plain colors rather than a color map, and says
        // so when given the wrong count.
        Assert.IsNull(ErrorFrom("pigment checker { Red, Blue }"));
        Assert.IsNull(ErrorFrom("pigment hexagon { Red, Blue, Green }"));
        Assert.IsNull(ErrorFrom("pigment square { Red, Blue, Green, Yellow }"));

        Assert.IsNotNull(ErrorFrom("pigment checker { Red, Blue, Green }"),
            "a checker takes two colors and should refuse a third");
    }

    [TestMethod]
    public void TestAContinuousPatternTakesAColorMap()
    {
        Assert.IsNull(ErrorFrom("pigment granite { [0, Red, 1, Blue] }"));
        Assert.IsNull(ErrorFrom("pigment linear gradient { [0, Red, 1, Blue] }"));

        Assert.IsNotNull(ErrorFrom("pigment granite { Red, Blue }"),
            "a granite is continuous and wants a map rather than two bare colors");
    }
}
