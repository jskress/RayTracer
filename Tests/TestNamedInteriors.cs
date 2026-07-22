using RayTracer.Parser;

namespace Tests;

/// <summary>
/// These tests cover naming an interior, which is what a surface is made of rather than what its
/// skin looks like.  Materials, pigments and transforms could always be declared once and named
/// later; interiors could not, and POV-Ray's glass library declares its interiors apart from its
/// textures, so bringing that library across depends on this.
/// </summary>
[TestClass]
public class TestNamedInteriors
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"interior-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    private void Write(string name, string text) =>
        File.WriteAllText(Path.Combine(_directory, name), text);

    /// <summary>
    /// Renders the given scene, and reports the error it produced, or <c>null</c> if all was
    /// well.  It renders rather than merely parsing, since a name that nothing defines is not a
    /// parsing failure: values are looked up when the image is made, so a scene naming an
    /// interior that does not exist parses perfectly and dies later.
    /// </summary>
    private string ErrorFrom(string scene)
    {
        Write("scene.igl",
            "camera { location [0, 0, -5] look at [0, 0, 0] }\n" +
            "light { location [0, 0, -5] }\n" +
            scene);

        StringWriter output = new ();
        TextWriter was = Console.Out;

        Console.SetOut(output);

        try
        {
            LanguageParser parser = new (Path.Combine(_directory, "scene.igl"));
            RayTracer.Renderer.ImageRenderer renderer = parser.Parse();

            if (renderer is null)
                return output.ToString();

            renderer.Render(new RayTracer.Options.RenderOptions
            {
                OutputFileName = Path.Combine(_directory, "out.png"),
                Width = 8, Height = 8
            });

            // A render that went well still says so, so it is the word "Error" that matters here
            // rather than whether anything was said at all.
            string text = output.ToString();

            return text.Contains("Error") ? text : null;
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
    public void TestAnInteriorMayBeDeclaredAndNamed()
    {
        Assert.IsNull(ErrorFrom("""
            Glass1Interior = interior { ior 1.5 }

            sphere {
                material {
                    pigment color [1, 1, 1, 0.2]
                    interior Glass1Interior
                }
            }
            """));
    }

    [TestMethod]
    public void TestANamedInteriorMayBeAddedTo()
    {
        // The same shape as extending a material: the name brings a copy, and the braces say what
        // to change about it.
        Assert.IsNull(ErrorFrom("""
            Glass1Interior = interior { ior 1.5 }
            FoggyInterior = interior Glass1Interior { clarity 0.5 }

            sphere {
                material {
                    pigment color [1, 1, 1, 0.2]
                    interior FoggyInterior
                }
            }
            """));
    }

    [TestMethod]
    public void TestANamedInteriorMayBeImported()
    {
        Write("glass.igl", """
            Glass1Interior = interior { ior 1.5 }
            Glass2Interior = interior { ior 1.7 }
            """);

        Assert.IsNull(ErrorFrom("""
            import 'glass' { Glass1Interior }

            sphere {
                material {
                    pigment color [1, 1, 1, 0.2]
                    interior Glass1Interior
                }
            }
            """));
    }

    [TestMethod]
    public void TestAnInteriorThatWasNotImportedIsNotInScope()
    {
        Write("glass.igl", """
            Glass1Interior = interior { ior 1.5 }
            Glass2Interior = interior { ior 1.7 }
            """);

        string error = ErrorFrom("""
            import 'glass' { Glass1Interior }

            sphere {
                material {
                    pigment color [1, 1, 1, 0.2]
                    interior Glass2Interior
                }
            }
            """);

        Assert.IsNotNull(error, "an unimported interior was still usable");
        StringAssert.Contains(error, "Glass2Interior");
    }

    [TestMethod]
    public void TestAnInteriorMayNotBeInherited()
    {
        // A material may say "inherited" because there is a way of handing one down to the pieces
        // of a surface that named none of their own.  Nothing hands interiors down, so the word
        // would mean nothing here and is refused rather than quietly accepted.
        string error = ErrorFrom("""
            sphere {
                material {
                    pigment color [1, 1, 1, 0.2]
                    interior inherited
                }
            }
            """);

        Assert.IsNotNull(error, "\"interior inherited\" was accepted");
    }

    [TestMethod]
    public void TestNamingAnInteriorThatDoesNotExistIsReported()
    {
        string error = ErrorFrom("""
            sphere {
                material {
                    pigment color [1, 1, 1, 0.2]
                    interior NoSuchInterior
                }
            }
            """);

        Assert.IsNotNull(error, "an undefined interior was accepted");
        StringAssert.Contains(error, "NoSuchInterior");
    }
}
