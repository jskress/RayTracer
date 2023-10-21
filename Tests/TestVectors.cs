using RayTracer.Basics;

namespace Tests;

[TestClass]
public class TestVectors
{
    [TestMethod]
    public void TestConstruction()
    {
        Vector vector = new Vector(4.3, -4.2, 3.1);

        Assert.AreEqual(4.3, vector.X);
        Assert.AreEqual(-4.2, vector.Y);
        Assert.AreEqual(3.1, vector.Z);
        Assert.AreEqual(0, vector.W);
    }

    [TestMethod]
    public void TestAddition()
    {
        Vector vector1 = new (3, -2, 5);
        Vector vector2 = new (-2, 3, 1);
        Vector expected = new (1, 1, 6);

        Vector result = vector1 + vector2;

        Assert.IsTrue(expected.Matches(result));
    }

    [TestMethod]
    public void TestNegation()
    {
        Vector vector = new (1, -2, 3);
        Vector expected = new (-1, 2, -3);

        Vector result = -vector;

        Assert.IsTrue(expected.Matches(result));
    }

    [TestMethod]
    public void TestSubtraction()
    {
        Vector vector1 = new (3, 2, 1);
        Vector vector2 = new (5, 6, 7);
        Vector expected = new (-2, -4, -6);

        Vector result = vector1 - vector2;

        Assert.IsTrue(expected.Matches(result));
    }

    [TestMethod]
    public void TestScalarMultiplicationAndDivision()
    {
        Vector vector = new (1, -2, 3);
        Vector expected = new (3.5, -7, 10.5);
        Vector result = vector * 3.5;

        Assert.IsTrue(expected.Matches(result));

        result = 3.5 * vector;

        Assert.IsTrue(expected.Matches(result));

        expected = new Vector(0.5, -1, 1.5);
        result = vector * 0.5;

        Assert.IsTrue(expected.Matches(result));

        result = vector / 2;

        Assert.IsTrue(expected.Matches(result));
    }

    [TestMethod]
    public void TestMagnitude()
    {
        Vector vector = new (1, 0, 0);

        Assert.AreEqual(1, vector.Magnitude);

        vector = new Vector(0, 1, 0);

        Assert.AreEqual(1, vector.Magnitude);

        vector = new Vector(0, 0, 1);

        Assert.AreEqual(1, vector.Magnitude);

        vector = new Vector(1, 2, 3);

        Assert.AreEqual(Math.Sqrt(14), vector.Magnitude);

        vector = new Vector(-1, -2, -3);

        Assert.AreEqual(Math.Sqrt(14), vector.Magnitude);
    }

    [TestMethod]
    public void TestNormalization()
    {
        Vector vector = new (4, 0, 0);
        Vector expected = new (1, 0, 0);

        Assert.IsTrue(expected.Matches(vector.Unit));

        vector = new Vector(1, 2, 3);
        expected = new Vector(0.26726, 0.53452, 0.80178);

        Vector unit = vector.Unit;

        Assert.IsTrue(expected.Matches(unit));
        Assert.AreEqual(1, unit.Magnitude);
    }

    [TestMethod]
    public void TestDotProduct()
    {
        Vector vector1 = new (1, 2, 3);
        Vector vector2 = new (2, 3, 4);

        Assert.AreEqual(20, vector1.Dot(vector2));
    }

    [TestMethod]
    public void TestCrossProduct()
    {
        Vector vector1 = new (1, 2, 3);
        Vector vector2 = new (2, 3, 4);
        Vector cross1 = new Vector(-1, 2, -1);
        Vector cross2 = new Vector(1, -2, 1);

        Assert.IsTrue(cross1.Matches(vector1.Cross(vector2)));
        Assert.IsTrue(cross2.Matches(vector2.Cross(vector1)));
    }

    [TestMethod]
    public void TestReflection()
    {
        Vector vector = new (1, -1, 0);
        Vector normal = new (0, 1, 0);
        Vector expected = new (1, 1, 0);
        double value = Math.Sqrt(2) / 2;

        Assert.IsTrue(expected.Matches(vector.Reflect(normal)));

        vector = new Vector(0, -1, 0);
        normal = new Vector(value, value, 0);
        expected = new Vector(1, 0, 0);

        Assert.IsTrue(expected.Matches(vector.Reflect(normal)));
    }
}
