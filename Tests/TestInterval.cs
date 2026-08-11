using RayTracer.Basics;

namespace Tests;

[TestClass]
public class TestInterval
{
    [TestMethod]
    public void TestClosedInterval()
    {
        Interval interval = new Interval
        {
            Start = 1,
            End = 5
        };
        
        interval.Reset(1);
        
        Assert.AreEqual(1, interval.Next());
        Assert.AreEqual(2, interval.Next());
        Assert.AreEqual(3, interval.Next());
        Assert.AreEqual(4, interval.Next());
        Assert.AreEqual(5, interval.Next());
        Assert.IsTrue(interval.IsAtEnd);

        interval = new Interval
        {
            Start = 5,
            End = 1
        };
        
        interval.Reset(-1);

        Assert.AreEqual(5, interval.Next());
        Assert.AreEqual(4, interval.Next());
        Assert.AreEqual(3, interval.Next());
        Assert.AreEqual(2, interval.Next());
        Assert.AreEqual(1, interval.Next());
        Assert.IsTrue(interval.IsAtEnd);
    }

    [TestMethod]
    public void TestClosedStartOpenEndInterval()
    {
        Interval interval = new Interval
        {
            Start = 1,
            End = 5,
            IsEndOpen = true
        };
        
        interval.Reset(1);
        
        Assert.AreEqual(1, interval.Next());
        Assert.AreEqual(2, interval.Next());
        Assert.AreEqual(3, interval.Next());
        Assert.AreEqual(4, interval.Next());
        Assert.IsTrue(interval.IsAtEnd);

        interval = new Interval
        {
            Start = 5,
            End = 1,
            IsEndOpen = true
        };
        
        interval.Reset(-1);

        Assert.AreEqual(5, interval.Next());
        Assert.AreEqual(4, interval.Next());
        Assert.AreEqual(3, interval.Next());
        Assert.AreEqual(2, interval.Next());
        Assert.IsTrue(interval.IsAtEnd);
    }

    [TestMethod]
    public void TestOpenStartClosedEndInterval()
    {
        Interval interval = new Interval
        {
            Start = 1,
            End = 5,
            IsStartOpen = true
        };
        
        interval.Reset(1);
        
        Assert.AreEqual(2, interval.Next());
        Assert.AreEqual(3, interval.Next());
        Assert.AreEqual(4, interval.Next());
        Assert.AreEqual(5, interval.Next());
        Assert.IsTrue(interval.IsAtEnd);

        interval = new Interval
        {
            Start = 5,
            End = 1,
            IsStartOpen = true
        };
        
        interval.Reset(-1);

        Assert.AreEqual(4, interval.Next());
        Assert.AreEqual(3, interval.Next());
        Assert.AreEqual(2, interval.Next());
        Assert.AreEqual(1, interval.Next());
        Assert.IsTrue(interval.IsAtEnd);
    }

    [TestMethod]
    public void TestOpenStartOpenEndInterval()
    {
        Interval interval = new Interval
        {
            Start = 1,
            End = 5,
            IsStartOpen = true,
            IsEndOpen = true
        };
        
        interval.Reset(1);
        
        Assert.AreEqual(2, interval.Next());
        Assert.AreEqual(3, interval.Next());
        Assert.AreEqual(4, interval.Next());
        Assert.IsTrue(interval.IsAtEnd);

        interval = new Interval
        {
            Start = 5,
            End = 1,
            IsStartOpen = true,
            IsEndOpen = true
        };
        
        interval.Reset(-1);

        Assert.AreEqual(4, interval.Next());
        Assert.AreEqual(3, interval.Next());
        Assert.AreEqual(2, interval.Next());
        Assert.IsTrue(interval.IsAtEnd);
    }

    /// <summary>
    /// This walks an interval to its end and hands back what it produced, refusing to walk forever.
    /// </summary>
    private static List<double> Walk(Interval interval)
    {
        List<double> produced = [];

        while (!interval.IsAtEnd)
        {
            produced.Add(interval.Next());

            Assert.IsTrue(produced.Count < 1000, "this interval does not know how to stop");
        }

        return produced;
    }

    [TestMethod]
    public void TestAnEndTheStepCannotLandOnStillStopsTheCount()
    {
        // The one that was wrong, and wrong in the worst way an interval can be: it asked whether the
        // count had *arrived* at the end, and a count only ever arrives when the end is a whole number
        // of steps from the start.  Nothing makes anybody write one that is, and a range like this one
        // ran forever rather than stopping short of its end.
        CollectionAssert.AreEqual(new List<double> { 0, 1, 2, 3 },
            Walk(new Interval { Start = 0, End = 3.4 }.Reset(1)));

        CollectionAssert.AreEqual(new List<double> { 0, 0.3, 0.6, 0.9 },
            Walk(new Interval { Start = 0, End = 1 }.Reset(0.3))
                .Select(value => Math.Round(value, 10)).ToList());

        // Going the other way is the same rule read backwards.
        CollectionAssert.AreEqual(new List<double> { 5, 4, 3 },
            Walk(new Interval { Start = 5, End = 2.6 }.Reset(-1)));
    }

    [TestMethod]
    public void TestAnEndTheStepDoesLandOnIsStillTaken()
    {
        // The other half, and the reason the check is against Near rather than a plain comparison: a
        // quarter step reaches one as 0.99999999, and a range written to take its end must take it.
        CollectionAssert.AreEqual(new List<double> { 0, 0.25, 0.5, 0.75, 1 },
            Walk(new Interval { Start = 0, End = 1 }.Reset(0.25))
                .Select(value => Math.Round(value, 10)).ToList());

        Assert.AreEqual(5, Walk(new Interval { Start = 1, End = 5 }.Reset(1)).Count);
        Assert.AreEqual(4, Walk(new Interval
        {
            Start = 1, End = 5, IsEndOpen = true
        }.Reset(1)).Count);
    }

}
