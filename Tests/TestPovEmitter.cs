using RayTracer.PovRay;

namespace Tests;

/// <summary>
/// These tests cover turning what was read from POV-Ray into the ray tracer's own language.
/// <para>
/// A good deal of this is about what cannot come across rather than what can.  POV-Ray declares
/// things that have no counterpart here -- a colour map on its own, a pigment left to be given its
/// colours later -- and the rule throughout is that such a declaration is left out and reported,
/// never half-written, since half a block would stop the whole library reading.
/// </para>
/// </summary>
[TestClass]
public class TestPovEmitter
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"pov-emitter-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// Converts the given POV-Ray text and returns the library it becomes.
    /// </summary>
    private string Convert(string text) => Convert(text, out _);

    /// <summary>
    /// Converts the given POV-Ray text, reporting anything that could not be brought across.
    /// </summary>
    private string Convert(string text, out List<PovIssue> issues)
    {
        File.WriteAllText(Path.Combine(_directory, "test.inc"), text);

        PovSourceReader reader = new PovSourceReader(_directory, [], ["test.inc"]);
        List<PovFile> files = reader.ReadAll();
        List<PovIssue> collected = reader.Issues.ToList();
        PovEmitter emitter = new PovEmitter(files, collected);
        string result = emitter.Emit(files.Single());

        issues = collected;

        return result;
    }

    [TestMethod]
    public void TestAColorBecomesAColor()
    {
        StringAssert.Contains(
            Convert("#declare Col_Ruby = color rgb <0.9, 0.1, 0.2>;"),
            "RubyColor = color [0.9, 0.1, 0.2]");
    }

    [TestMethod]
    public void TestAVectorStaysAVector()
    {
        // POV-Ray does not tell colours and vectors apart, but it writes them differently, and
        // what a thing was written as is what it was meant as.  A scene can scale by a vector.
        StringAssert.Contains(
            Convert("#declare GoldBase = <1.0, 0.875, 0.575>;"),
            "GoldBase = [1, 0.875, 0.575]");
    }

    [TestMethod]
    public void TestTransmitBecomesTheColorsAlpha()
    {
        // POV-Ray's transmit of 0.25 lets a quarter of the light straight through, which is an
        // alpha of three quarters here.
        StringAssert.Contains(
            Convert("#declare Col_Hazy = color rgbt <0.5, 0.6, 0.7, 0.25>;"),
            "HazyColor = color [0.5, 0.6, 0.7, 0.75]");
    }

    [TestMethod]
    public void TestATextureBecomesAMaterial()
    {
        string library = Convert("""
            #declare T_Plain = texture {
                pigment { color rgb <0.2, 0.4, 0.6> }
                finish { ambient 0.2 diffuse 0.7 }
            }
            """);

        StringAssert.Contains(library, "PlainMaterial = material {");
        StringAssert.Contains(library, "pigment color [0.2, 0.4, 0.6]");
        StringAssert.Contains(library, "ambient 0.2");
        StringAssert.Contains(library, "diffuse 0.7");
    }

    [TestMethod]
    public void TestRoughnessBecomesShininess()
    {
        // The factor between them was measured by rendering a lit sphere at each roughness in
        // both and counting the pixels the highlight covered: shininess = 1/(4 * roughness).
        // A roughness of 0.05, which is what most of the metals use, wants a shininess of 5.
        StringAssert.Contains(
            Convert("#declare F_Metal = finish { specular 0.8 roughness 0.05 }"),
            "shininess 5");

        string library = Convert("#declare F_Soft = finish { specular 0.5 roughness 0.2 }");

        StringAssert.Contains(library, "SoftFinish = material {");
        StringAssert.Contains(library, "shininess 1.25");
    }

    [TestMethod]
    public void TestTheGlassiestFinishesStayWithinReach()
    {
        // POV-Ray's tightest library finish is a roughness of 0.0001.  Under the old, wrong
        // conversion that asked for a shininess of two hundred million, which draws no highlight
        // at all because none of it lands on a pixel.  It should stay somewhere a renderer can
        // actually draw.
        string library = Convert(
            "#declare F_Glassy = finish { specular 1 roughness 0.0001 }", out List<PovIssue> issues);

        StringAssert.Contains(library, "shininess 2500");
        Assert.AreEqual(0, issues.Count, "nothing about this finish is beyond us");
    }

    [TestMethod]
    public void TestMetallicIsAlwaysGivenItsAmount()
    {
        // Both languages let the amount be left off, but the ray tracer's grammar then takes
        // whatever property comes next as the amount and chokes on the number after it.
        string library = Convert("""
            #declare T_Metal = texture {
                pigment { color rgb 1 }
                finish { metallic specular 0.8 }
            }
            """);

        StringAssert.Contains(library, "metallic 1");
        StringAssert.Contains(library, "specular 0.8");
    }

    [TestMethod]
    public void TestAFinishBecomesAMaterialWithNothingButItsFinish()
    {
        // There is no finish of its own here, so it becomes a material a texture can be built on.
        string library = Convert("#declare Dull = finish { specular 0.5 roughness 0.15 }");

        StringAssert.Contains(library, "DullFinish = material {");
        Assert.IsFalse(library.Contains("pigment"), "a finish should say nothing about colour");
    }

    [TestMethod]
    public void TestAnInteriorBecomesAnInterior()
    {
        string library = Convert("#declare I_Glass = interior { ior 1.5 fade_distance 2 }");

        StringAssert.Contains(library, "GlassInterior = interior {");
        StringAssert.Contains(library, "ior 1.5");
        StringAssert.Contains(library, "clarity 2");
    }

    [TestMethod]
    public void TestAPatternPigmentCarriesItsMapAndTransforms()
    {
        string library = Convert("""
            #declare T_Stone = texture {
                pigment {
                    granite
                    turbulence 0.4
                    color_map { [0.0 color rgb <0.2, 0.2, 0.2>][1.0 color rgb <0.8, 0.8, 0.8>] }
                    scale <0.5, 0.5, 1>
                    rotate <3, 0, 0>
                }
            }
            """);

        StringAssert.Contains(library, "pigment granite {");
        StringAssert.Contains(library, "turbulence { amplitude 0.4 }");
        StringAssert.Contains(library, "[0, [0.2, 0.2, 0.2], 1, [0.8, 0.8, 0.8]]");
        StringAssert.Contains(library, "scale [0.5, 0.5, 1]");
        StringAssert.Contains(library, "rotate X 3");
    }

    [TestMethod]
    public void TestTurbulenceThatDiffersByAxisIsKeptThatWay()
    {
        // Averaging this is not merely less exact, it is ruinous: the wood pigments ask for a
        // gentle wobble across the grain and a large one along it, and one number cannot say it.
        StringAssert.Contains(
            Convert("""
                #declare P_Grain = pigment {
                    wood
                    turbulence <0.1, 0.04, 1>
                    color_map { [0.0 color rgb 0][1.0 color rgb 1] }
                }
                """),
            "amplitude [0.1, 0.04, 1]");
    }

    [TestMethod]
    public void TestABandedMapIsFlattenedWithoutRepeatingItsJoins()
    {
        // POV-Ray's older map form gives a band and the colour at each end of it, and the bands
        // run into one another, so the colour ending one starts the next.
        StringAssert.Contains(
            Convert("""
                #declare P_Band = pigment {
                    granite
                    color_map {
                        [0.0, 0.5 color rgb <0, 0, 0> color rgb <0.5, 0.5, 0.5>]
                        [0.5, 1.0 color rgb <0.5, 0.5, 0.5> color rgb <1, 1, 1>]
                    }
                }
                """),
            "[0, [0, 0, 0], 0.5, [0.5, 0.5, 0.5], 1, [1, 1, 1]]");
    }

    [TestMethod]
    public void TestAMapThatOvershootsTheEndIsBroughtBackToIt()
    {
        // POV-Ray's stone textures all finish at 1.001, making sure the last band is not missed;
        // the ray tracer wants a place within the pattern's range.
        StringAssert.Contains(
            Convert("""
                #declare P_Over = pigment {
                    granite
                    color_map { [0.0 color rgb 0][1.001 color rgb 1] }
                }
                """),
            "[0, [0, 0, 0], 1, [1, 1, 1]]");
    }

    [TestMethod]
    public void TestLayeredTexturesAreWrittenTopDown()
    {
        // POV-Ray writes its layers from the bottom up and the ray tracer reads them from the top
        // down, so the one written last has to come first.
        string library = Convert("""
            #declare T_Layered =
                texture { pigment { color rgb <1, 0, 0> } }
                texture { pigment { color rgbt <0, 1, 0, 0.5> } }
            """);

        StringAssert.Contains(library, "pigment layer {");

        int top = library.IndexOf("[0, 1, 0, 0.5]", StringComparison.Ordinal);
        int bottom = library.IndexOf("[1, 0, 0]", StringComparison.Ordinal);

        Assert.IsTrue(top >= 0 && bottom >= 0, "both layers should be written");
        Assert.IsTrue(top < bottom, "the layer POV-Ray wrote last should be written first");
    }

    [TestMethod]
    public void TestAMaterialSupersedesTheTextureOfTheSameName()
    {
        // textures.inc declares both forms of each of its glasses: Glass3 is the texture, and
        // M_Glass3 is that same texture with an interior around it.  Both would arrive here as
        // Glass3Material, leaving the file declaring it twice with the second quietly winning.
        string library = Convert("""
            #declare Glass3 = texture { pigment { color rgbf <1, 1, 1, 0.9> } }
            #declare I_Glass3 = interior { ior 1.5 }
            #declare M_Glass3 = material { texture { Glass3 } interior { I_Glass3 } }
            """, out List<PovIssue> issues);

        Assert.AreEqual(1, CountOf(library, "Glass3Material = material"),
            "Glass3Material should be declared exactly once");

        // The one kept is the material, so it brings the interior with it.
        StringAssert.Contains(library, "interior {");

        Assert.IsTrue(issues.Any(issue => issue.Name == "Glass3"),
            "dropping the texture form should be reported");
    }

    /// <summary>
    /// Counts how many times a line starting with the given text appears.
    /// </summary>
    private static int CountOf(string library, string start) => library
        .Split('\n')
        .Count(line => line.TrimEnd().StartsWith(start, StringComparison.Ordinal));

    [TestMethod]
    public void TestAColorMapOnItsOwnIsLeftOutAndExplained()
    {
        // POV-Ray declares maps apart from the patterns they colour; there is no such thing here.
        string library = Convert(
            "#declare M_Wood = color_map { [0.0 color rgb 0][1.0 color rgb 1] }",
            out List<PovIssue> issues);

        Assert.IsFalse(library.Contains("WoodColor ="), "a map should not be declared on its own");

        PovIssue issue = issues.Single();

        Assert.AreEqual("M_Wood", issue.Name);
        StringAssert.Contains(issue.Reason, "inside the textures that use it");
    }

    [TestMethod]
    public void TestAPatternLeftToBeGivenItsColorsLaterCannotStandAlone()
    {
        // POV-Ray's woodgrain pigments are written this way on purpose, to be paired with a map.
        string library = Convert(
            "#declare P_Grain = pigment { wood turbulence 0.04 scale <0.05, 0.05, 1> }",
            out List<PovIssue> issues);

        Assert.IsFalse(library.Contains("GrainPigment"));
        StringAssert.Contains(issues.Single().Reason, "at least two colors");
    }

    [TestMethod]
    public void TestAPigmentAndAMapArePutBackTogether()
    {
        // ...and this is why leaving them out costs nothing: the texture that pairs them still
        // comes across whole.
        StringAssert.Contains(
            Convert("""
                #declare P_Grain = pigment { wood turbulence 0.04 scale <0.05, 0.05, 1> }
                #declare M_Grain = color_map { [0.0 color rgb 0][1.0 color rgb 1] }
                #declare T_Wood = texture { pigment { P_Grain color_map { M_Grain } } }
                """),
            "WoodMaterial = material {");
    }

    [TestMethod]
    public void TestAPatternWeCannotExpressCostsOnlyItsOwnTexture()
    {
        string library = Convert("""
            #declare T_Before = texture { pigment { color rgb 1 } }
            #declare T_Odd = texture { pigment { onion color_map { [0.0 color rgb 0][1.0 color rgb 1] } } }
            #declare T_After = texture { pigment { color rgb 0 } }
            """, out List<PovIssue> issues);

        StringAssert.Contains(library, "BeforeMaterial");
        StringAssert.Contains(library, "AfterMaterial");
        Assert.IsFalse(library.Contains("OddMaterial"));
        Assert.AreEqual("T_Odd", issues.Single().Name);
    }

    [TestMethod]
    public void TestADeclarationThatFailsLeavesNoHalfWrittenBlock()
    {
        // This is the one that would break the whole library rather than costing one texture, so
        // a declaration is written somewhere of its own and kept only once it has worked.
        string library = Convert("""
            #declare T_Odd = texture { pigment { onion color_map { [0.0 color rgb 0][1.0 color rgb 1] } } }
            """);

        Assert.AreEqual(
            library.Count(character => character == '{'),
            library.Count(character => character == '}'),
            "the braces in the generated library do not balance");
        Assert.IsFalse(library.Contains("material {"), "a failed texture left something behind");
    }

    [TestMethod]
    public void TestThePovNameIsWrittenAboveEachDeclaration()
    {
        // Anyone reaching for these libraries is reading POV-Ray's documentation, where every one
        // of them is called something else.
        StringAssert.Contains(
            Convert("#declare T_Ruby = texture { pigment { color rgb <0.9, 0.1, 0.2> } }"),
            "// T_Ruby");
    }

    [TestMethod]
    public void TestTheVersionGuardIsNotCarriedIntoTheLibrary()
    {
        Assert.IsFalse(
            Convert("""
                #ifndef(Test_Inc_Temp)
                #declare Test_Inc_Temp = version;
                #declare Kept = 1;
                #end
                """).Contains("IncTemp"),
            "the version guard is bookkeeping, not something a scene would want");
    }

    [TestMethod]
    public void TestOnlyTheLastOfARepeatedDeclarationIsWritten()
    {
        // golds.inc works out five diffuse values and then declares each of them again as the
        // greater of itself and zero.  Writing both would put a line in that is wrong and then
        // correct it on the next.
        string library = Convert("""
            #declare D = -0.5;
            #declare D = max(D, 0);
            """);

        StringAssert.Contains(library, "D = 0");
        Assert.IsFalse(library.Contains("D = -0.5"), "the superseded declaration was written");
    }

    [TestMethod]
    public void TestADependencyIsWrittenAsAnInclude()
    {
        File.WriteAllText(
            Path.Combine(_directory, "base.inc"),
            "#declare T_Base = texture { pigment { color rgb 1 } }");
        File.WriteAllText(
            Path.Combine(_directory, "uses.inc"),
            "#declare T_Derived = texture { T_Base finish { specular 1 } }");

        PovSourceReader reader = new PovSourceReader(_directory, [], ["base.inc", "uses.inc"]);
        List<PovFile> files = reader.ReadAll();
        List<PovIssue> issues = reader.Issues.ToList();
        PovEmitter emitter = new PovEmitter(files, issues);

        StringAssert.Contains(
            emitter.Emit(files.Single(file => file.Name == "uses.inc")),
            "include 'base.igl'");
    }

    [TestMethod]
    public void TestWhatWasWrittenIsReportedWithItsKind()
    {
        File.WriteAllText(Path.Combine(_directory, "test.inc"), """
            #declare Col_Red = color rgb <1, 0, 0>;
            #declare T_Thing = texture { pigment { color rgb 1 } }
            #declare I_Stuff = interior { ior 1.5 }
            """);

        PovSourceReader reader = new PovSourceReader(_directory, [], ["test.inc"]);
        List<PovFile> files = reader.ReadAll();
        PovEmitter emitter = new PovEmitter(files, reader.Issues.ToList());

        _ = emitter.Emit(files.Single());

        Assert.AreEqual("color", emitter.Emitted.Single(name => name.Name == "RedColor").Kind);
        Assert.AreEqual("material", emitter.Emitted.Single(name => name.Name == "ThingMaterial").Kind);
        Assert.AreEqual("interior", emitter.Emitted.Single(name => name.Name == "StuffInterior").Kind);
        Assert.IsTrue(emitter.Emitted.All(name => name.Library == "test"));
    }
}
