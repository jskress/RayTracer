using RayTracer.Basics;

namespace Tests;

[TestClass]
public class TestBreakValue
{
    [TestMethod]
    public void TestTheGoverningEntryIsTheLastOneReached()
    {
        BreakValue<string> values = new ();

        values.AddEntry("a", 0);
        values.AddEntry("b", 0.5);
        values.AddEntry("c", 1);

        Assert.AreEqual(0, values.GetIndexByValue(0));
        Assert.AreEqual(0, values.GetIndexByValue(0.25));
        Assert.AreEqual(1, values.GetIndexByValue(0.5));
        Assert.AreEqual(1, values.GetIndexByValue(0.75));
    }

    [TestMethod]
    public void TestRepeatedValuesResolveToTheirOwnEntry()
    {
        // Nothing stops one value being named at several break values -- a color map that
        // names the same pigment at every stop, say.  Entries used to be located by searching
        // for their value, which in that case found the first stop holding it no matter which
        // stop was actually meant, and so reported a break value from somewhere else entirely.
        // (Rendered, that showed up as black bands across an otherwise fine sphere.)
        BreakValue<string> values = new ();

        values.AddEntry("same", 0);
        values.AddEntry("same", 0.25);
        values.AddEntry("same", 0.5);
        values.AddEntry("same", 0.75);

        Assert.AreEqual(0, values.GetIndexByValue(0.1));
        Assert.AreEqual(1, values.GetIndexByValue(0.3));
        Assert.AreEqual(2, values.GetIndexByValue(0.6));
        Assert.AreEqual(3, values.GetIndexByValue(0.9));

        // ...and each index must report its own break value, not the first one's.
        Assert.AreEqual(0.0, values.GetByIndex(0).Item1);
        Assert.AreEqual(0.25, values.GetByIndex(1).Item1);
        Assert.AreEqual(0.5, values.GetByIndex(2).Item1);
        Assert.AreEqual(0.75, values.GetByIndex(3).Item1);
    }

    [TestMethod]
    public void TestAValueBelowEveryBreakUsesTheFirstEntry()
    {
        BreakValue<string> values = new ();

        values.AddEntry("a", 0.3);
        values.AddEntry("b", 0.7);

        Assert.AreEqual(0, values.GetIndexByValue(0.1));
    }

    [TestMethod]
    public void TestNoEntriesReportsNoEntry()
    {
        BreakValue<string> values = new ();

        Assert.AreEqual(-1, values.GetIndexByValue(0.5));
    }

    [TestMethod]
    public void TestEntriesStaySortedByBreakValue()
    {
        BreakValue<string> values = new ();

        values.AddEntry("c", 0.8);
        values.AddEntry("a", 0.1);
        values.AddEntry("b", 0.4);

        Assert.AreEqual("a", values.GetByIndex(0).Item2);
        Assert.AreEqual("b", values.GetByIndex(1).Item2);
        Assert.AreEqual("c", values.GetByIndex(2).Item2);
    }
}
