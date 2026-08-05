using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover what a scene writes to fill a piece of space with something, and what it is
/// told when it writes something that cannot work.  They render rather than merely parse, since a
/// medium that parses perfectly may still be attached to nothing.
/// </summary>
[TestClass]
public class TestMediumClauses
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"medium-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// Renders the given scene and hands back the picture, or whatever stopped it.
    /// </summary>
    private (Canvas Image, string Error) Render(string scene)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");

        File.WriteAllText(path, "context { angles are degrees  no gamma }\n" + scene);

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            if (renderer is null)
                return (null, captured.ToString());

            renderer.Render(new RenderOptions
            {
                OutputFileName = output, Width = 40, Height = 40
            });

            return captured.ToString().Contains("Error")
                ? (null, captured.ToString())
                : (new ImageFile(output).Load()[0], null);
        }
        catch (Exception exception)
        {
            return (null, exception.ToString());
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    /// <summary>
    /// The pieces every scene here needs: somewhere to look from, and a lamp near enough that its
    /// own trip through any fog is a short one.
    /// </summary>
    private const string Viewpoint = """
        camera { location [0, 0, -4]  look at [0, 0, 1]  field of view 60 }
        point light { location [-2, 3, -3]  color White }
        """;

    [TestMethod]
    public void TestTheSurroundingsMayBeFilledWithSomething()
    {
        // Two balls at the same size, one four times further off than the other.  With the space
        // between them filled, the far one must arrive dimmer than the near one purely for having
        // been further away, which is the whole of what a medium buys here.
        const string balls = """
            sphere { material { pigment White  ambient 1  diffuse 0 }  translate [-1.2, 0, 0] }
            sphere { material { pigment White  ambient 1  diffuse 0 }  scale 4  translate [4.8, 0, 24] }
            """;
        (Canvas withFog, string error) = Render("""
            environment { medium { absorption [0.12, 0.12, 0.12] } }
            {{Viewpoint}}
            {{balls}}
            """.Replace("{{Viewpoint}}", Viewpoint).Replace("{{balls}}", balls));

        Assert.IsNull(error, error);

        double near = Brightest(withFog, 0, 20);
        double far = Brightest(withFog, 20, 40);

        Assert.IsTrue(near > 0.9, $"the near ball should be barely touched, and came out at {near}");
        Assert.IsTrue(far < near * 0.5,
            $"the far ball should be plainly dimmer, and came out at {far} against {near}");
    }

    [TestMethod]
    public void TestAnEndlessMediumBecomesTheSky()
    {
        // With nothing in the way, every ray runs on forever through the medium, and what comes back
        // is the color the medium's own numbers settle at: what it gives off over what it takes out.
        (Canvas image, string error) = Render("""
            environment {
                medium {
                    absorption [0.5, 0.5, 0.5]
                    emission [0.1, 0.25, 0.4]
                }
            }
            {{Viewpoint}}
            """.Replace("{{Viewpoint}}", Viewpoint));

        Assert.IsNull(error, error);

        Color sky = image.GetPixel(20, 20);

        Assert.AreEqual(0.2, sky.Red, 0.01, $"the sky came out {sky}");
        Assert.AreEqual(0.5, sky.Green, 0.01, $"the sky came out {sky}");
        Assert.AreEqual(0.8, sky.Blue, 0.01, $"the sky came out {sky}");
    }

    [TestMethod]
    public void TestASurfaceMayBeFilledWithSomethingThatGlows()
    {
        // A medium in an interior needs the surface to let light through, exactly as the fade
        // through a substance does: a ray that cannot get inside never crosses what is in there.
        (Canvas image, string error) = Render("""
            {{Viewpoint}}
            background Black
            sphere {
                material {
                    pigment White  ambient 0  diffuse 0  specular 0  transparency 1
                    interior { medium { emission [0.8, 0.4, 0.1]  absorption [0.2, 0.3, 0.5] } }
                }
            }
            """.Replace("{{Viewpoint}}", Viewpoint));

        Assert.IsNull(error, error);

        Color middle = image.GetPixel(20, 20);

        Assert.IsTrue(middle.Red > 0.5, $"the gas should glow, and came out {middle}");
        Assert.IsTrue(middle.Red > middle.Green && middle.Green > middle.Blue,
            $"it should glow in its own colors, and came out {middle}");

        // The corners are past the ball, where there is nothing at all to see.
        Assert.IsTrue(image.GetPixel(1, 1).Red < 0.01);
    }

    [TestMethod]
    public void TestTheShorthandAndTheBlockMayBothBeWritten()
    {
        // The single line the index of refraction arrived as still stands, and says the same thing
        // the block does.
        (Canvas fromShorthand, string first) = Render("""
            environment ior Water
            {{Viewpoint}}
            sphere { material { pigment White  transparency 1  interior { ior Glass } } }
            """.Replace("{{Viewpoint}}", Viewpoint));
        (Canvas fromBlock, string second) = Render("""
            environment { ior Water }
            {{Viewpoint}}
            sphere { material { pigment White  transparency 1  interior { ior Glass } } }
            """.Replace("{{Viewpoint}}", Viewpoint));

        Assert.IsNull(first, first);
        Assert.IsNull(second, second);

        for (int x = 0; x < fromShorthand.Width; x++)
        for (int y = 0; y < fromShorthand.Height; y++)
        {
            Assert.IsTrue(fromShorthand.GetPixel(x, y).Matches(fromBlock.GetPixel(x, y)),
                $"the two spellings disagreed at {x}, {y}");
        }
    }

    [TestMethod]
    public void TestTheSurroundingsMayBeFilledInsideASceneBlock()
    {
        (Canvas image, string error) = Render("""
            scene {
                environment { medium { absorption [0.2, 0.2, 0.2]  emission [0.1, 0.1, 0.1] } }
                camera { location [0, 0, -4]  look at [0, 0, 1]  field of view 60 }
                point light { location [-2, 3, -3]  color White }
            }
            """);

        Assert.IsNull(error, error);
        Assert.AreEqual(0.5, image.GetPixel(20, 20).Red, 0.01);
    }

    [TestMethod]
    public void TestAMediumFillingTheSurroundingsMustHaveAnAnswerOverAnEndlessSpan()
    {
        // Light given off where none is absorbed piles up without limit, so a medium saying that of
        // the space outside everything is describing something infinitely bright.  Said of a surface
        // it is perfectly reasonable, and allowed.
        (Canvas image, string error) = Render("""
            environment { medium { emission [0.5, 0.5, 0.5] } }
            {{Viewpoint}}
            """.Replace("{{Viewpoint}}", Viewpoint));

        Assert.IsNull(image);
        Assert.Contains("must absorb wherever it emits", error);

        (Canvas bounded, string allowed) = Render("""
            {{Viewpoint}}
            sphere {
                material {
                    pigment White  ambient 0  diffuse 0  transparency 1
                    interior { medium { emission [0.5, 0.5, 0.5] } }
                }
            }
            """.Replace("{{Viewpoint}}", Viewpoint));

        Assert.IsNull(allowed, allowed);
        Assert.IsNotNull(bounded);
    }

    [TestMethod]
    public void TestAMediumCannotWorkBackward()
    {
        (Canvas image, string error) = Render("""
            environment { medium { absorption [-0.1, 0, 0] } }
            {{Viewpoint}}
            """.Replace("{{Viewpoint}}", Viewpoint));

        Assert.IsNull(image);
        Assert.Contains("cannot absorb light at less than no rate", error);

        (image, error) = Render("""
            environment { medium { absorption 0.1  emission [0, -0.2, 0] } }
            {{Viewpoint}}
            """.Replace("{{Viewpoint}}", Viewpoint));

        Assert.IsNull(image);
        Assert.Contains("cannot emit light at less than no rate", error);

        (image, error) = Render("""
            environment { medium { absorption 0.1  density -1 } }
            {{Viewpoint}}
            """.Replace("{{Viewpoint}}", Viewpoint));

        Assert.IsNull(image);
        Assert.Contains("density cannot be less than nothing", error);
    }

    /// <summary>
    /// Reports the brightest pixel found in a band of columns, which is how a ball is looked for
    /// without having to know exactly where it landed.
    /// </summary>
    private static double Brightest(Canvas image, int fromColumn, int toColumn)
    {
        double brightest = 0;

        for (int x = fromColumn; x < toColumn; x++)
        for (int y = 0; y < image.Height; y++)
            brightest = Math.Max(brightest, image.GetPixel(x, y).Red);

        return brightest;
    }
}
