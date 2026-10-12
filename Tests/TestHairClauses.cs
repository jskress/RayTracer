using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover hair from the scene's side: that a surface may grow it, that what is said about it
/// arrives, and what it wears.
/// </summary>
[TestClass]
public class TestHairClauses
{
    private const string Blue = "material { pigment [0, 0, 1]  ambient 1  diffuse 0  specular 0 }";
    private const string Red = "material { pigment [1, 0, 0]  ambient 1  diffuse 0  specular 0 }";

    private string _directory;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"hair-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [TestMethod]
    public void TestASurfaceMayGrowHair()
    {
        Assert.IsNull(ErrorFrom("""
                                sphere {
                                    hair {
                                        density 200  length 0.3  vary 0.1  thickness 0.01
                                        lean 20  comb [1, 0, 0]  curl 2 radius 0.02  frizz 0.2
                                        clump 0.5 size 12  droop 0.4  with seed 7
                                        material { pigment [0.4, 0.3, 0.2] }
                                    }
                                }
                                """));
        Assert.IsNull(ErrorFrom("cube { hair { count 40  length 0.2 }  scale [2, 0.1, 1] }"));
    }

    [TestMethod]
    public void TestHairMustSayHowMuchAndHowLong()
    {
        StringAssert.Contains(ErrorFrom("sphere { hair { length 0.3 } }"), "density or a count");
        StringAssert.Contains(ErrorFrom("sphere { hair { count 30 } }"), "length");
        StringAssert.Contains(ErrorFrom("sphere { hair { count 30  density 4  length 0.3 } }"), "not both");
        StringAssert.Contains(ErrorFrom("sphere { hair { count 30  length 0.3  vary 1 } }"), "less than one");
        StringAssert.Contains(ErrorFrom("sphere { hair { count 30  length 0.3  clump 2 } }"), "nought to one");
        StringAssert.Contains(ErrorFrom("plane { hair { count 30  length 0.3 } }"), "plane");
        StringAssert.Contains(ErrorFrom("sphere { hair { count 30  length 0.3  lean 120 } }"), "right angle");
    }

    /// <summary>
    /// A material may color hair by its melanin: a named natural shade, an amount of eumelanin, or
    /// amounts of eumelanin and pheomelanin.
    /// </summary>
    [TestMethod]
    public void TestAMaterialMayColorHairByItsMelanin()
    {
        Assert.IsNull(ErrorFrom("sphere { hair { count 30  length 0.3  material { melanin 'auburn' } } }"));
        Assert.IsNull(ErrorFrom("sphere { hair { count 30  length 0.3  material { melanin 1.3 } } }"));
        Assert.IsNull(ErrorFrom("sphere { hair { count 30  length 0.3  material { melanin 0.5, 0.8  roughness 0.2 } } }"));
        Assert.IsNull(ErrorFrom("strand { width 0.02 at [0, 0, 0]  width 0.01 at [0, 1, 0]  material { melanin 'black' } }"));
        StringAssert.Contains(ErrorFrom("sphere { material { melanin 'purple' } }"), "no shade of hair called 'purple'");
        StringAssert.Contains(ErrorFrom("sphere { material { melanin -1 } }"), "less than nought");
        StringAssert.Contains(ErrorFrom("sphere { material { melanin 'black', 2 } }"), "written alone");
    }

    /// <summary>
    /// Melanin colors hair as it colors real hair: black hair comes out darker than blond.
    /// </summary>
    [TestMethod]
    public void TestBlackHairIsDarkerThanBlond()
    {
        double Brightness(string shade)
        {
            string path = Path.Combine(_directory, "shade.igl");
            string output = Path.Combine(_directory, "shade.png");

            File.WriteAllText(path,
                "context { no gamma }\n" +
                "camera { location [0, 0, -8]  look at [0, 0, 0]  field of view 30 }\n" +
                "point light { location [-4, 8, -8] }\n" +
                $"sphere {{ material {{ pigment [0, 0, 0] }}  hair {{ count 4000  length 0.6  thickness 0.04  material {{ melanin '{shade}' }} }} }}\n");

            new LanguageParser(path).Parse().Render(new RenderOptions { OutputFileName = output, Width = 60, Height = 60 });

            Canvas canvas = new ImageFile(output).Load()[0];
            double sum = 0;

            for (int y = 0; y < canvas.Height; y++)
            {
                for (int x = 0; x < canvas.Width; x++)
                    sum += canvas.GetPixel(x, y).Red;
            }

            return sum;
        }

        double black = Brightness("black");
        double blond = Brightness("blond");

        Assert.IsTrue(blond > 2 * black, $"blond {blond}, black {black}");
    }

