using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the <c>isosurface</c> clause: what a scene may write, and what it is told when it
/// writes something that cannot work.
/// <para>
/// They render, rather than merely parse, for the reason the other clause tests here do: a function is
/// lowered, compiled and differentiated when the image is made, not when the file is read, so a clause
/// that parses perfectly may still have nothing behind it.  Whether the surface actually turned up is
/// read from a pixel the shape should be covering.
/// </para>
/// </summary>
[TestClass]
public class TestIsosurfaceClauses
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"isosurface-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// Renders a scene holding the given surface, looking straight at where it should be, and hands back
    /// the image along with whatever went wrong.
    /// </summary>
    private (Canvas Image, string Error) Render(string surface)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");

        File.WriteAllText(path,
            "context { angles are degrees  no gamma }\n" +
            "camera { location [0, 0, -5]  look at [0, 0, 0]  field of view 40 }\n" +
            "point light { location [-4, 6, -8]  color White }\n" + surface);

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

            string text = captured.ToString();

            return text.Contains("Error")
                ? (null, text)
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
    /// Reports whether the surface turned up in the middle of the image, which it has if the middle
    /// pixel is not the black of the empty background.
    /// </summary>
    private bool SomethingIsInTheMiddleOf(string surface)
    {
        (Canvas image, string error) = Render(surface);

        Assert.IsNull(error, error);

        Color middle = image.GetPixel(20, 20);

        return middle.Red + middle.Green + middle.Blue > 0.05;
    }

    /// <summary>
    /// This tests that a function makes a surface, and that what a scene writes as a shape's name it may
    /// now write as arithmetic instead.
    /// </summary>
    [TestMethod]
    public void TestAFunctionMakesASurface()
    {
        Assert.IsTrue(SomethingIsInTheMiddleOf("""
            isosurface {
                function { x² + y² + z² - 1 }
                bounded by [-1.1, -1.1, -1.1], [1.1, 1.1, 1.1]
                material { pigment White }
            }
            """));

        // The same sphere, written every way the language now allows: a root, a call, and a power.
        Assert.IsTrue(SomethingIsInTheMiddleOf("""
            isosurface {
                function { √(x² + y² + z²) - 1 }
                bounded by [-1.1, -1.1, -1.1], [1.1, 1.1, 1.1]
                material { pigment White }
            }
            """));
        Assert.IsTrue(SomethingIsInTheMiddleOf("""
            isosurface {
                function { pow(x, 2) + pow(y, 2) + pow(z, 2) - 1 }
                bounded by [-1.1, -1.1, -1.1], [1.1, 1.1, 1.1]
                material { pigment White }
            }
            """));
    }

    /// <summary>
    /// This tests that a function may call noise, which is the thing that makes a surface genuinely
    /// rough rather than merely shaded as though it were.  Noise takes its three coordinates separately
    /// here, since a field works in numbers throughout.
    /// </summary>
    [TestMethod]
    public void TestAFunctionMayCallNoise()
    {
        Assert.IsTrue(SomethingIsInTheMiddleOf("""
            isosurface {
                function { sqrt(x² + y² + z²) - 0.8 + 0.3 ⋅ (noise(3 * x, 3 * y, 3 * z) - 0.5) }
                bounded by [-1.3, -1.3, -1.3], [1.3, 1.3, 1.3]
                material { pigment White }
            }
            """));

        // Noise is repeatable, so the same scene twice is the same picture -- which is what lets a
        // rough surface be rendered again tomorrow and match.
        (Canvas first, string error) = Render("""
            isosurface {
                function { sqrt(x² + y² + z²) - 0.8 + 0.3 ⋅ (noise(4 * x, 4 * y, 4 * z) - 0.5) }
                bounded by [-1.3, -1.3, -1.3], [1.3, 1.3, 1.3]
                material { pigment White }
            }
            """);

        Assert.IsNull(error, error);

        (Canvas second, string _) = Render("""
            isosurface {
                function { sqrt(x² + y² + z²) - 0.8 + 0.3 ⋅ (noise(4 * x, 4 * y, 4 * z) - 0.5) }
                bounded by [-1.3, -1.3, -1.3], [1.3, 1.3, 1.3]
                material { pigment White }
            }
            """);

        for (int x = 0; x < 40; x += 7)
        for (int y = 0; y < 40; y += 7)
            Assert.IsTrue(first.GetPixel(x, y).Matches(second.GetPixel(x, y)),
                $"the same rough surface rendered twice differs at ({x}, {y})");
    }

    /// <summary>
    /// This tests that the value the surface is drawn at is honoured: the same function at a threshold
    /// too small to reach the middle of the image leaves it empty.
    /// </summary>
    [TestMethod]
    public void TestTheThresholdIsHonoured()
    {
        const string surface = """
            isosurface {
                function { x² + y² + z² }
                threshold %THRESHOLD%
                bounded by [-1.1, -1.1, -1.1], [1.1, 1.1, 1.1]
                material { pigment White }
            }
            """;

        Assert.IsTrue(SomethingIsInTheMiddleOf(surface.Replace("%THRESHOLD%", "1")));
        Assert.IsTrue(SomethingIsInTheMiddleOf(surface.Replace("%THRESHOLD%", "0.04")));

        // A radius of a thousandth is far too small to cover the middle pixel of the image.
        Assert.IsFalse(SomethingIsInTheMiddleOf(surface.Replace("%THRESHOLD%", "0.000001")));
    }

    /// <summary>
    /// This tests that a scene's own variables may be used in a function, and that x, y and z mean the
    /// point being asked about even when the scene has variables of those names.
    /// </summary>
    [TestMethod]
    public void TestAFunctionMayUseTheScenesVariables()
    {
        Assert.IsTrue(SomethingIsInTheMiddleOf("""
            radius = 0.9
            isosurface {
                function { x² + y² + z² - radius² }
                bounded by [-1.1, -1.1, -1.1], [1.1, 1.1, 1.1]
                material { pigment White }
            }
            """));

        // A scene variable called x cannot take the place of the function's own x.
        Assert.IsTrue(SomethingIsInTheMiddleOf("""
            x = 99
            isosurface {
                function { x² + y² + z² - 1 }
                bounded by [-1.1, -1.1, -1.1], [1.1, 1.1, 1.1]
                material { pigment White }
            }
            """));
    }

    /// <summary>
    /// This tests that leaving out the box is refused.  For every other surface <c>bounded by</c> is a
    /// hint the renderer may use to skip work; for this one it is the region the surface is looked for
    /// in at all, so a scene that leaves it out is told rather than handed an empty picture.
    /// </summary>
    [TestMethod]
    public void TestTheBoxIsRequired()
    {
        (Canvas image, string error) = Render("""
            isosurface {
                function { x² + y² + z² - 1 }
                material { pigment White }
            }
            """);

        Assert.IsNull(image);
        Assert.IsNotNull(error);
        Assert.Contains("bounded by", error);
        Assert.Contains("required", error);
    }

    /// <summary>
    /// This tests that leaving out the function is refused, since there is no default shape to fall back
    /// on -- the function is the whole of what the surface is.
    /// </summary>
    [TestMethod]
    public void TestTheFunctionIsRequired()
    {
        (Canvas image, string error) = Render("""
            isosurface {
                bounded by [-1, -1, -1], [1, 1, 1]
                material { pigment White }
            }
            """);

        Assert.IsNull(image);
        Assert.IsNotNull(error);
        Assert.Contains("function", error);
        Assert.Contains("required", error);
    }

    /// <summary>
    /// This tests that a function holding something a field cannot mean is reported, with enough said to
    /// act on -- and, for the vector functions, which forms of them do exist.
    /// </summary>
    [TestMethod]
    public void TestAFunctionThatCannotMeanAnything()
    {
        (Canvas image, string error) = Render("""
            isosurface {
                function { length([x, y, z]) - 1 }
                bounded by [-1.1, -1.1, -1.1], [1.1, 1.1, 1.1]
                material { pigment White }
            }
            """);

        Assert.IsNull(image);
        // The tuple is what stops it, and says what to write instead.
        Assert.Contains("tuple cannot appear", error);
        Assert.Contains("x\u00b2 + y\u00b2 + z\u00b2", error);

        // A name that is neither one of the three variables nor anything the scene set.
        (image, error) = Render("""
            isosurface {
                function { x² + wobble - 1 }
                bounded by [-1.1, -1.1, -1.1], [1.1, 1.1, 1.1]
                material { pigment White }
            }
            """);

        Assert.IsNull(image);
        Assert.Contains("'wobble'", error);

        // And a function whose slope is not known, which can be evaluated but cannot be a surface.
        (image, error) = Render("""
            isosurface {
                function { smoothstep(0, 1, x) - 0.5 }
                bounded by [-1.1, -1.1, -1.1], [1.1, 1.1, 1.1]
                material { pigment White }
            }
            """);

        Assert.IsNull(image);
        Assert.Contains("smoothstep", error);
    }

    /// <summary>
    /// This tests that the accuracy must be a positive number, since a crossing cannot be pinned down to
    /// within nothing at all.
    /// </summary>
    [TestMethod]
    public void TestTheAccuracyMustBePositive()
    {
        (Canvas image, string error) = Render("""
            isosurface {
                function { x² + y² + z² - 1 }
                accuracy 0
                bounded by [-1.1, -1.1, -1.1], [1.1, 1.1, 1.1]
                material { pigment White }
            }
            """);

        Assert.IsNull(image);
        Assert.Contains("greater than zero", error);
    }

    /// <summary>
    /// This tests that an isosurface takes the properties every surface has -- a material, a transform,
    /// a name to be reused by -- since it is a surface like any other and is only unusual in how its
    /// shape is arrived at.
    /// </summary>
    [TestMethod]
    public void TestAnIsosurfaceIsASurfaceLikeAnyOther()
    {
        // Moved out of the middle by a transform, so the middle should be empty...
        Assert.IsFalse(SomethingIsInTheMiddleOf("""
            isosurface {
                function { x² + y² + z² - 1 }
                bounded by [-1.1, -1.1, -1.1], [1.1, 1.1, 1.1]
                material { pigment White }
                translate [8, 0, 0]
            }
            """));

        // ...and put back by a transform that undoes it.
        Assert.IsTrue(SomethingIsInTheMiddleOf("""
            isosurface {
                function { x² + y² + z² - 1 }
                bounded by [-1.1, -1.1, -1.1], [1.1, 1.1, 1.1]
                material { pigment White }
                translate [8, 0, 0]
                translate [-8, 0, 0]
            }
            """));

        // And a scaled one is bigger, which the box being in the surface's own space is what makes work.
        Assert.IsTrue(SomethingIsInTheMiddleOf("""
            isosurface {
                function { x² + y² + z² - 1 }
                bounded by [-1.1, -1.1, -1.1], [1.1, 1.1, 1.1]
                material { pigment White }
                scale 2
            }
            """));
    }

    /// <summary>
    /// This tests that an isosurface may be nested inside the things that hold other surfaces.  The
    /// grammar allowed this from the start but nothing behind it did, since every test above places its
    /// surface at the top level, and so a grouped isosurface failed outright.
    /// </summary>
    [TestMethod]
    public void TestAnIsosurfaceMayBeNestedInOtherSurfaces()
    {
        Assert.IsTrue(SomethingIsInTheMiddleOf("""
            group {
                isosurface {
                    function { x² + y² + z² - 1 }
                    bounded by [-1.1, -1.1, -1.1], [1.1, 1.1, 1.1]
                    material { pigment White }
                }
            }
            """));

        // A sphere with a bite taken out of one side, so what is left still covers the middle.
        Assert.IsTrue(SomethingIsInTheMiddleOf("""
            difference {
                isosurface {
                    function { x² + y² + z² - 1 }
                    bounded by [-1.1, -1.1, -1.1], [1.1, 1.1, 1.1]
                    material { pigment White }
                }
                sphere {
                    material { pigment White }
                    scale 0.5
                    translate [1, 0, 0]
                }
            }
            """));
    }
}
