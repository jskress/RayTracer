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
        Assert.Contains("must absorb or scatter wherever it emits", error);

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
    public void TestAMediumMayBeGivenAShape()
    {
        // A ball whose density is thickest at its heart and reaches nothing at its rim, against the
        // same ball filled evenly.  The two cannot look alike: one has an edge and the other does not.
        const string ball = """
            camera { location [0, 0, -5]  look at [0, 0, 0]  field of view 40 }
            point light { location [-4, 3, -4]  color [0.55, 0.55, 0.55] }
            background Black
            """;
        (Canvas shaped, string first) = Render("""
            {{ball}}
            sphere {
                material {
                    pigment White  ambient 0  diffuse 0  specular 0  transparency 1
                    interior {
                        medium {
                            scattering 1.6
                            density function { max(0, 1 - √(x² + y² + z²)) }
                        }
                    }
                }
                scale 1.4
            }
            """.Replace("{{ball}}", ball));
        (Canvas even, string second) = Render("""
            {{ball}}
            sphere {
                material {
                    pigment White  ambient 0  diffuse 0  specular 0  transparency 1
                    interior { medium { scattering 1.6 } }
                }
                scale 1.4
            }
            """.Replace("{{ball}}", ball));

        Assert.IsNull(first, first);
        Assert.IsNull(second, second);

        Assert.IsTrue(shaped.GetPixel(20, 20).Red > 0.05,
            $"the shaped medium should be lit, and came out {shaped.GetPixel(20, 20)}");

        // Near the rim the shaped one has run out of stuff altogether while the even one has not, and
        // that is what gives a shaped medium an edge of its own rather than its container's.
        Assert.IsTrue(shaped.GetPixel(6, 20).Red < even.GetPixel(6, 20).Red * 0.5,
            $"the rim should be far thinner: {shaped.GetPixel(6, 20).Red} against " +
            $"{even.GetPixel(6, 20).Red}");

        // Note which way round the middle comes out: the shaped ball is *brighter* there, though it
        // holds less stuff.  The even ball is thicker throughout, so more of what it scatters is
        // swallowed again on the way out and more of it stands in its own light.  Thicker is not
        // brighter, which is worth knowing before tuning a cloud by eye.
        Assert.IsTrue(shaped.GetPixel(20, 20).Red > even.GetPixel(20, 20).Red,
            $"{shaped.GetPixel(20, 20).Red} against {even.GetPixel(20, 20).Red}");
    }

    [TestMethod]
    public void TestAShapeMayBeNamedAsAPatternInstead()
    {
        // The other way to say a shape: not written out as a function, but named from the pattern
        // library.  A checker is the one to hold it to, since it fills alternating blocks and leaves
        // the rest empty, so a ball filled with it cannot come out as the same ball filled evenly.
        //
        // What this checks is the difference from the even ball rather than how blotchy the picture
        // looks, and that is worth knowing before writing another of these: a ray crosses many blocks
        // and adds up what it finds all the way along, so the blocks average out and a checkered
        // medium is only about a quarter blotchier than an even one.  The difference between the two
        // pictures is far plainer than the texture of either.
        const string ball = """
            context { medium samples 40 }
            camera { location [0, 0, -5]  look at [0, 0, 0]  field of view 40 }
            point light { location [-4, 3, -4]  color [0.9, 0.9, 0.9] }
            background Black
            """;
        string scene = """
            {{ball}}
            sphere {
                material {
                    pigment White  ambient 0  diffuse 0  specular 0  transparency 1
                    interior { medium { scattering 1.6 {{density}} } }
                }
                scale 1.4
            }
            """.Replace("{{ball}}", ball);
        (Canvas blocks, string first) = Render(
            scene.Replace("{{density}}", "density checker { scale 1.2 }"));
        (Canvas even, string second) = Render(scene.Replace("{{density}}", ""));

        Assert.IsNull(first, first);
        Assert.IsNull(second, second);

        double most = 0;

        for (int x = 8; x < 32; x++)
        {
            for (int y = 6; y < 34; y++)
            {
                most = Math.Max(most,
                    Math.Abs(blocks.GetPixel(x, y).Red - even.GetPixel(x, y).Red));
            }
        }

        Assert.IsTrue(most > 0.1,
            $"a checkered medium should be plainly unlike an even one, and differed by only {most}");
    }

    [TestMethod]
    public void TestThePatternIsPlacedByItsTransform()
    {
        // Without a footing of its own a pattern would be stuck at the scale of the space it sits in,
        // and most of the library would give one block over a ball this size.  The same pattern at two
        // scales has to give two different pictures, or the transform is being dropped.
        const string ball = """
            context { medium samples 40 }
            camera { location [0, 0, -5]  look at [0, 0, 0]  field of view 40 }
            point light { location [-4, 3, -4]  color [0.9, 0.9, 0.9] }
            background Black
            """;
        string scene = """
            {{ball}}
            sphere {
                material {
                    pigment White  ambient 0  diffuse 0  specular 0  transparency 1
                    interior { medium { scattering 1.6  density checker { scale {{scale}} } } }
                }
                scale 1.4
            }
            """;
        (Canvas fine, string first) = Render(
            scene.Replace("{{ball}}", ball).Replace("{{scale}}", "0.3"));
        (Canvas coarse, string second) = Render(
            scene.Replace("{{ball}}", ball).Replace("{{scale}}", "1.2"));

        Assert.IsNull(first, first);
        Assert.IsNull(second, second);

        bool differs = false;

        for (int x = 4; x < 36 && !differs; x++)
        {
            for (int y = 4; y < 36 && !differs; y++)
                differs = Math.Abs(fine.GetPixel(x, y).Red - coarse.GetPixel(x, y).Red) > 0.02;
        }

        Assert.IsTrue(differs, "the two scales gave the same picture, so the transform was dropped");
    }

    [TestMethod]
    public void TestAShapeStandsInItsOwnLight()
    {
        // What tells a cloud from a glowing blob.  The lamp is off to the left, so the left of the ball
        // is lit through less of its own stuff than the right is, and must come out brighter -- and
        // nothing but the medium shadowing itself gives that, the container being perfectly clear.
        (Canvas image, string error) = Render("""
            context { medium samples 40 }
            camera { location [0, 0, -5]  look at [0, 0, 0]  field of view 40 }
            point light { location [-9, 0, -1]  color [2.5, 2.5, 2.5] }
            background Black
            sphere {
                material {
                    pigment White  ambient 0  diffuse 0  specular 0  transparency 1
                    interior {
                        medium {
                            scattering 3.5
                            density function { max(0, 1 - √(x² + y² + z²)) }
                        }
                    }
                }
                scale 1.4
            }
            """);

        Assert.IsNull(error, error);

        double towardTheLamp = image.GetPixel(11, 20).Red;
        double awayFromIt = image.GetPixel(29, 20).Red;

        Assert.IsTrue(towardTheLamp > awayFromIt * 1.5,
            $"the lit side should be plainly brighter: {towardTheLamp} against {awayFromIt}");
    }

    [TestMethod]
    public void TestAShapeMustHaveSomewhereToEnd()
    {
        // A crossing with no end can only be walked at all because there is a distance past which
        // nothing could still show, and that rests on there being a floor under how much stuff is
        // there.  A shape free to thin away removes the floor, so it is asked for a surface instead.
        (Canvas image, string error) = Render("""
            environment { medium { scattering 0.2  density function { 1 - y } } }
            {{Viewpoint}}
            """.Replace("{{Viewpoint}}", Viewpoint));

        Assert.IsNull(image);
        Assert.Contains("must fill a surface", error);

        // The very same medium is welcome inside one.
        (Canvas bounded, string allowed) = Render("""
            {{Viewpoint}}
            sphere {
                material {
                    pigment White  ambient 0  diffuse 0  transparency 1
                    interior { medium { scattering 0.2  density function { 1 - y } } }
                }
            }
            """.Replace("{{Viewpoint}}", Viewpoint));

        Assert.IsNull(allowed, allowed);
        Assert.IsNotNull(bounded);
    }

    [TestMethod]
    public void TestAShapeMayUseMoreThanAnIsosurfaceMay()
    {
        // The density is written in the language an isosurface's function is written in, but it is held
        // to less: an isosurface has to be differentiated to be given a normal, and refuses anything
        // whose slope it cannot write down.  A density is only ever asked for a value, so smoothstep --
        // which an isosurface turns away for exactly that reason -- is welcome here.
        (Canvas image, string error) = Render("""
            {{Viewpoint}}
            sphere {
                material {
                    pigment White  ambient 0  diffuse 0  transparency 1
                    interior { medium { scattering 0.2  density function { smoothstep(0, 1, x) } } }
                }
            }
            """.Replace("{{Viewpoint}}", Viewpoint));

        Assert.IsNull(error, error);
        Assert.IsNotNull(image);

        // A name the catalog does not know at all is still refused rather than quietly ignored.
        (image, error) = Render("""
            {{Viewpoint}}
            sphere {
                material {
                    pigment White  ambient 0  diffuse 0  transparency 1
                    interior { medium { scattering 0.2  density function { wibble(x) } } }
                }
            }
            """.Replace("{{Viewpoint}}", Viewpoint));

        Assert.IsNull(image);
        Assert.Contains("no function named", error);
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

    /// <summary>
    /// A glowing volume seen against nothing, tall enough that top and bottom can be told apart.
    /// </summary>
    private const string Glowing = """
        camera { location [0, 0, -5]  look at [0, 0, 0]  field of view 40 }
        background Black
        """;

    [TestMethod]
    public void TestAMediumMayGiveOffADifferentColorFromPlaceToPlace()
    {
        // What one flat color cannot say.  A flame is white at its heart and red at its tip, and that
        // gradient is most of what makes fire read as fire -- so emission takes a pigment, which is
        // already the thing in this renderer that answers what color a place is.
        (Canvas image, string error) = Render($$"""
            Lower = color [1.1, 0.05, 0.03]
            Upper = color [0.03, 0.05, 1.1]
            Split = pigment linear gradient { [0, Lower, 1, Upper]  rotate Z 90  scale 2  translate Y -1 }
            {{Glowing}}
            sphere {
                material {
                    pigment White  ambient 0  diffuse 0  specular 0  transparency 1
                    interior { ior 1.0  medium { emission pigment Split  absorption [0.4, 0.4, 0.4] } }
                }
                no shadow
            }
            """);

        Assert.IsNull(error, error);

        Color top = image.GetPixel(20, 12);
        Color bottom = image.GetPixel(20, 28);

        // Compared against each other rather than against absolute values.  A ray crosses a range
        // of heights and gathers what it finds all along, so neither end is ever the pure color
        // the gradient names there -- what the pigment promises is that one end leans one way and
        // the other leans the other, and that is what is asked.
        Assert.IsTrue(bottom.Red > top.Red,
            $"the low end should be the redder, and got {bottom} against {top}");
        Assert.IsTrue(top.Blue > bottom.Blue,
            $"the high end should be the bluer, and got {top} against {bottom}");
    }

    [TestMethod]
    public void TestAMediumGivingOffOneColorStillDoes()
    {
        // The form every existing scene uses.  A pigment is the addition, not the replacement.
        (Canvas image, string error) = Render($$"""
            {{Glowing}}
            sphere {
                material {
                    pigment White  ambient 0  diffuse 0  specular 0  transparency 1
                    interior { ior 1.0  medium { emission [4, 0.2, 0.1]  absorption [0.4, 0.4, 0.4] } }
                }
                no shadow
            }
            """);

        Assert.IsNull(error, error);

        Color top = image.GetPixel(20, 12);
        Color bottom = image.GetPixel(20, 28);

        Assert.IsTrue(top.Red > top.Blue * 2 && bottom.Red > bottom.Blue * 2,
            "a medium given one color should give off that color everywhere");
    }

    [TestMethod]
    public void TestWhatTheSurroundingsRefuseTheyNameCorrectly()
    {
        // Both of these must fill a surface, and for the same underlying reason -- each has to be
        // walked along rather than written down, and an endless crossing has no honest place to
        // stop.  But they are different mistakes and get different complaints, because a scene
        // told its *density* varies when what varies is the light it gives off has been told
        // something untrue and will go looking in the wrong place.  Refusing correctly is not the
        // same as refusing.
        (Canvas shaped, string aboutDensity) = Render("""
            camera { location [0, 0, -5]  look at [0, 0, 0] }
            background Black
            environment { medium { absorption [0.4, 0.4, 0.4]  density function { 1 - y } } }
            sphere { material { pigment Blue } }
            """);
        (Canvas glowing, string aboutEmission) = Render("""
            Glow = pigment linear gradient { [0, White, 1, Red] }
            camera { location [0, 0, -5]  look at [0, 0, 0] }
            background Black
            environment { medium { emission pigment Glow  absorption [0.4, 0.4, 0.4] } }
            sphere { material { pigment Blue } }
            """);

        Assert.IsNull(shaped);
        Assert.IsNull(glowing);
        StringAssert.Contains(aboutDensity, "whose density varies");
        StringAssert.Contains(aboutEmission, "whose emission varies");
    }

    [TestMethod]
    public void TestAnEndlessMediumThatGlowsIsStillRefusedWhenItsColorVaries()
    {
        // A medium that gives off light with nothing to take it back out is infinitely bright over an
        // endless span, and the surroundings are endless.  That was already refused for a flat color;
        // a pigment cannot be asked whether it is ever anything but black, so one is taken as emitting
        // and refused the same way.  Letting it through would render a picture of infinity.
        (Canvas image, string error) = Render(""
            + "Glow = pigment linear gradient { [0, White, 1, Red] }\n"
            + "camera { location [0, 0, -5]  look at [0, 0, 0] }\n"
            + "background Black\n"
            + "environment { medium { emission pigment Glow } }\n"
            + "sphere { material { pigment Blue } }\n");

        Assert.IsNull(image);
        StringAssert.Contains(error, "must absorb or scatter wherever it emits");
    }

}
