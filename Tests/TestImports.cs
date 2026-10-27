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

        // And the sort a curated library actually is: one name worth exporting, built out of several
        // that are nobody else's business.
        Write("trees.igl", """
            Bark = material { pigment [0.5, 0.35, 0.24] }

            function taper(n) -> number { return n * 0.7 }

            primitive twig(size) -> sphere {
                return sphere { material { pigment [0.2, 0.5, 0.2] }  scale size }
            }

            primitive elm(height) -> group {
                return group {
                    cylinder {
                        min Y 0  max Y height
                        material Bark
                        scale [taper(0.3), 1, taper(0.3)]
                    }
                    object twig(height * 0.35) { translate Y height }
                }
            }
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
    /// Renders the given scene and reports what stopped it, or <c>null</c> if nothing did.
    /// <para>
    /// Parsing is not enough to ask this question.  A name that is not there is discovered when
    /// something goes looking for it, and nothing does until the instructions run -- so a check that
    /// stopped at parsing would pass whether the name survived or not, which is worse than no check
    /// at all because it reads like one.
    /// </para>
    /// </summary>
    private string ErrorFromRendering(string scene)
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
            ImageRenderer renderer = new LanguageParser(path).Parse();

            renderer?.Render(new RenderOptions { InputFileName = path });
        }
        catch (Exception exception)
        {
            return exception.ToString();
        }
        finally
        {
            Console.SetOut(was);
        }

        return output.ToString().Contains("Error") ? output.ToString() : null;
    }

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
    public void TestALibraryKeepsItsHelpersToItself()
    {
        // What an import has always promised and could not keep once a library could hold more than
        // textures.  A tree is an elm and a twig and a taper, and only the first is anybody else's
        // business.
        Render("""
            import 'trees' { elm }
            object elm(3)
            """);

        string function = ErrorFromRendering("""
            import 'trees' { elm }
            sphere { scale taper(2) }
            """);
        string primitive = ErrorFromRendering("""
            import 'trees' { elm }
            object twig(1)
            """);

        Assert.IsNotNull(function, "the library's own function should not have reached the scene");
        StringAssert.Contains(function, "taper");
        Assert.IsNotNull(primitive, "the library's own primitive should not have reached the scene");
        StringAssert.Contains(primitive, "twig");
    }

    [TestMethod]
    public void TestAnExportKeepsSightOfWhatItWasWrittenAmong()
    {
        // The half that makes the other half possible, and the reason a library needs a scope rather
        // than a filter.  Throwing away what the scene did not ask for would throw away what `elm` is
        // built out of; keeping it in the library's own names costs `elm` nothing, since a primitive
        // goes on looking where it was written.  If this broke, the test above would still pass -- the
        // helpers would be gone, along with the export that needed them.
        Render("""
            import 'trees' { elm }
            object elm(2)
            object elm(3) { translate X 4 }
            """);
    }

    [TestMethod]
    public void TestAHelperMayBeAskedForByName()
    {
        // Nothing is secret; it simply is not handed over unasked.
        Render("""
            import 'trees' { elm, taper, twig }
            object elm(3)
            object twig(taper(1))
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
    // -- A library importing another library ---------------------------------------------------------

    [TestMethod]
    public void TestALibraryMayImportAnotherLibrary()
    {
        // The whole point of the change: a curated library should be able to build on another rather
        // than carry its own copy of it.  Before this, an `import` inside a library reached the general
        // clause set -- which does not hold one -- and the author was told they had written an
        // "unsupported object type", which says nothing about what is actually wrong.
        Write("inner.igl", """
            InnerBlue = color [0.2, 0.3, 0.9]
            InnerPaint = material { pigment InnerBlue }

            primitive InnerBall(size) -> group {
                return group { sphere { material InnerPaint  scale size } }
            }
            """);
        Write("outer.igl", """
            import 'inner' { InnerBall, InnerPaint }

            primitive OuterPair(size) -> group {
                return group {
                    object InnerBall(size) { translate X -size }
                    object InnerBall(size) { translate X  size }
                }
            }
            """);

        Render("""
            import 'outer' { OuterPair }
            object OuterPair(0.6)
            """);
    }

    [TestMethod]
    public void TestAnInnerLibrarysNamesDoNotReachTheScene()
    {
        // The filtering has to keep working through a layer.  A scene that imports the outer library
        // asked for nothing out of the inner one, so nothing of the inner one's should be spellable --
        // otherwise importing anything would quietly drag a whole tree of names into scope.
        Write("inner.igl", """
            InnerPaint = material { pigment [0.2, 0.3, 0.9] }

            primitive InnerBall(size) -> group {
                return group { sphere { material InnerPaint  scale size } }
            }
            """);
        Write("outer.igl", """
            import 'inner' { InnerBall }

            primitive OuterPair(size) -> group {
                return group { object InnerBall(size) }
            }
            """);

        string error = ErrorFromRendering("""
            import 'outer' { OuterPair }
            object InnerBall(0.6)
            """);

        Assert.IsNotNull(error, "the inner library's primitive should not be in the scene's scope");
        Assert.Contains("InnerBall", error);
        // And specifically because the *scene* does not have the name -- not because the outer library
        // failed to parse, which is the other way this test can go green while proving nothing.  Before
        // nested imports worked at all, asserting only that the message mentioned `InnerBall` passed.
        Assert.Contains("this scene has declared", error);
    }

    [TestMethod]
    public void TestLibrariesThatImportEachOtherAreReportedRatherThanRecursed()
    {
        // Two libraries naming each other used to be a stack overflow, which tells an author nothing.
        // The message names the whole chain, because which pair closes the loop is the only thing that
        // says where to cut it.
        Write("ringA.igl", """
            import 'ringB' { RingB }
            primitive RingA(size) -> group { return group { object RingB(size) } }
            """);
        Write("ringB.igl", """
            import 'ringA' { RingA }
            primitive RingB(size) -> group { return group { sphere { scale size } } }
            """);

        string error = ErrorFromRendering("""
            import 'ringA' { RingA }
            object RingA(0.6)
            """);

        Assert.IsNotNull(error, "a loop of imports should be reported");
        Assert.Contains("loop", error);
        Assert.Contains("ringA.igl", error);
        Assert.Contains("ringB.igl", error);
    }

    [TestMethod]
    public void TestALibraryMayImportOneThatImportsAnother()
    {
        // Two layers rather than one, since a chain is where a fix that only handles a single level
        // shows itself.
        Write("bottom.igl", """
            BottomPaint = material { pigment [0.9, 0.4, 0.2] }
            primitive BottomBall(size) -> group {
                return group { sphere { material BottomPaint  scale size } }
            }
            """);
        Write("middle.igl", """
            import 'bottom' { BottomBall }
            primitive MiddlePair(size) -> group {
                return group {
                    object BottomBall(size) { translate X -size }
                    object BottomBall(size) { translate X  size }
                }
            }
            """);
        Write("top.igl", """
            import 'middle' { MiddlePair }
            primitive TopRow(size) -> group {
                return group {
                    object MiddlePair(size)
                    object MiddlePair(size) { translate Z size * 3 }
                }
            }
            """);

        Render("""
            import 'top' { TopRow }
            object TopRow(0.4)
            """);
    }

    // -- What a library's values call ----------------------------------------------------------------

    /// <summary>
    /// Writes a library whose exports are values made by calling helpers it keeps to itself: a primitive of each
    /// sort that can be named in a value -- a surface, a material, an interior and a medium -- and a function.
    /// </summary>
    private void WritePottery()
    {
        Write("pottery.igl", """
            function flared(n) -> number { return n * 1.1 }

            primitive lump(size) -> sphere { return sphere { scale size } }
            primitive glazed(hue) -> material { return material { pigment hue  specular 0.6 } }
            primitive glassy(index) -> interior { return interior { ior index } }
            primitive smoky(thickness) -> medium {
                return medium { scattering thickness  absorption thickness * 0.1 }
            }

            Bowl = group { object lump(1)  object lump(0.5) { translate Y 1 } }
            Jar = sphere { scale flared(1) }
            Vase = cylinder { min Y 0  max Y 1  material glazed([0.2, 0.4, 0.7]) }
            Bottle = sphere { material { transparency 0.9  interior glassy(1.5) } }
            Smoke = sphere { material { transparency 1  interior { medium smoky(0.5) } } }
            """);
    }

    [TestMethod]
    public void TestAValueKeepsSightOfTheHelpersItCalls()
    {
        // A value a scene imports is a recipe the scene makes for itself, and a call inside it names a helper the
        // library kept back.  Until 2026-10-26 the recipe was made among the scene's names alone, so every one of
        // these failed -- "nothing named lump is a primitive" -- unless the scene asked for the helpers too, which it
        // has no business knowing about.  The Eagle's command module was the first value to find it.
        WritePottery();

        foreach (string value in new[] { "Bowl", "Jar", "Vase", "Bottle", "Smoke" })
        {
            string error = ErrorFromRendering($$"""
                import 'pottery' { {{value}} }
                object {{value}}
                """);

            Assert.IsNull(error, $"{value} could not find what it calls: {error}");
        }
    }

    [TestMethod]
    public void TestWhatASceneAddsToAValueIsReadAmongTheScenesOwnNames()
    {
        // The other half: a value the scene adds to is read in two sets of names at once.  What the library wrote
        // finds the library's helpers, and what the scene wrote -- here a loop's own counter, which nothing but
        // the loop can see, and a value the scene names after the import -- finds the scene's.  Making the whole
        // value among the library's names would lose the counter.
        WritePottery();

        Assert.IsNull(ErrorFromRendering("""
            import 'pottery' { Bowl }
            spacing = 3
            group {
                for i in [0, 2] {
                    object Bowl { translate X i * spacing }
                }
            }
            """));
    }

    [TestMethod]
    public void TestAValueCallsItsOwnLibrarysHelperAndNotOneTheSceneNamedAlike()
    {
        // The scene was never given `lump`, so it may name a primitive of its own so.  The library's bowl must go on
        // calling the library's: the scene's takes two values, so a bowl that reached for it would be refused.
        WritePottery();

        Assert.IsNull(ErrorFromRendering("""
            import 'pottery' { Bowl }
            primitive lump(across, high) -> cube { return cube { scale [across, high, across] } }
            object Bowl
            object lump(1, 2) { translate X 3 }
            """));
    }

    [TestMethod]
    public void TestTwoLibrariesMayKeepHelpersOfTheSameName()
    {
        // Two libraries, each keeping a helper called `part` that takes a different number of values.  Each one's
        // value has to find its own; handing the scene every library's helpers would leave the second import's
        // `part` standing where the first library's value went looking for its own, and the call refused.
        Write("left.igl", """
            primitive part() -> sphere { return sphere { } }
            LeftThing = group { object part() }
            """);
        Write("right.igl", """
            primitive part(across, high) -> cube { return cube { scale [across, high, across] } }
            RightThing = group { object part(1, 2) }
            """);

        Assert.IsNull(ErrorFromRendering("""
            import 'left' { LeftThing }
            import 'right' { RightThing }
            object LeftThing
            object RightThing { translate X 3 }
            """));
    }

    [TestMethod]
    public void TestAValueFromALibraryALibraryImportsKeepsSightOfItsHelpers()
    {
        // A layer down, where a fix for one level shows itself: the inner library's value calls a helper the inner
        // library keeps back, the outer library builds a value of its own round it, and the scene asks for that.
        Write("inner.igl", """
            primitive knob(size) -> sphere { return sphere { scale size } }
            Handle = group { object knob(0.3) }
            """);
        Write("outer.igl", """
            import 'inner' { Handle }
            primitive drawer(wide) -> cube { return cube { scale [wide, 0.5, 0.5] } }
            Chest = group { object drawer(2)  object Handle { translate Z -0.6 } }
            """);

        Assert.IsNull(ErrorFromRendering("""
            import 'outer' { Chest }
            object Chest
            """));
    }
}
