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
    public void TestAMediumMayTurnLightTowardTheEye()
    {
        // The lamp stands in the fog and nothing else does, so every scrap of what is seen was turned
        // toward the eye by the fog itself.  Without scattering the same scene is empty and black.
        const string fog = """
            camera { location [0, 0, -5]  look at [0, 0, 1]  field of view 60 }
            point light { location [0, 0, 0]  color White }
            background Black
            """;
        (Canvas lit, string error) = Render("""
            environment { medium { scattering 0.2 } }
            {{fog}}
            """.Replace("{{fog}}", fog));

        Assert.IsNull(error, error);
        Assert.IsTrue(lit.GetPixel(20, 20).Red > 0.2,
            $"the fog should be plainly lit, and came out {lit.GetPixel(20, 20)}");

        (Canvas unlit, string second) = Render("""
            environment { medium { absorption 0.2 } }
            {{fog}}
            """.Replace("{{fog}}", fog));

        Assert.IsNull(second, second);
        Assert.IsTrue(unlit.GetPixel(20, 20).Red < 0.001,
            "a fog that only swallows light should show nothing of a lamp inside it");
    }

    [TestMethod]
    public void TestWhichWayAMediumTurnsLightShows()
    {
        // The lamp is behind the eye, so along the whole of what the eye looks down, the light arrives
        // travelling the way the eye is looking and has to be turned right around to be seen -- the
        // headlights-in-fog case.  A medium favoring the way light came from must then show far more
        // than one favoring the way it was already going.
        //
        // Note that the lamp has to be behind rather than beyond: a lamp ahead of the eye still has
        // ray past it, and every place out there sees the lamp behind itself, so the two halves mix
        // and neither preference wins cleanly.  What each shape is worth at a given angle is pinned
        // exactly in the unit tests, where no geometry can get in the way.
        const string fog = """
            camera { location [0, 0, -5]  look at [0, 0, 1]  field of view 60 }
            point light { location [0, 0, -9]  color White }
            background Black
            """;
        double carriedOn = Lit(fog, "anisotropy 0.7");
        double sentBack = Lit(fog, "anisotropy -0.7");
        double spreadEvenly = Lit(fog, "anisotropy 0");
        double rayleigh = Lit(fog, "phase rayleigh");

        Assert.IsTrue(sentBack > spreadEvenly,
            $"a medium favoring the way light came from should show more than an even spread: " +
            $"{sentBack} against {spreadEvenly}");
        Assert.IsTrue(carriedOn < spreadEvenly,
            $"and one favoring the way it was going should show less: {carriedOn} against " +
            $"{spreadEvenly}");

        // Rayleigh's sends as much back as on and least to the sides, so looked straight back down it
        // shows more than an even spread but nothing like a medium bent on sending light back.
        Assert.IsTrue(rayleigh > spreadEvenly && rayleigh < sentBack,
            $"Rayleigh's should sit between the even spread and the backward one, and gave {rayleigh}");
    }

    /// <summary>
    /// Renders the given fog with the given scattering shape and hands back how lit the middle of it
    /// came out.
    /// </summary>
    private double Lit(string fog, string shape)
    {
        (Canvas image, string error) = Render("""
            environment { medium { scattering 0.15  {{shape}} } }
            {{fog}}
            """.Replace("{{shape}}", shape).Replace("{{fog}}", fog));

        Assert.IsNull(error, error);

        return image.GetPixel(20, 20).Red;
    }

    [TestMethod]
    public void TestHowHardToWorkIsTheContextsBusinessAndTheMediumsChoice()
    {
        // Asking in one place is a poor estimate and asking in sixty-four is a good one, so the two
        // must differ -- otherwise the setting does nothing at all.
        const string fog = """
            camera { location [0, 0, -5]  look at [0, 0, 1]  field of view 60 }
            point light { location [1, 1, 2]  color White }
            background Black
            """;
        (Canvas crude, string first) = Render("""
            context { medium samples 1 }
            environment { medium { scattering 0.3  anisotropy 0.5 } }
            {{fog}}
            """.Replace("{{fog}}", fog));
        (Canvas careful, string second) = Render("""
            context { medium samples 64 }
            environment { medium { scattering 0.3  anisotropy 0.5 } }
            {{fog}}
            """.Replace("{{fog}}", fog));

        Assert.IsNull(first, first);
        Assert.IsNull(second, second);
        Assert.IsFalse(crude.GetPixel(20, 20).Matches(careful.GetPixel(20, 20)),
            "the number of places asked about made no difference at all");

        // And a medium may name its own count, which then stands whatever the context says -- so this
        // must match the careful one exactly rather than the crude one it was told to be.
        (Canvas overridden, string third) = Render("""
            context { medium samples 1 }
            environment { medium { scattering 0.3  anisotropy 0.5  samples 64 } }
            {{fog}}
            """.Replace("{{fog}}", fog));

        Assert.IsNull(third, third);

        for (int x = 0; x < overridden.Width; x++)
        for (int y = 0; y < overridden.Height; y++)
        {
            Assert.IsTrue(overridden.GetPixel(x, y).Matches(careful.GetPixel(x, y)),
                $"the medium's own count did not stand at {x}, {y}");
        }
    }

    [TestMethod]
    public void TestASpotlightLaysAVisibleConeInAFog()
    {
        // The effect the whole effort is for.  A spotlight aimed down through a fog is seen as a cone,
        // because the fog inside it is lit and the fog outside it is not -- and nothing but the fog
        // makes it visible, there being nothing above the floor to catch the light.
        (Canvas image, string error) = Render("""
            context { medium samples 48 }
            camera { location [0, 2.5, -8]  look at [0, 2, 0]  field of view 45 }
            background Black
            environment { medium { scattering 0.06  anisotropy 0.3 } }
            spot light {
                location [0, 7, 0]
                point at [0, 0, 0]
                radius 8
                falloff 12
                color White
            }
            """);

        Assert.IsNull(error, error);

        // The middle of the frame looks along the cone; the edge looks past it.
        double insideTheCone = image.GetPixel(20, 12).Red;
        double outsideTheCone = image.GetPixel(3, 12).Red;

        Assert.IsTrue(insideTheCone > outsideTheCone * 5,
            $"the cone should stand out plainly: {insideTheCone} inside against {outsideTheCone} " +
            "outside");
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

        (image, error) = Render("""
            environment { medium { scattering [0, -0.3, 0] } }
            {{Viewpoint}}
            """.Replace("{{Viewpoint}}", Viewpoint));

        Assert.IsNull(image);
        Assert.Contains("cannot turn aside light at less than no rate", error);

        // At one exactly, every scrap would go straight on and the shape would have nothing left to
        // say about any other direction.
        (image, error) = Render("""
            environment { medium { scattering 0.1  anisotropy 1 } }
            {{Viewpoint}}
            """.Replace("{{Viewpoint}}", Viewpoint));

        Assert.IsNull(image);
        Assert.Contains("between minus one and one", error);

        (image, error) = Render("""
            environment { medium { scattering 0.1  samples 0 } }
            {{Viewpoint}}
            """.Replace("{{Viewpoint}}", Viewpoint));

        Assert.IsNull(image);
        Assert.Contains("at least one place", error);
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
