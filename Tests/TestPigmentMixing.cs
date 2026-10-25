using RayTracer.Basics;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// These tests hold the pigments that wrap or mix others to what they say: a mottled pigment reads the pigment it
/// mottles through that pigment's own transforms, and keeps how clear it is; a mixture mixes coverage as it mixes
/// color.
/// </summary>
[TestClass]
public class TestPigmentMixing
{
    /// <summary>
    /// A mottled pigment reads the pigment it wraps where that pigment's own transforms put it: one moved along X
    /// is mottled moved, as a blend or a layer would read it.
    /// </summary>
    [TestMethod]
    public void TestMottlingReadsThePigmentThroughItsTransforms()
    {
        TestPigment moved = new () { Transform = Transforms.Translate(1, 0, 0) };
        MottledPigment mottled = new () { Pigment = moved, Noise = new LayeredNoise() };
        Point point = new (0.2, 0.3, 0.1);
        Color seen = mottled.GetColorFor(point);

        // The wrapped pigment's color at its own place is (-0.8, 0.3, 0.1), and mottling only dims it.
        Assert.AreEqual(-0.8 / 0.3, seen.Red / seen.Green, 1e-9);
    }

    /// <summary>
    /// Mottling dims a color without making it opaque: a pane mottled lets through what it did.
    /// </summary>
    [TestMethod]
    public void TestMottlingKeepsHowClearAPigmentIs()
    {
        MottledPigment mottled = new ()
        {
            Pigment = new SolidPigment(new Color(0.5, 0.6, 0.7, 0.3)), Noise = new LayeredNoise()
        };

        Assert.AreEqual(0.3, mottled.GetColorFor(new Point(0.2, 0.3, 0.1)).Alpha, 1e-12);
    }

    /// <summary>
    /// A mixture of two pigments is part way between them in coverage as well as color: half a solid one and half a
    /// clear one is half clear.
    /// </summary>
    [TestMethod]
    public void TestAMixtureMixesCoverage()
    {
        MixedPigment mixed = new ()
        {
            From = new SolidPigment(new Color(1, 0, 0, 1)), To = new SolidPigment(new Color(0, 0, 1, 0)), Share = 0.5
        };
        Color seen = mixed.GetColorFor(new Point(0, 0, 0));

        Assert.AreEqual(0.5, seen.Alpha, 1e-12);
        Assert.AreEqual(0.5, seen.Red, 1e-12);
    }
}
