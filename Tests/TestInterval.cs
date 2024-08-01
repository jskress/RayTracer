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
}
