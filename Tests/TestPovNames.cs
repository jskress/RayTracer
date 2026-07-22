using RayTracer.PovRay;

namespace Tests;

/// <summary>
/// These tests cover the way a POV-Ray name is turned into one written the way the ray tracer's
/// own libraries are.  POV-Ray marks what a thing is with a one-letter prefix and we say it in a
/// word at the end, so <c>T_Ruby</c> becomes <c>RubyMaterial</c>.
/// </summary>
[TestClass]
public class TestPovNames
{
    [TestMethod]
    public void TestEachPrefixIsTakenOff()
    {
        Assert.AreEqual("RubyMaterial", PovNames.From("T_Ruby", "Material"));
        Assert.AreEqual("Glass3Material", PovNames.From("M_Glass3", "Material"));
        Assert.AreEqual("Glass1Finish", PovNames.From("F_Glass1", "Finish"));
        Assert.AreEqual("Chrome1Pigment", PovNames.From("P_Chrome1", "Pigment"));
        Assert.AreEqual("Glass1Interior", PovNames.From("I_Glass1", "Interior"));
    }

    [TestMethod]
    public void TestUnderscoresWithinANameAreClosedUp()
    {
        Assert.AreEqual("GlassRubyColor", PovNames.From("Col_Glass_Ruby", "Color"));
        Assert.AreEqual("Chrome3AMaterial", PovNames.From("T_Chrome_3A", "Material"));
    }

    [TestMethod]
    public void TestTheCaseWithinAWordIsLeftAlone()
    {
        // "WoodGrain1A" is easier to read than "Woodgrain1a" would be, and POV-Ray already chose.
        Assert.AreEqual("WoodGrain1APigment", PovNames.From("P_WoodGrain1A", "Pigment"));
    }

    [TestMethod]
    public void TestANameWithNoPrefixIsLeftAsItIs()
    {
        Assert.AreEqual("DullFinish", PovNames.From("Dull", "Finish"));
        Assert.AreEqual("Crack1Material", PovNames.From("Crack1", "Material"));
    }

    [TestMethod]
    public void TestANameMayBeGivenNoSuffix()
    {
        // Numbers and vectors get none; there is no word for what they are that says anything.
        Assert.AreEqual("GoldBase", PovNames.From("GoldBase", null));
        Assert.AreEqual("CVect1", PovNames.From("CVect1", null));
    }

    [TestMethod]
    public void TestTakingThePrefixOffNeverLeavesSomethingUnusable()
    {
        // Taking "P_" off "P_1" would leave "1", which is not a name.  Rather than produce
        // something the ray tracer cannot read, the prefix is kept.
        Assert.AreEqual("P1Pigment", PovNames.From("P_1", "Pigment"));

        // And a name that is nothing but a prefix would leave nothing at all.
        Assert.AreEqual("TMaterial", PovNames.From("T_", "Material"));
    }

    [TestMethod]
    public void TestOnlyTheFirstPrefixIsTakenOff()
    {
        // "T_P_Thing" is not a real POV-Ray name, but stripping repeatedly would be a surprising
        // rule to have, and one nothing asks for.
        Assert.AreEqual("PThingMaterial", PovNames.From("T_P_Thing", "Material"));
    }
}
