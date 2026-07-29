using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the check that decides whether a file may be imported as a library: a
/// library must hold only definitions, since anything else would be dragged into every scene
/// that imported it.  The check drives <c>libraries --import</c> (without <c>--povray</c>).
/// </summary>
[TestClass]
public class TestLibraryImport
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"library-import-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    private ImageRenderer Parse(string text)
    {
        string path = Path.Combine(_directory, "library.igl");

        File.WriteAllText(path, text);

        return new LanguageParser(path).Parse();
    }

    [TestMethod]
    public void TestAFileOfDefinitionsIsAcceptableAsALibrary()
    {
        ImageRenderer renderer = Parse(
            "Copper = material { pigment color [0.72, 0.45, 0.2]  specular 0.8  shininess 120 }\n" +
            "Jade = color [0.2, 0.6, 0.45]\n" +
            "Sheen = 0.9\n");

        Assert.IsTrue(renderer.HoldsOnlyDefinitions);
        Assert.AreEqual(3, renderer.DefinitionCount);
    }

    [TestMethod]
    public void TestAFileWithASurfaceIsNotALibrary()
    {
        ImageRenderer renderer = Parse(
            "Copper = material { pigment color Red }\n" +
            "sphere { material Copper }\n");

        Assert.IsFalse(renderer.HoldsOnlyDefinitions,
            "a surface makes the file more than a library");
    }

    [TestMethod]
    public void TestAFileWithACameraOrRenderIsNotALibrary()
    {
        Assert.IsFalse(Parse(
            "Copper = material { pigment color Red }\n" +
            "camera { location [0, 0, -5]  look at [0, 0, 0] }\n").HoldsOnlyDefinitions);

        Assert.IsFalse(Parse(
            "Copper = material { pigment color Red }\n" +
            "render\n").HoldsOnlyDefinitions);
    }

    [TestMethod]
    public void TestAnEmptyFileDefinesNothing()
    {
        ImageRenderer renderer = Parse("// nothing but a comment\n");

        Assert.IsTrue(renderer.HoldsOnlyDefinitions);
        Assert.AreEqual(0, renderer.DefinitionCount,
            "an empty file holds no definitions, so there would be nothing to import");
    }

    [TestMethod]
    public void TestALibraryConvertedFromPovRayIsAcceptable()
    {
        // The real shape of the thing this feature copies: the converted POV-Ray libraries are
        // exactly named definitions, so one must read back as a library.
        string library = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".rayTracer", "Libraries", "golds.igl");

        if (!File.Exists(library))
            Assert.Inconclusive("the POV-Ray libraries are not installed on this machine");

        ImageRenderer renderer = new LanguageParser(library).Parse();

        Assert.IsTrue(renderer.HoldsOnlyDefinitions);
        Assert.IsTrue(renderer.DefinitionCount > 0);
    }
}
