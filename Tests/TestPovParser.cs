using RayTracer.PovRay;

namespace Tests;

/// <summary>
/// These tests cover reading POV-Ray's scene description language into the declarations the
/// emitter works from.
/// <para>
/// Only as much of the language as the texture library files need is read, so a good half of what
/// is covered here is stepping over what we cannot handle without losing what we can: a macro in
/// the middle of a file must not cost us the declarations on either side of it.
/// </para>
/// </summary>
[TestClass]
public class TestPovParser
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"pov-parser-{Guid.NewGuid():N}");

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
    /// Reads the given text as a POV-Ray file and returns what it declares.
    /// </summary>
    private PovFile Read(string text, out List<PovIssue> issues)
    {
        Write("test.inc", text);

        PovSourceReader reader = new PovSourceReader(_directory, [], ["test.inc"]);
        List<PovFile> files = reader.ReadAll();

        issues = reader.Issues.ToList();

        return files.Single();
    }

    private PovFile Read(string text) => Read(text, out _);

    /// <summary>
    /// Returns the value the given file declares under the given name.
    /// </summary>
    private static PovValue ValueOf(PovFile file, string name) => file.Declarations
        .Last(declaration => declaration.Name == name)
        .Value;

    /// <summary>
    /// Asserts that a name was declared as a vector with the given slots.
    /// </summary>
    private static void AssertSlots(PovFile file, string name, params double[] expected)
    {
        PovVector vector = ValueOf(file, name) as PovVector;

        Assert.IsNotNull(vector, $"{name} is not a vector");

        for (int index = 0; index < expected.Length; index++)
        {
            Assert.AreEqual(expected[index], vector.Components[index], 1e-9,
                $"{name} slot {index}");
        }
    }

    [TestMethod]
    public void TestAPlainDeclarationIsRead()
    {
        PovFile file = Read("#declare Half = 0.5;");

        Assert.AreEqual(0.5, ((PovNumber) ValueOf(file, "Half")).Value, 1e-9);
    }

    [TestMethod]
    public void TestVectorArithmeticIsWorkedOut()
    {
        // golds.inc derives every colour it has this way, so this is the arithmetic that matters.
        PovFile file = Read("""
            #declare Base = <1.0, 0.8, 0.6>;
            #declare Shifted = Base - <0.0, 0.2, 0.4>;
            #declare Scaled = Base * 0.5 + <0.25, 0.25, 0.25>;
            """);

        AssertSlots(file, "Shifted", 1.0, 0.6, 0.2);
        AssertSlots(file, "Scaled", 0.75, 0.65, 0.55);
    }

    [TestMethod]
    public void TestAComponentMayBePickedOutByName()
    {
        PovFile file = Read("""
            #declare C = <0.3, 0.6, 0.9>;
            #declare Average = (C.red + C.green + C.blue) / 3;
            """);

        Assert.AreEqual(0.6, ((PovNumber) ValueOf(file, "Average")).Value, 1e-9);
    }

    [TestMethod]
    public void TestFunctionsAreWorkedOut()
    {
        PovFile file = Read("""
            #declare Low = max(-0.5, 0);
            #declare Pair = min(3, 7, 5);
            #declare Root = sqrt(16);
            """);

        Assert.AreEqual(0, ((PovNumber) ValueOf(file, "Low")).Value, 1e-9);
        Assert.AreEqual(3, ((PovNumber) ValueOf(file, "Pair")).Value, 1e-9);
        Assert.AreEqual(4, ((PovNumber) ValueOf(file, "Root")).Value, 1e-9);
    }

    [TestMethod]
    public void TestFilterAndTransmitGoInDifferentSlots()
    {
        // The whole point of keeping the colour keywords apart: "rgbf" and "rgbt" are written the
        // same way and mean different things.
        PovFile file = Read("""
            #declare Filtered = color rgbf <0.1, 0.2, 0.3, 0.4>;
            #declare Transmitted = color rgbt <0.1, 0.2, 0.3, 0.4>;
            #declare Both = color rgbft <0.1, 0.2, 0.3, 0.4, 0.5>;
            """);

        AssertSlots(file, "Filtered", 0.1, 0.2, 0.3, 0.4, 0);
        AssertSlots(file, "Transmitted", 0.1, 0.2, 0.3, 0, 0.4);
        AssertSlots(file, "Both", 0.1, 0.2, 0.3, 0.4, 0.5);
    }

    [TestMethod]
    public void TestAColorMayNameItsSlotsOneAtATime()
    {
        PovFile file = Read("#declare G = color red 0.26 green 0.41 blue 0.31;");

        AssertSlots(file, "G", 0.26, 0.41, 0.31, 0, 0);
    }

    [TestMethod]
    public void TestSlotsMayBeLaidOverAColorAlreadyGiven()
    {
        // stones1.inc writes "color White transmit 0.3" throughout.
        PovFile file = Read("""
            #declare White = rgb 1;
            #declare Ghost = color White transmit 0.3;
            """);

        AssertSlots(file, "Ghost", 1, 1, 1, 0, 0.3);
    }

    [TestMethod]
    public void TestAVectorNeedNotHaveCommasInIt()
    {
        // woods.inc writes "< -1 0 0 >", which POV-Ray allows.
        PovFile file = Read("#declare V = < -1 0 0 >;");

        AssertSlots(file, "V", -1, 0, 0);
    }

    [TestMethod]
    public void TestCommentsAreIgnored()
    {
        PovFile file = Read("""
            // a line comment
            #declare A = 1; /* a block
                               comment spanning lines */
            #declare B = 2;
            """);

        Assert.AreEqual(1, ((PovNumber) ValueOf(file, "A")).Value, 1e-9);
        Assert.AreEqual(2, ((PovNumber) ValueOf(file, "B")).Value, 1e-9);
    }

    [TestMethod]
    public void TestTheVersionGuardDoesNotSwallowTheFile()
    {
        // Every one of POV-Ray's library files is wrapped in exactly this.
        PovFile file = Read("""
            #ifndef(Test_Inc_Temp)
            #declare Test_Inc_Temp = version;
            #version 3.5;

            #declare Inside = 7;

            #version Test_Inc_Temp;
            #end
            """);

        Assert.AreEqual(7, ((PovNumber) ValueOf(file, "Inside")).Value, 1e-9);
    }

    [TestMethod]
    public void TestABranchThatWasNotTakenIsSkipped()
    {
        PovFile file = Read("""
            #ifdef(NeverDeclared)
            #declare Unwanted = 1;
            #else
            #declare Wanted = 2;
            #end
            """);

        Assert.IsFalse(file.Declarations.Any(declaration => declaration.Name == "Unwanted"),
            "a branch that was not taken was read anyway");
        Assert.AreEqual(2, ((PovNumber) ValueOf(file, "Wanted")).Value, 1e-9);
    }

    [TestMethod]
    public void TestADeprecatedDeclarationIsStillRead()
    {
        // glass_old.inc marks every texture in it this way.
        PovFile file = Read("""
            #declare deprecated once "please use something else" Old = 3;
            """);

        Assert.AreEqual(3, ((PovNumber) ValueOf(file, "Old")).Value, 1e-9);
    }

    [TestMethod]
    public void TestAMacroThatStandsForAValueIsUsable()
    {
        // POV-Ray uses a macro where another language would use a named function, and ior.inc
        // leans on one for every index of refraction it declares:
        //
        //     #macro Ior(data) (data.y) #end
        //
        // Without this the most useful half of that file does not come across at all.
        PovFile file = Read("""
            #macro Ior(data) (data.y) #end
            #local iorData = <1.53024, 1.51673, 1.51432>;
            #declare iorCrownGlass = Ior(iorData);
            """);

        Assert.AreEqual(1.51673, ((PovNumber) ValueOf(file, "iorCrownGlass")).Value, 1e-9);
    }

    [TestMethod]
    public void TestAMacroThatTakesSeveralThingsIsUsable()
    {
        PovFile file = Read("""
            #macro Mix(a, b) ((a + b) / 2) #end
            #declare Middle = Mix(1, 4);
            """);

        Assert.AreEqual(2.5, ((PovNumber) ValueOf(file, "Middle")).Value, 1e-9);
    }

    [TestMethod]
    public void TestWhatAMacroTakesGoesOutOfScopeAgain()
    {
        // The names a macro takes are put in scope only for as long as it takes to read its body,
        // and whatever they meant before is put back.
        PovFile file = Read("""
            #declare data = 5;
            #macro Twice(data) (data * 2) #end
            #declare Used = Twice(3);
            #declare After = data;
            """);

        Assert.AreEqual(6, ((PovNumber) ValueOf(file, "Used")).Value, 1e-9);
        Assert.AreEqual(5, ((PovNumber) ValueOf(file, "After")).Value, 1e-9);
    }

    [TestMethod]
    public void TestAMacroThatDoesMoreThanWorkOutAValueFailsWhereItIsCalled()
    {
        // Which sort of macro it is cannot be told from the definition, so the body is kept and
        // the question is settled at the call.  Only the declaration doing the calling is lost.
        PovFile file = Read("""
            #declare Before = 1;
            #macro Unreadable(A, B)
                #declare Hidden = A + B;
            #end
            #declare Broken = Unreadable(1, 2);
            #declare After = 2;
            """, out List<PovIssue> issues);

        Assert.AreEqual(1, ((PovNumber) ValueOf(file, "Before")).Value, 1e-9);
        Assert.AreEqual(2, ((PovNumber) ValueOf(file, "After")).Value, 1e-9);
        Assert.IsFalse(file.Declarations.Any(declaration => declaration.Name == "Broken"));

        PovIssue issue = issues.Single();

        Assert.AreEqual("Broken", issue.Name);
        StringAssert.Contains(issue.Reason, "Unreadable");
    }

    [TestMethod]
    public void TestAMacroThatIsNeverCalledCostsNothing()
    {
        PovFile file = Read("""
            #macro Unreadable(A)
                #declare Hidden = A;
            #end
            #declare After = 2;
            """, out List<PovIssue> issues);

        Assert.AreEqual(2, ((PovNumber) ValueOf(file, "After")).Value, 1e-9);
        Assert.AreEqual(0, issues.Count, "a macro nobody calls is nobody's problem");
    }

    [TestMethod]
    public void TestSomethingUnreadableCostsOnlyItsOwnDeclaration()
    {
        PovFile file = Read("""
            #declare Before = 1;
            #declare Broken = NothingDeclaresThis * 2;
            #declare After = 2;
            """, out List<PovIssue> issues);

        Assert.AreEqual(1, ((PovNumber) ValueOf(file, "Before")).Value, 1e-9);
        Assert.AreEqual(2, ((PovNumber) ValueOf(file, "After")).Value, 1e-9);
        Assert.AreEqual("Broken", issues.Single().Name);
    }

    [TestMethod]
    public void TestABlockIsKeptAsItWasWritten()
    {
        PovFile file = Read("""
            #declare T = texture {
                pigment { granite turbulence 0.4 color_map { [0.0 color rgb 0][1.0 color rgb 1] } }
                finish { specular 0.5 roughness 0.15 }
            }
            """);

        PovBlock block = ValueOf(file, "T") as PovBlock;

        Assert.IsNotNull(block);
        Assert.AreEqual("texture", block.Kind);
        Assert.AreEqual(2, block.Items.OfType<PovBlock>().Count());
    }

    [TestMethod]
    public void TestTexturesWrittenOneAfterAnotherBecomeLayers()
    {
        PovFile file = Read("""
            #declare Layered =
                texture { pigment { color rgb 1 } }
                texture { pigment { color rgbt <1, 1, 1, 0.5> } }
            """);

        PovBlockSequence sequence = ValueOf(file, "Layered") as PovBlockSequence;

        Assert.IsNotNull(sequence, "the layers were not gathered up");
        Assert.AreEqual(2, sequence.Blocks.Count);
    }

    [TestMethod]
    public void TestAnIncludedFileThatIsNotALibraryIsFoldedIn()
    {
        Write("shared.inc", "#declare Shared = 5;");

        PovFile file = Read("""
            #include "shared.inc"
            #declare Uses = Shared * 2;
            """);

        // "shared.inc" is becoming no library of its own, so a scene would have nowhere to import
        // its declarations from.  They therefore come along in the file that included it.
        Assert.IsTrue(file.Declarations.Any(declaration => declaration.Name == "Shared"),
            "the included declarations were not folded in");
        Assert.AreEqual(10, ((PovNumber) ValueOf(file, "Uses")).Value, 1e-9);
        Assert.AreEqual(0, file.Includes.Count);
    }

    [TestMethod]
    public void TestLeaningOnAnotherLibraryIsRecordedAsADependency()
    {
        Write("base.inc", "#declare BaseTexture = texture { pigment { color rgb 1 } }");
        Write("uses.inc", """
            #declare Derived = texture { BaseTexture finish { specular 1 } }
            """);

        PovSourceReader reader = new PovSourceReader(_directory, [], ["base.inc", "uses.inc"]);
        List<PovFile> files = reader.ReadAll();
        PovFile uses = files.Single(file => file.Name == "uses.inc");

        CollectionAssert.AreEqual((List<string>) ["base.inc"], uses.Includes);
        Assert.IsFalse(uses.Declarations.Any(declaration => declaration.Name == "BaseTexture"),
            "a library that is imported from should not be copied as well");
    }

    [TestMethod]
    public void TestADependencyIsFoundEvenWhereNothingIncludesIt()
    {
        // stones1.inc uses "White" and "Mica" while including nothing at all, on the
        // understanding that a scene will have included colors.inc first.  Noting where each name
        // came from as it is used is what finds that.
        Write("base.inc", "#declare BaseTexture = texture { pigment { color rgb 1 } }");
        Write("uses.inc", "#declare Derived = texture { BaseTexture }");

        PovSourceReader reader = new PovSourceReader(_directory, [], ["base.inc", "uses.inc"]);
        PovFile uses = reader.ReadAll().Single(file => file.Name == "uses.inc");

        CollectionAssert.AreEqual((List<string>) ["base.inc"], uses.Includes);
    }

    [TestMethod]
    public void TestAPreludeIsReadForItsNamesAndNothingElse()
    {
        // consts.inc is read this way: glass.inc needs its names in scope, but it declares things
        // called "E" and "O", which would be very poor names to hand a scene.
        Write("prelude.inc", "#declare E = 2.718;");
        Write("test.inc", "#declare Doubled = E * 2;");

        PovSourceReader reader = new PovSourceReader(_directory, ["prelude.inc"], ["test.inc"]);
        List<PovFile> files = reader.ReadAll();

        Assert.AreEqual(1, files.Count, "the prelude should not become a library");

        PovFile file = files.Single();

        Assert.AreEqual(0, file.Includes.Count, "a prelude is not a dependency");
        Assert.IsFalse(file.Declarations.Any(declaration => declaration.Name == "E"),
            "the prelude's own names should not be carried");
        Assert.AreEqual(5.436, ((PovNumber) ValueOf(file, "Doubled")).Value, 1e-9);
    }
}
