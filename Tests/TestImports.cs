using RayTracer.Options;
using RayTracer.Renderer;
using RayTracer.Parser;

namespace Tests;

/// <summary>
/// These tests cover importing named definitions from a library, which differs from including a
/// file in that only what was asked for is left in scope afterward.  A library may hold a hundred
/// textures, and a scene wanting two of them should not have to carry the names of the rest.
/// </summary>
[TestClass]
public class TestImports
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"import-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);

        // A library with a value, a base texture, one texture built from that base, and one
        // unrelated texture.  Between them these cover both ways a definition can depend on
        // another: by naming a value, which is looked up when the image is rendered, and by
        // extending a material, which is copied when the file is parsed.
        Write("library.igl", """
            LibraryRed = color [0.8, 0.2, 0.2]

            T_Base = material { pigment LibraryRed }
            T_Wanted = material T_Base { }
            T_Unwanted = material { pigment color [0.2, 0.2, 0.8] }
            """);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    private void Write(string name, string text)
    {
        string path = Path.Combine(_directory, name);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    /// <summary>
    /// Parses the given scene text, and reports the error it produced, or <c>null</c> if it
    /// parsed cleanly.  Parsing failures are reported rather than thrown, so what this really
    /// asks is whether an image renderer came back.
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

            return parser.Parse() is null ? output.ToString() : null;
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    [TestMethod]
    public void TestAnImportedNameIsUsable()
    {
        Assert.IsNull(ErrorFrom("""
            import 'library' { T_Wanted }
            sphere { material T_Wanted }
            """));
    }

    [TestMethod]
    public void TestANameThatWasNotImportedIsNotInScope()
    {
        // The whole point.  Reading the library is not the same as taking everything in it.
        string error = ErrorFrom("""
            import 'library' { T_Wanted }
            sphere { material T_Unwanted }
            """);

        Assert.IsNotNull(error, "an unimported name was still usable");
        StringAssert.Contains(error, "T_Unwanted");
    }

    [TestMethod]
    public void TestSeveralNamesMayBeImportedAtOnce()
    {
        Assert.IsNull(ErrorFrom("""
            import 'library' { T_Wanted, T_Unwanted }
            sphere { material T_Unwanted }
            """));
    }

    [TestMethod]
    public void TestAnImportedThingKeepsWhatItWasBuiltFrom()
    {
        // T_Wanted extends T_Base, which is not imported.  It survives because extending a
        // material copies it as the file is parsed, so what is discarded afterward is only the
        // name -- the copy inside T_Wanted is its own.
        Assert.IsNull(ErrorFrom("""
            import 'library' { T_Wanted }
            sphere { material T_Wanted }
            """));
    }

    /// <summary>
    /// Parses the given scene and then runs it, which is what a parse alone cannot do: a name that
    /// was never defined is only discovered when something goes looking for it, and that happens
    /// while the image is being made rather than while it is being read.
    /// </summary>
    private void Render(string scene)
    {
        Write("scene.igl",
            "context { width 8 height 8 no gamma }\n" +
            "camera { location [0, 0, -5] look at [0, 0, 0] }\n" +
            "light { location [0, 0, -5] }\n" +
            scene);

        string path = Path.Combine(_directory, "scene.igl");
        StringWriter output = new ();
        TextWriter was = Console.Out;

        Console.SetOut(output);

        try
        {
            LanguageParser parser = new (path);
            ImageRenderer renderer = parser.Parse();

            Assert.IsNotNull(renderer, $"the scene did not parse: {output}");

            // Running it is the point.  A name that was never defined is only discovered when
            // something goes looking for it, and nothing does until the instructions are executed.
            renderer.Render(new RenderOptions { InputFileName = path });
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    [TestMethod]
    public void TestAnImportedThingKeepsTheValuesItNames()
    {
        // The other kind of dependency, and the reason values are not filtered: T_Base names
        // LibraryRed, and that name is looked up when the image is rendered rather than when the
        // file is parsed, so discarding it would leave the material pointing at nothing.
        //
        // This one has to be rendered rather than merely parsed, and that is the whole point of it:
        // an undefined name is not a parsing failure at all.  Nothing goes looking for LibraryRed
        // until the instructions run, so a version of this that stopped at parsing would pass
        // whether the name survived or not -- which is worse than no test, since it reads like
        // cover.
        Render("""
            import 'library' { T_Wanted }
            sphere { material T_Wanted }
            """);
    }

    [TestMethod]
    public void TestAskingForSomethingTheLibraryLacksIsAnError()
    {
        // Far more likely a typo than an intention, and saying so here beats failing much later
        // with a message about an undefined variable.
        string error = ErrorFrom("import 'library' { T_NoSuchThing }");

        Assert.IsNotNull(error);
        StringAssert.Contains(error, "T_NoSuchThing");
    }

    [TestMethod]
    public void TestAMissingLibraryIsAnError()
    {
        string error = ErrorFrom("import 'no-such-library' { Anything }");

        Assert.IsNotNull(error);
        StringAssert.Contains(error, "no-such-library");
    }

    [TestMethod]
    public void TestTheExtensionMayBeLeftOffOrPutOn()
    {
        // A library is better named for what it holds than for how it is stored, but spelling out
        // the file name should not be wrong either.
        Assert.IsNull(ErrorFrom("""
            import 'library' { T_Wanted }
            sphere { material T_Wanted }
            """));
        Assert.IsNull(ErrorFrom("""
            import 'library.igl' { T_Wanted }
            sphere { material T_Wanted }
            """));
    }

    [TestMethod]
    public void TestALibraryMayIncludeAnotherFile()
    {
        Write("shared.igl", "T_Shared = material { pigment color [0.2, 0.8, 0.2] }");
        Write("compound.igl", """
            include 'shared.igl'
            T_Compound = material T_Shared { }
            """);

        Assert.IsNull(ErrorFrom("""
            import 'compound' { T_Compound }
            sphere { material T_Compound }
            """));
    }

    [TestMethod]
    public void TestWhatALibraryIncludesIsFilteredToo()
    {
        // The names a library picks up along the way are as much its business as the ones it
        // declares itself, so they are filtered on the same terms.
        Write("shared.igl", "T_Shared = material { pigment color [0.2, 0.8, 0.2] }");
        Write("compound.igl", """
            include 'shared.igl'
            T_Compound = material T_Shared { }
            """);

        string error = ErrorFrom("""
            import 'compound' { T_Compound }
            sphere { material T_Shared }
            """);

        Assert.IsNotNull(error, "a name the library merely included was still usable");
    }

    [TestMethod]
    public void TestTheSceneGoesOnAfterAnImport()
    {
        // Reading a library to its end must leave the scene's own parsing exactly where it was.
        Assert.IsNull(ErrorFrom("""
            import 'library' { T_Wanted }
            sphere { material T_Wanted  translate [-1, 0, 0] }
            sphere { material { pigment color [0, 1, 0] }  translate [1, 0, 0] }
            """));
    }

    [TestMethod]
    public void TestSeveralLibrariesMayBeImported()
    {
        Write("other.igl", "T_Other = material { pigment color [0.9, 0.9, 0.1] }");

        Assert.IsNull(ErrorFrom("""
            import 'library' { T_Wanted }
            import 'other' { T_Other }
            sphere { material T_Wanted  translate [-1, 0, 0] }
            sphere { material T_Other  translate [1, 0, 0] }
            """));
    }

    [TestMethod]
    public void TestTheScenesOwnNamesSurviveAnImport()
    {
        // Only what the library brought is filtered.  Anything the scene had already defined is
        // its own and must be left alone.
        Assert.IsNull(ErrorFrom("""
            T_Mine = material { pigment color [0.5, 0.5, 0.5] }
            import 'library' { T_Wanted }
            sphere { material T_Mine }
            """));
    }

    [TestMethod]
    public void TestALibraryIsFoundInTheStandardDirectory()
    {
        // The point of having a standard place: a scene names a library the ray tracer brought with
        // it, without knowing or caring where on disk that is.
        string installed = Path.Combine(
            LibraryLocator.LibrariesDirectory, $"test-{Guid.NewGuid():N}.igl");

        Directory.CreateDirectory(LibraryLocator.LibrariesDirectory);
        File.WriteAllText(installed, "T_Installed = material { pigment color [0.4, 0.4, 0.9] }");

        try
        {
            string name = Path.GetFileNameWithoutExtension(installed);

            Assert.IsNull(ErrorFrom(
                $"import '{name}' {{ T_Installed }}\n" +
                "sphere { material T_Installed }"));
        }
        finally
        {
            File.Delete(installed);
        }
    }

    [TestMethod]
    public void TestALibraryBesideTheSceneWinsOverAnInstalledOne()
    {
        // A scene should be able to put its own version of a library in front of the one that came
        // with the ray tracer, which is why the scene's own directory is looked at first.
        string shared = $"shadow-{Guid.NewGuid():N}";
        string installed = Path.Combine(LibraryLocator.LibrariesDirectory, $"{shared}.igl");

        Directory.CreateDirectory(LibraryLocator.LibrariesDirectory);
        File.WriteAllText(installed, "T_OnlyInstalled = material { pigment color [1, 0, 0] }");
        Write($"{shared}.igl", "T_OnlyLocal = material { pigment color [0, 1, 0] }");

        try
        {
            // The local one defines T_OnlyLocal and the installed one does not, so this succeeding
            // is what proves which was read.
            Assert.IsNull(ErrorFrom(
                $"import '{shared}' {{ T_OnlyLocal }}\n" +
                "sphere { material T_OnlyLocal }"));
        }
        finally
        {
            File.Delete(installed);
        }
    }
}
