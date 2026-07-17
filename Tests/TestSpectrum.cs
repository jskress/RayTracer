using RayTracer.Basics;

namespace Tests;

[TestClass]
public class TestSpectrum
{
    [TestMethod]
    public void TestTheGoverningEntryIsTheLastOneReached()
    {
        Spectrum<string> spectrum = new ();

        spectrum.AddEntry("a", 0);
        spectrum.AddEntry("b", 0.5);
        spectrum.AddEntry("c", 1);

        Assert.AreEqual(0, spectrum.GetIndexByValue(0));
        Assert.AreEqual(0, spectrum.GetIndexByValue(0.25));
        Assert.AreEqual(1, spectrum.GetIndexByValue(0.5));
        Assert.AreEqual(1, spectrum.GetIndexByValue(0.75));
    }

    [TestMethod]
    public void TestRepeatedValuesResolveToTheirOwnEntry()
    {
        // Nothing stops one value being named at several break values -- a colour map that
        // names the same pigment at every stop, say.  Entries used to be located by searching
        // for their value, which in that case found the first stop holding it no matter which
        // stop was actually meant, and so reported a break value from somewhere else entirely.
        // (Rendered, that showed up as black bands across an otherwise fine sphere.)
        Spectrum<string> spectrum = new ();

        spectrum.AddEntry("same", 0);
        spectrum.AddEntry("same", 0.25);
        spectrum.AddEntry("same", 0.5);
        spectrum.AddEntry("same", 0.75);

        Assert.AreEqual(0, spectrum.GetIndexByValue(0.1));
        Assert.AreEqual(1, spectrum.GetIndexByValue(0.3));
        Assert.AreEqual(2, spectrum.GetIndexByValue(0.6));
        Assert.AreEqual(3, spectrum.GetIndexByValue(0.9));

        // ...and each index must report its own break value, not the first one's.
        Assert.AreEqual(0.0, spectrum.GetByIndex(0).Item1);
        Assert.AreEqual(0.25, spectrum.GetByIndex(1).Item1);
        Assert.AreEqual(0.5, spectrum.GetByIndex(2).Item1);
        Assert.AreEqual(0.75, spectrum.GetByIndex(3).Item1);
    }

    [TestMethod]
    public void TestAValueBelowEveryBreakUsesTheFirstEntry()
    {
        Spectrum<string> spectrum = new ();

        spectrum.AddEntry("a", 0.3);
        spectrum.AddEntry("b", 0.7);

        Assert.AreEqual(0, spectrum.GetIndexByValue(0.1));
    }

    [TestMethod]
    public void TestAnEmptySpectrumReportsNoEntry()
    {
        Spectrum<string> spectrum = new ();

        Assert.AreEqual(-1, spectrum.GetIndexByValue(0.5));
    }

    [TestMethod]
    public void TestEntriesStaySortedByBreakValue()
    {
        Spectrum<string> spectrum = new ();

        spectrum.AddEntry("c", 0.8);
        spectrum.AddEntry("a", 0.1);
        spectrum.AddEntry("b", 0.4);

        Assert.AreEqual("a", spectrum.GetByIndex(0).Item2);
        Assert.AreEqual("b", spectrum.GetByIndex(1).Item2);
        Assert.AreEqual("c", spectrum.GetByIndex(2).Item2);
    }
}
