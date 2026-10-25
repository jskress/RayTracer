using RayTracer.Commands;
using RayTracer.Fonts;

namespace Tests;

/// <summary>
/// These tests cover how the fonts command names a face: a family, then a weight and whether it is italic, each part
/// optional, as the fonts guide writes them.
/// </summary>
[TestClass]
public class TestFaceSpecs
{
    private static (string Family, int Weight, bool Italic) Parsed(string spec)
    {
        FaceIdentifier id = FontsCommand.ParseFaceSpec(spec);

        return (id.FamilyName, id.Weight, id.Italic);
    }

    [TestMethod]
    public void TestEveryDocumentedFormIsRead()
    {
        Assert.AreEqual(("Merriweather", 400, false), Parsed("Merriweather"));
        Assert.AreEqual(("Merriweather", 700, false), Parsed("Merriweather:Bold"));
        Assert.AreEqual(("Merriweather", 700, true), Parsed("Merriweather:Bold:italic"));
        Assert.AreEqual(("Merriweather", 400, true), Parsed("Merriweather::italic"));
        Assert.AreEqual(("Merriweather", 700, true), Parsed("Merriweather:700:i"));
    }

    [TestMethod]
    public void TestAWeightIsNamedInAnyCase()
    {
        Assert.AreEqual(("Merriweather", 900, false), Parsed("Merriweather:black"));
        Assert.AreEqual(("Merriweather", 300, true), Parsed("Merriweather:LIGHT:Italic"));
    }

    [TestMethod]
    public void TestOnlyTheStandardWeightNumbersAreTaken()
    {
        foreach (string wrong in new[] { "Merriweather:12345", "Merriweather:450", "Merriweather:Heavy",
                     "Merriweather:Bold:oblique" })
        {
            Assert.ThrowsExactly<ArgumentException>(() => FontsCommand.ParseFaceSpec(wrong), wrong);
        }
    }
}