    /// <summary>
    /// Hair with no material of its own wears its surface's, and hair with one wears that.
    /// </summary>
    [TestMethod]
    public void TestHairWearsItsSurfacesMaterialUnlessItHasOne()
    {
        (int bare, int bareRed) = Counted($"sphere {{ {Blue} }}");
        (int furry, int furryRed) = Counted($"sphere {{ {Blue}  hair {{ count 3000  length 1  thickness 0.04 }} }}");
        (int dressed, int dressedRed) = Counted(
            $"sphere {{ {Blue}  hair {{ count 3000  length 1  thickness 0.04  {Red} }} }}");

        Assert.AreEqual(0, bareRed + furryRed, "red showed where nothing was red");
        Assert.IsTrue(furry > bare * 1.2, $"the hair did not wear its sphere's blue: {furry} blue against {bare}");
        Assert.IsTrue(dressedRed > bare * 0.2, $"the hair did not wear its own red: {dressedRed} red");
        Assert.IsTrue(dressed < furry, "the red hair was blue as well");
    }

    /// <summary>
    /// Hair grown over a group with no material of its own, whose parts each have one, wears the material
    /// of the part each strand grows from -- not the plain white of a surface with no material.
    /// </summary>
    [TestMethod]
    public void TestHairWearsTheMaterialOfThePartItGrowsFrom()
    {
        string parts = $"sphere {{ {Red}  scale 0.7  translate [-1.1, 0, 0] }}  sphere {{ {Blue}  scale 0.7  translate [1.1, 0, 0] }}";
        (int bareBlue, int bareRed, _) = CountedWithWhite($"group {{ {parts} }}");
        (int blue, int red, int white) = CountedWithWhite(
            $"group {{ {parts}  hair {{ count 3000  length 0.8  thickness 0.04 }} }}");

        // A strand's highlight is white whatever its color, so a little white is to be expected; hair
        // wearing nothing at all would be white all over.
        Assert.IsTrue(white * 50 < red + blue, $"some hair wore no material at all: {white} white pixels");
        Assert.IsTrue(red > bareRed * 1.2, $"the red part's hair was not red: {red} against {bareRed}");
        Assert.IsTrue(blue > bareBlue * 1.2, $"the blue part's hair was not blue: {blue} against {bareBlue}");
    }

    /// <summary>
    /// A named surface with hair, used twice, grows hair in both places.
    /// </summary>
    [TestMethod]
    public void TestANamedFurrySurfaceGrowsHairWhereverItIsUsed()
    {
        (int one, _) = Counted($"Ball = sphere {{ {Blue}  hair {{ count 2000  length 0.6  thickness 0.04 }}  scale 0.5 }}\n" +
                               "object Ball { translate [-1.2, 0, 0] }");
        (int two, _) = Counted($"Ball = sphere {{ {Blue}  hair {{ count 2000  length 0.6  thickness 0.04 }}  scale 0.5 }}\n" +
                               "object Ball { translate [-1.2, 0, 0] }\nobject Ball { translate [1.2, 0, 0] }");

        Assert.IsTrue(one > 0);
        Assert.AreEqual(2 * one, two, one * 0.1, "the second ball was not as furry as the first");
    }

    /// <summary>
    /// Renders the given surfaces, lit flat, and counts the pixels that come out blue and red.
    /// </summary>
    private (int Blue, int Red) Counted(string body)
    {
        (int blue, int red, _) = CountedWithWhite(body);

        return (blue, red);
    }

    /// <summary>
    /// Renders the given surfaces, lit flat, and counts the pixels that come out blue, red and white.
    /// </summary>
    private (int Blue, int Red, int White) CountedWithWhite(string body)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");

        File.WriteAllText(path,
            "context { no gamma }\n" +
            "camera { location [0, 0, -8]  look at [0, 0, 0]  field of view 45 }\n" +
            "point light { location [-4, 8, -4] }\n" + body + "\n");

        ImageRenderer renderer = new LanguageParser(path).Parse();

        renderer.Render(new RenderOptions { OutputFileName = output, Width = 80, Height = 80 });

        Canvas canvas = new ImageFile(output).Load()[0];
        int blue = 0;
        int red = 0;
        int white = 0;

        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                Color color = canvas.GetPixel(x, y);

                if (color.Green > 0.3)
                    white++;
                else if (color.Blue > 0.5 && color.Red < 0.2)
                    blue++;
                else if (color.Red > 0.5 && color.Blue < 0.2)
                    red++;
            }
        }

        return (blue, red, white);
    }

    /// <summary>
    /// Renders a small scene and hands back the error that stopped it, or <c>null</c>.
    /// </summary>
    private string ErrorFrom(string body)
    {
        string path = Path.Combine(_directory, "bad.igl");

        File.WriteAllText(path,
            "camera { location [0, 4, -5]  look at [0, 0, 0] }\n" +
            "point light { location [-4, 6, -6] }\n" + body + "\n");

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            renderer?.Render(new RenderOptions
            {
                OutputFileName = Path.ChangeExtension(path, ".png"), Width = 4, Height = 4
            });
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
        finally
        {
            Console.SetOut(was);
        }

        // A parse error is reported rather than thrown.
        string said = captured.ToString();

        return said.Contains("Error") ? said : null;
    }
}
