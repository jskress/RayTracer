using System.Text;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.PovRay;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover a converted library end to end: read, written, imported by a scene, and
/// rendered.
/// <para>
/// The rendering is the point of them.  A name that nothing defines is not a parsing failure --
/// values are looked up when the image is made -- so a library full of broken references reads
/// perfectly and fails much later, complaining about an undefined variable and saying nothing
/// about where the mistake was.  Parsing these would prove very little.
/// </para>
/// </summary>
[TestClass]
public class TestPovLibraries
{
    /// <summary>
    /// A small stand-in for POV-Ray's own files, holding one of each sort of thing the converter
    /// produces, plus a second file that leans on the first.
    /// </summary>
    private const string BaseSource = """
        #declare Col_Ruby = color rgb <0.9, 0.1, 0.2>;
        #declare Col_Hazy = color rgbt <0.8, 0.9, 0.85, 0.4>;
        #declare Depth = 1.5;
        #declare Axis = <1, 0.5, 0.25>;

        #declare F_Shiny = finish { specular 1 roughness 0.02 }

        #declare I_Glass = interior { ior 1.5 fade_distance 2 }

        #declare P_Speck = pigment {
            granite
            turbulence <0.4, 0.1, 0.05>
            octaves 3
            color_map {
                [0.0, 0.5 color rgb <0.2, 0.2, 0.25> color rgb <0.7, 0.7, 0.75>]
                [0.5, 1.001 color rgb <0.7, 0.7, 0.75> color rgb <0.2, 0.2, 0.25>]
            }
            scale <0.5, 0.5, 1>
        }

        #declare T_Plain = texture {
            pigment { color Col_Ruby }
            finish { ambient 0.2 diffuse 0.7 metallic }
        }

        #declare T_Stone = texture { pigment { P_Speck } finish { F_Shiny } }

        #declare T_Layered =
            texture { pigment { color rgb <0.4, 0.25, 0.15> } finish { specular 0.3 } }
            texture { pigment { color rgbt <0.8, 0.7, 0.6, 0.6> } }

        #declare M_Bottle = material {
            texture { pigment { color Col_Hazy } finish { specular 1 roughness 0.005 } }
            interior { I_Glass }
        }
        """;

    private const string DerivedSource = """
        #declare T_Derived = texture { T_Plain finish { specular 0.9 } }
        """;

    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"pov-libraries-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// Converts the given POV-Ray sources and writes the libraries beside where the scene will be,
    /// so that an import finds them without touching the user's own library directory.
    /// </summary>
    private PovConversion ConvertAndWrite(params (string Name, string Text)[] sources)
    {
        foreach ((string name, string text) in sources)
            File.WriteAllText(Path.Combine(_directory, name), text);

        PovSourceReader reader = new PovSourceReader(
            _directory, [], sources.Select(source => source.Name));
        List<PovFile> files = reader.ReadAll();
        List<PovIssue> issues = reader.Issues.ToList();
        PovEmitter emitter = new PovEmitter(files, issues);
        List<PovGeneratedLibrary> libraries = [];

        foreach (PovFile file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file.Name);

            libraries.Add(new PovGeneratedLibrary
            {
                Name = name,
                Text = emitter.Emit(file),
                Names = emitter.Emitted.Where(emitted => emitted.Library == name).ToList(),
                SourceDeclarations = file.Declarations.Count
            });
        }

        PovConversion conversion = new PovConversion
        {
            Libraries = libraries, Issues = issues, Names = emitter.Emitted.ToList()
        };

        PovLibraryConverter.Write(conversion, _directory);

