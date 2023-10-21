using RayTracer.Basics;

namespace Tests;

[TestClass]
public class TestPoints
{
    [TestMethod]
    public void TestConstruction()
    {
        Point point = new Point(4.3, -4.2, 3.1);

        Assert.AreEqual(4.3, point.X);
        Assert.AreEqual(-4.2, point.Y);
        Assert.AreEqual(3.1, point.Z);
        Assert.AreEqual(1, point.W);
    }

    [TestMethod]
    public void TestAddition()
    {
        Point point = new (3, -2, 5);
        Vector vector = new (-2, 3, 1);
        Point expected = new (1, 1, 6);

        Point result = point + vector;

        Assert.IsTrue(expected.Matches(result));

        result = vector + point;

        Assert.IsTrue(expected.Matches(result));
    }

    [TestMethod]
    public void TestSubtraction()
    {
        Point point1 = new (3, 2, 1);
        Point point2 = new (5, 6, 7);
        Vector vector = new (-2, -4, -6);

        Vector result = point1 - point2;

        Assert.IsTrue(vector.Matches(result));

        vector = new Vector(5, 6, 7);

        Point point = point1 - vector;
        Point expected = new (-2, -4, -6);

        Assert.IsTrue(expected.Matches(point));
    }
}
