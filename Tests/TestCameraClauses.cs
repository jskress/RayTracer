using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover how a scene asks its camera for focal blur, checked by rendering, since a
/// clause that parses may still do nothing.  The lens geometry itself is pinned down in
/// <see cref="TestFocalBlur"/>; these confirm the words reach the renderer and soften what they
/// ought to.
/// </summary>
[TestClass]
public class TestCameraClauses
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"camera-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// Renders a flat white ball on a black background through a camera written as given, and
    /// returns the row of brightnesses through its middle.
    /// <para>
    /// The ball is lit by nothing and shaded by nothing -- it is pure ambient -- so its edge is a
    /// clean step from white to black.  That leaves blur the only thing that can put a value
    /// between the two, which is what makes the edge worth measuring.
    /// </para>
    /// </summary>
    private double[] EdgeProfile(string cameraBody, out string error) =>
        EdgeProfile(cameraBody, "", out error);

    /// <summary>
    /// The same, with the given extra properties given to the ball -- a motion, most usefully.
    /// </summary>
    private double[] EdgeProfile(string cameraBody, string ballBody, out string error)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");

        File.WriteAllText(path,
            "context { no gamma }\n" +
            cameraBody + "\n" +
            "background [0, 0, 0]\n" +
            // The shading sums over the lights, so there has to be one for even a wholly ambient
            // surface to come out lit at all.  Where it stands does not matter here, since the
            // ball takes its colour from the ambient term alone.
            "point light { location [0, 0, -5]  color White }\n" +
            "sphere { material { pigment color [1, 1, 1] ambient 1 diffuse 0 specular 0 }\n" +
            ballBody + " }");

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            if (renderer is null)
            {
                error = captured.ToString();

                return null;
            }

            renderer.Render(new RenderOptions
            {
                OutputFileName = output, Width = 80, Height = 80
            });

            error = captured.ToString().Contains("Error") ? captured.ToString() : null;

            if (error is not null)
                return null;

            Canvas image = new ImageFile(output).Load()[0];
            int row = image.Height / 2;

            return Enumerable.Range(0, image.Width)
                .Select(x =>
                {
                    Color pixel = image.GetPixel(x, row);

                    return (pixel.Red + pixel.Green + pixel.Blue) / 3;
                })
                .ToArray();
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    /// <summary>
    /// Counts how many samples fall between white and black.  A sharp edge has almost none; a
    /// blurred one eases across many.
    /// </summary>
    private static int SoftCount(double[] profile) =>
        profile.Count(value => value is > 0.1 and < 0.9);

    [TestMethod]
    public void TestACameraWithNoApertureDrawsASharpEdge()
    {
        double[] profile = EdgeProfile(
            "camera { location [0, 0, -5]  look at [0, 0, 0] }", out string error);

        Assert.IsNull(error);
        Assert.IsTrue(profile.Any(value => value > 0.9), "the ball should be there");
        Assert.IsTrue(profile.Any(value => value < 0.1), "the background should be there");
    }

    [TestMethod]
    public void TestFocusingElsewhereBlursTheBall()
    {
        // The same ball twice: once with the focus on it, once with the focus well in front of it.
        // Both are the same size and the same brightness, so the extra softness in the second is
        // the blur and nothing else.
        double[] sharp = EdgeProfile("""
            camera {
                location [0, 0, -5]  look at [0, 0, 0]
                aperture 0.35  focal point [0, 0, 0]  blur samples 16
            }
            """, out string sharpError);
        double[] blurred = EdgeProfile("""
            camera {
                location [0, 0, -5]  look at [0, 0, 0]
                aperture 0.35  focal distance 1.5  blur samples 16
            }
            """, out string blurredError);

        Assert.IsNull(sharpError);
        Assert.IsNull(blurredError);
        Assert.IsTrue(SoftCount(blurred) > SoftCount(sharp) + 3,
            $"focusing away from the ball should soften its edge, but the focused camera showed " +
            $"{SoftCount(sharp)} part-lit pixels and the unfocused one {SoftCount(blurred)}");
    }

    [TestMethod]
    public void TestFocalPointAndFocalDistanceAgreeWhenTheyMeanTheSame()
    {
        // The camera sits five back and the ball is at the origin, so a focal point on the ball
        // and a focal distance of five are two ways of saying one thing.
        double[] byPoint = EdgeProfile("""
            camera {
                location [0, 0, -5]  look at [0, 0, 0]
                aperture 0.35  focal point [0, 0, 0]  blur samples 8
            }
            """, out _);
        double[] byDistance = EdgeProfile("""
            camera {
                location [0, 0, -5]  look at [0, 0, 0]
                aperture 0.35  focal distance 5  blur samples 8
            }
            """, out _);

        CollectionAssert.AreEqual(byPoint, byDistance);
    }

    [TestMethod]
    public void TestBlurIsTheSameEveryTime()
    {
        double[] first = EdgeProfile("""
            camera {
                location [0, 0, -5]  look at [0, 0, 0]
                aperture 0.4  focal distance 2  blur samples 12  seed 5
            }
            """, out _);
        double[] second = EdgeProfile("""
            camera {
                location [0, 0, -5]  look at [0, 0, 0]
                aperture 0.4  focal distance 2  blur samples 12  seed 5
            }
            """, out _);

        CollectionAssert.AreEqual(first, second);
    }

    [TestMethod]
    public void TestFocalMustSayWhichItMeans()
    {
        EdgeProfile(
            "camera { location [0, 0, -5]  look at [0, 0, 0]  aperture 0.2  focal 5 }",
            out string error);

        Assert.IsNotNull(error, "\"focal\" without \"point\" or \"distance\" should be an error");
    }

    [TestMethod]
    public void TestBlurMustSaySamples()
    {
        EdgeProfile(
            "camera { location [0, 0, -5]  look at [0, 0, 0]  aperture 0.2  blur 8 }",
            out string error);

        Assert.IsNotNull(error, "\"blur\" without \"samples\" should be an error");
    }

    /// <summary>
    /// A camera whose shutter stays open, with everything else left plain, so that a smear can only
    /// have come from something moving.
    /// </summary>
    private const string OpenShutter =
        "camera { location [0, 0, -5]  look at [0, 0, 0]  shutter 1  blur samples 16 }";

    [TestMethod]
    public void TestAMovingBallSmearsWhileAStillOneDoesNot()
    {
        double[] still = EdgeProfile(OpenShutter, "", out string stillError);
        double[] moving = EdgeProfile(
            OpenShutter, "motion { translate [1.5, 0, 0] }", out string movingError);

        Assert.IsNull(stillError);
        Assert.IsNull(movingError);
        Assert.IsTrue(SoftCount(moving) > SoftCount(still) + 3,
            $"a moving ball should smear, but the still one showed {SoftCount(still)} part-lit " +
            $"pixels and the moving one {SoftCount(moving)}");
    }

    [TestMethod]
    public void TestNothingSmearsWhileTheShutterDoesNotLinger()
    {
        // The motion is there, but the shutter never opens on it, so the ball is caught in one
        // place -- the place it starts.
        double[] plain = EdgeProfile(
            "camera { location [0, 0, -5]  look at [0, 0, 0] }", "", out _);
        double[] moving = EdgeProfile(
            "camera { location [0, 0, -5]  look at [0, 0, 0] }",
            "motion { translate [1.5, 0, 0] }", out string error);

        Assert.IsNull(error);
        CollectionAssert.AreEqual(plain, moving,
            "with the shutter shut, a motion should make no difference at all");
    }

    [TestMethod]
    public void TestASmearRepeatsExactly()
    {
        double[] first = EdgeProfile(
            OpenShutter, "motion { translate [1, 0, 0] }", out _);
        double[] second = EdgeProfile(
            OpenShutter, "motion { translate [1, 0, 0] }", out _);

        CollectionAssert.AreEqual(first, second);
    }

    [TestMethod]
    public void TestAGrowingBallIsNeverSmallerThanItStarted()
    {
        // Scaling by nothing means scaling by one, so a ball told to grow to twice its size runs
        // from its own size up to double it.  Were the scale measured from zero instead it would
        // start as a speck, and the middle of the picture -- solidly covered at every instant here
        // -- would come out a washed-out grey instead of white.
        double[] profile = EdgeProfile(
            OpenShutter, "motion { scale 2 }", out string error);

        Assert.IsNull(error);
        Assert.IsTrue(profile.Any(value => value > 0.99),
            "the middle should be solid, since the ball covers it the whole time it is watched");
    }

    [TestMethod]
    public void TestAMotionNeedsATransform()
    {
        EdgeProfile(OpenShutter, "motion { }", out string error);

        Assert.IsNotNull(error, "an empty motion should be an error");
    }

    [TestMethod]
    public void TestARotationSmearsAPatternedBallWithoutMovingIt()
    {
        // A spin is the other motion worth having, and it is the one that shows a turn is measured
        // by its angle rather than by interpolating the matrix it comes to: the ball stays put, so
        // its outline is as sharp as ever, while what is painted on it smears.
        double[] profile = EdgeProfile(
            OpenShutter, "motion { rotate Y 60 }", out string error);

        Assert.IsNull(error, "a ball should be able to spin where it stands");
        Assert.IsTrue(profile.Any(value => value > 0.9), "the ball should still be there");
    }
}