        return conversion;
    }

    /// <summary>
    /// Builds a scene that imports and uses every name the given library declares, renders it, and
    /// returns the error it produced, or <c>null</c> if all was well.  Each sort of thing has to
    /// be used the way that sort of thing is used, or the scene would name it without ever making
    /// the renderer resolve it.
    /// </summary>
    private string RenderEverythingIn(PovGeneratedLibrary library)
    {
        StringBuilder scene = new StringBuilder();

        scene.AppendLine("camera { location [0, 0, -9] look at [0, 0, 0] }");
        scene.AppendLine("light { location [0, 0, -9] }");
        scene.AppendLine(
            $"import '{library.Name}' " +
            $"{{ {string.Join(", ", library.Names.Select(name => name.Name))} }}");

        int index = 0;

        foreach (PovEmittedName name in library.Names)
        {
            string body = name.Kind switch
            {
                "material" => $"material {name.Name}",
                "pigment" => $"material {{ pigment {name.Name} }}",
                "interior" => $"material {{ pigment color [1, 1, 1, 0.3] interior {name.Name} }}",
                "color" => $"material {{ pigment color {name.Name} }}",
                "vector" => $"material {{ pigment color [1, 1, 1] }} scale {name.Name}",
                _ => $"material {{ pigment color [1, 1, 1] ambient {name.Name} }}"
            };

            scene.AppendLine($"sphere {{ {body} translate X {index++ % 12} }}");
        }

        return Render(scene.ToString(), $"scene-{library.Name}.igl");
    }

    /// <summary>
    /// Renders the given scene and reports what went wrong, or <c>null</c> if nothing did.
    /// </summary>
    private string Render(string scene, string fileName)
    {
        string path = Path.Combine(_directory, fileName);

        File.WriteAllText(path, scene);

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
                OutputFileName = Path.ChangeExtension(path, ".png"), Width = 8, Height = 8
            });

            // A render that went well still says so, so it is the word "Error" that matters rather
            // than whether anything was said at all.
            string text = output.ToString();

            return text.Contains("Error") ? text : null;
        }
        catch (Exception exception)
        {
            return exception.ToString();
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    [TestMethod]
    public void TestEveryConvertedDefinitionCanBeImportedAndRendered()
    {
        PovConversion conversion = ConvertAndWrite(("base.inc", BaseSource));
        PovGeneratedLibrary library = conversion.Libraries.Single();

        // Everything in the source should have come across; nothing in it is beyond us.
        Assert.AreEqual(0, conversion.Issues.Count,
            $"unexpected issues: {string.Join("; ", conversion.Issues)}");

        Assert.IsNull(RenderEverythingIn(library));
    }

    [TestMethod]
    public void TestALibraryMayLeanOnAnother()
    {
        PovConversion conversion = ConvertAndWrite(
            ("base.inc", BaseSource), ("derived.inc", DerivedSource));
        PovGeneratedLibrary derived = conversion.Libraries.Single(
            library => library.Name == "derived");

        StringAssert.Contains(derived.Text, "include 'base.igl'");

        Assert.IsNull(RenderEverythingIn(derived));
    }

    [TestMethod]
    public void TestAnImportTakesOnlyWhatItAskedFor()
    {
        ConvertAndWrite(("base.inc", BaseSource));

        Assert.IsNull(Render("""
            camera { location [0, 0, -5] look at [0, 0, 0] }
            light { location [0, 0, -5] }
            import 'base' { StoneMaterial }
            sphere { material StoneMaterial }
            """, "wanted.igl"));

        string error = Render("""
            camera { location [0, 0, -5] look at [0, 0, 0] }
            light { location [0, 0, -5] }
            import 'base' { StoneMaterial }
            sphere { material PlainMaterial }
            """, "unwanted.igl");

        Assert.IsNotNull(error, "a name that was not imported was still usable");
        StringAssert.Contains(error, "PlainMaterial");
    }

    [TestMethod]
    public void TestAConvertedMaterialMayBeAddedTo()
    {
        // The whole scheme rests on this: a scene takes a library's material and changes what it
        // wants about it.
        ConvertAndWrite(("base.inc", BaseSource));

        Assert.IsNull(Render("""
            camera { location [0, 0, -5] look at [0, 0, 0] }
            light { location [0, 0, -5] }
            import 'base' { PlainMaterial, GlassInterior }
            sphere { material PlainMaterial { pigment color [0.2, 0.4, 0.8] } }
            sphere {
                material PlainMaterial { interior GlassInterior }
                translate X 2
            }
            """, "extended.igl"));
    }

    [TestMethod]
    public void TestNamesAreNotDeclaredTwiceAcrossLibraries()
    {
        // Two libraries declaring the same name collide silently, the last one read winning, so
        // the converter is the only place it can be caught.
        PovConversion conversion = ConvertAndWrite(
            ("base.inc", BaseSource), ("derived.inc", DerivedSource));

        Assert.AreEqual(0, conversion.Clashes.Count(),
            $"clashing names: {string.Join(", ", conversion.Clashes.Select(clash => clash.Key))}");
    }

    [TestMethod]
    public void TestAClashBetweenLibrariesIsFound()
    {
        PovConversion conversion = ConvertAndWrite(
            ("one.inc", "#declare F_Metal = finish { specular 1 roughness 0.05 }"),
            ("two.inc", "#declare F_Metal = finish { specular 1 roughness 0.02 }"));

        Assert.AreEqual("MetalFinish", conversion.Clashes.Single().Key);
    }

    [TestMethod]
    public void TestTheRealPovRayLibrariesConvertAndRender()
    {
        // The synthetic sources above cover the shapes; this covers the real thing, which has a
        // way of holding constructs nobody would think to invent.  It is skipped where POV-Ray's
        // include files are not to hand, since not every machine has them.
        string source = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "povray", "povray", "distribution", "include");

        if (!PovLibraryConverter.LibraryFiles.All(name => File.Exists(Path.Combine(source, name))))
        {
            Assert.Inconclusive(
                $"POV-Ray's include files are not in {source}, so there is nothing to convert.");
        }

        PovConversion conversion = new PovLibraryConverter().Convert(source);

        PovLibraryConverter.Write(conversion, _directory);

        Assert.AreEqual(
            PovLibraryConverter.LibraryFiles.Length, conversion.Libraries.Count,
            "not every file became a library");

        foreach (PovGeneratedLibrary library in conversion.Libraries)
        {
            Assert.IsTrue(library.Names.Count > 0, $"{library.Name} declares nothing");
            Assert.IsNull(RenderEverythingIn(library), $"{library.Name} did not render");
        }
    }
}
