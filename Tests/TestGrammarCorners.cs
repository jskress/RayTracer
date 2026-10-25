using Lex.Parser;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover corners of the grammar where a word could be read two ways and the parser once insisted on the
/// wrong one, or where a well-formed file stopped the parse: `no shadow` on a sweep, a `cubic` pattern starting a
/// list of pigments, a named material blend, one name given another's thing, a file that ends with an import, and a
/// file that includes itself.
/// </summary>
[TestClass]
public class TestGrammarCorners
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"grammar-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    private const string Stage = """
        camera { location [0, 1, -6]  look at [0, 1, 0] }
        point light { location [-6, 8, -8] }
        """;

    /// <summary>
    /// Renders a scene written in the given file text, and hands back the picture or what stopped it.
    /// </summary>
    private (Canvas Picture, string Error) Rendered(string text, string name = "scene.igl")
    {
        string path = Path.Combine(_directory, name);
        string output = Path.ChangeExtension(path, "png");
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        File.WriteAllText(path, text);
        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            if (renderer is null)
                return (null, captured.ToString());

            renderer.Render(new RenderOptions { OutputFileName = output, Width = 24, Height = 18 });

            return captured.ToString().Contains("Error")
                ? (null, captured.ToString())
                : (new ImageFile(output).Load()[0], null);
        }
        catch (Exception exception)
        {
            return (null, exception.Message);
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    private void Renders(string text, string what)
    {
        (Canvas picture, string error) = Rendered(text);

        Assert.IsNull(error, $"{what}: {error}");
        Assert.IsNotNull(picture, what);
    }

    [TestMethod]
    public void TestASweepMayCastNoShadow()
    {
        Renders(Stage + """
            sweep {
                profile { move to -0.2, -0.2  line to 0.2, -0.2  line to 0.2, 0.2  line to -0.2, 0.2  close }
                spline { move to -1, 1, 0  line to 1, 1, 0 }
                no shadow
                no caustics
            }
            """, "a sweep that casts no shadow");
    }

    [TestMethod]
    public void TestASweepsProfileIsOneOutline()
    {
        (_, string error) = Rendered(Stage + """
            sweep {
                profile {
                    move to -0.2, -0.2  line to 0.2, -0.2  line to 0.2, 0.2  line to -0.2, 0.2  close
                    move to -0.1, -0.1  line to 0.1, -0.1  line to 0.1, 0.1  line to -0.1, 0.1  close
                }
                spline { move to -1, 1, 0  line to 1, 1, 0 }
            }
            """);

        StringAssert.Contains(error, "a single outline");
    }

    [TestMethod]
    public void TestSwellsMayBeNoSteeperThanOneAllTogether()
    {
        (_, string error) = Rendered(Stage + """
            swells {
                width 40  depth 40
                wave { steepness 0.6  wavelength 20  direction [0.92, 0, 0.39] }
                wave { steepness 0.5  wavelength 13  direction [-0.42, 0, 0.91] }
            }
            """);

        StringAssert.Contains(error, "must add to less than one");
    }

    [TestMethod]
    public void TestACubicPatternMayStartAListOfPigments()
    {
        Renders(Stage + """
            sphere { translate Y 1  material { pigment checker { cubic { Red, Green, Blue, Yellow, White, Black }, White } } }
            """, "a cubic pattern first in a checker");
    }

    /// <summary>
    /// `metallic` with no amount may be followed straight on by any of a material's entries, cloth's sheen and a
    /// fiber's melanin among them, as it may by a coat.
    /// </summary>
    [TestMethod]
    public void TestMetallicMayBeFollowedByAnyEntry()
    {
        Renders(Stage + "sphere { translate Y 1  material { pigment Gold  metallic sheen { color White } } }",
            "metallic then a sheen");
        Renders(Stage + "sphere { translate Y 1  material { pigment Gold  metallic melanin 0.4 } }",
            "metallic then a melanin");
    }

    [TestMethod]
    public void TestAMaterialBlendMayBeNamed()
    {
        Renders(Stage + """
            Paint = material { pigment White }
            Grime = material { pigment Black }
            Weathered = material blend crevices { radius 0.2  [0, Paint, 1, Grime] }
            sphere { translate Y 1  material Weathered }
            """, "a named material blend");
    }

    [TestMethod]
    public void TestANameMayBeGivenAnothersThing()
    {
        Renders(Stage + """
            Ball = sphere { translate Y 1 }
            Again = object Ball
            object Again
            """, "a name given another name's thing");
    }

    /// <summary>
    /// A name holding two things -- `Coral` is a color and an index of refraction -- is refused where nothing says
    /// which is meant, with a word on how to say it, and taken where something does.
    /// </summary>
    [TestMethod]
    public void TestANameOfTwoThingsMustSayWhichIsMeant()
    {
        (_, string error) = Rendered(Stage + "Shade = Coral\nsphere { translate Y 1  material { pigment Shade } }\n");

        StringAssert.Contains(error, "'Coral' names more than one thing here -- a color and a number");

        Renders(Stage + "Shade = color Coral\nsphere { translate Y 1  material { pigment Shade } }\n", "a cast says which");
        Renders(Stage + "sphere { translate Y 1  material { pigment Coral } }\n", "a pigment asks for a color");
    }

    /// <summary>
    /// A seed written before a pigment is given to it when the pigment is made there -- by a primitive's call -- and
    /// refused before a named pigment, which everything naming it shares, and before an image, which has no noise.
    /// </summary>
    [TestMethod]
    public void TestASeedBeforeAPigmentReachesItOrIsRefused()
    {
        // Speckled coarsely, so the grain is not averaged away at this size.
        const string speckle = """
            primitive Speckle(size) -> pigment { return granite { [0, Black, 1, White]  scale size } }
            """;
        (Canvas three, string first) = Rendered(Stage + speckle +
            "sphere { translate Y 1  material { pigment with seed 3 Speckle(3)  ambient 1  diffuse 0 } }\n");
        (Canvas four, string second) = Rendered(Stage + speckle +
            "sphere { translate Y 1  material { pigment with seed 4 Speckle(3)  ambient 1  diffuse 0 } }\n");

        Assert.IsNull(first, first);
        Assert.IsNull(second, second);

        bool differ = false;

        for (int x = 0; x < three.Width && !differ; x++)
        {
            for (int y = 0; y < three.Height && !differ; y++)
                differ = !three.GetPixel(x, y).Matches(four.GetPixel(x, y));
        }

        Assert.IsTrue(differ, "two seeds should speckle the ball differently");

        (_, string named) = Rendered(Stage + """
            Stone = pigment granite { [0, Black, 1, White] }
            sphere { translate Y 1  material { pigment with seed 3 Stone } }
            """);

        StringAssert.Contains(named, "A named pigment is one pigment");

        (_, string image) = Rendered(Stage + "sphere { material { pigment with seed 3 image 'nothing.png' } }\n");

        StringAssert.Contains(image, "an image has none");
    }

    /// <summary>
    /// What a built-in function refuses is reported where it was called, with the file, line and column.
    /// </summary>
    [TestMethod]
    public void TestABuiltInsRefusalSaysWhereItWasCalled()
    {
        string path = Path.Combine(_directory, "kelvin.igl");

        File.WriteAllText(path, Stage + "sphere { translate Y 1  material { pigment kelvin(0) } }\n");

        TokenException refused = Assert.ThrowsExactly<TokenException>(() => new LanguageParser(path).Parse()
            .Render(new RenderOptions { OutputFileName = Path.ChangeExtension(path, "png"), Width = 8, Height = 6 }));

        StringAssert.Contains(refused.Message, "nothing glows at absolute zero");
        Assert.IsNotNull(refused.Token, "the refusal should carry where the function was called");
        Assert.AreEqual(3, refused.Token.Line + 1, "the third line, counted from one as it is shown");
    }

    [TestMethod]
    public void TestAFileMayEndWithAnImport()
    {
        File.WriteAllText(Path.Combine(_directory, "colors.igl"), "Rust = color [0.6, 0.3, 0.1]\n");

        Renders(Stage + """
            sphere { translate Y 1 }
            import 'colors' { Rust }
            """, "a file whose last line is an import");
    }

    [TestMethod]
    public void TestAFileThatIncludesItselfIsRefused()
    {
        File.WriteAllText(Path.Combine(_directory, "other.igl"), "include 'scene.igl'\n");

        (_, string error) = Rendered(Stage + "include 'other.igl'\nsphere { }\n");

        StringAssert.Contains(error, "already being read");
    }
}
