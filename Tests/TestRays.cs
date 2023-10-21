using RayTracer.Basics;
using RayTracer.Core;

namespace Tests;

[TestClass]
public class TestRays
{
    [TestMethod]
    public void TestConstruction()
    {
        Point origin = new (1, 2, 3);
        Vector direction = new (4, 5, 6);
        Ray ray = new (origin, direction);

        Assert.AreSame(origin, ray.Origin);
        Assert.AreSame(direction, ray.Direction);
    }

    [TestMethod]
    public void TestAt()
    {
        Point origin = new (2, 3, 4);
        Vector direction = new (1, 0, 0);
        Ray ray = new (origin, direction);

        Assert.IsTrue(origin.Matches(ray.At(0)));
        Assert.IsTrue(new Point(3, 3, 4).Matches(ray.At(1)));
        Assert.IsTrue(new Point(1, 3, 4).Matches(ray.At(-1)));
        Assert.IsTrue(new Point(4.5, 3, 4).Matches(ray.At(2.5)));
    }
}
