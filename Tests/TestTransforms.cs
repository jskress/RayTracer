using RayTracer.Basics;

namespace Tests;

[TestClass]
public class TestTransforms
{
    [TestMethod]
    public void TestTranslatePoints()
    {
        Matrix transform = Transforms.Translate(5, -3, 2);
        Matrix inverse = transform.Invert();
        Point point = new (-3, 4, 5);
        Point expected = new (2, 1, 7);
        Point actual = transform * point;

        Assert.IsTrue(expected.Matches(actual));

        expected = new Point(-8, 7, 3);
        actual = inverse * point;

        Assert.IsTrue(expected.Matches(actual));
    }

    [TestMethod]
    public void TestTranslateVectors()
    {
        Matrix transform = Transforms.Translate(5, -3, 2);
        // Matrix inverse = transform.Invert();
        Vector vector = new (-3, 4, 5);
        Vector actual = transform * vector;

        Assert.IsTrue(vector.Matches(actual));
    }

    [TestMethod]
    public void TestScalePoints()
    {
        Matrix transform = Transforms.Scale(2, 3, 4);
        Point point = new (-4, 6, 8);
        Point expected = new (-8, 18, 32);
        Point actual = transform * point;

        Assert.IsTrue(expected.Matches(actual));

        transform = Transforms.Scale(-1, 1, 1);
        point = new Point(2, 3, 4);
        expected = new Point(-2, 3, 4);
        actual = transform * point;

        Assert.IsTrue(expected.Matches(actual));
    }

    [TestMethod]
    public void TestScaleVectors()
    {
        Matrix transform = Transforms.Scale(2, 3, 4);
        Matrix inverse = transform.Invert();
        Vector vector = new (-4, 6, 8);
        Vector expected = new (-8, 18, 32);
        Vector actual = transform * vector;

        Assert.IsTrue(expected.Matches(actual));

        expected = new Vector(-2, 2, 2);
        actual = inverse * vector;

        Assert.IsTrue(expected.Matches(actual));
    }

    [TestMethod]
    public void TestRotatePointsAroundX()
    {
        Matrix halfQuarter = Transforms.RotateAroundX(Math.PI / 4, true);
        Matrix fullQuarter = Transforms.RotateAroundX(Math.PI / 2, true);
        Point point = new (0, 1, 0);
        Point expected = new (0, Math.Sqrt(2) / 2, Math.Sqrt(2) / 2);
        Point actual = halfQuarter * point;

        Assert.IsTrue(expected.Matches(actual));

        expected = new Point(0, 0, 1);
        actual = fullQuarter * point;

        Assert.IsTrue(expected.Matches(actual));

        halfQuarter = halfQuarter.Invert();
        expected = new Point(0, Math.Sqrt(2) / 2, -Math.Sqrt(2) / 2);
        actual = halfQuarter * point;

        Assert.IsTrue(expected.Matches(actual));
    }

    [TestMethod]
    public void TestRotatePointsAroundY()
    {
        Matrix halfQuarter = Transforms.RotateAroundY(Math.PI / 4, true);
        Matrix fullQuarter = Transforms.RotateAroundY(Math.PI / 2, true);
        Point point = new (0, 0, 1);
        Point expected = new (Math.Sqrt(2) / 2, 0, Math.Sqrt(2) / 2);
        Point actual = halfQuarter * point;

        Assert.IsTrue(expected.Matches(actual));

        expected = new Point(1, 0, 0);
        actual = fullQuarter * point;

        Assert.IsTrue(expected.Matches(actual));
    }

    [TestMethod]
    public void TestRotatePointsAroundZ()
    {
        Matrix halfQuarter = Transforms.RotateAroundZ(Math.PI / 4, true);
        Matrix fullQuarter = Transforms.RotateAroundZ(Math.PI / 2, true);
        Point point = new (0, 1, 0);
        Point expected = new (-Math.Sqrt(2) / 2, Math.Sqrt(2) / 2, 0);
        Point actual = halfQuarter * point;

        Assert.IsTrue(expected.Matches(actual));

        expected = new Point(-1, 0, 0);
        actual = fullQuarter * point;

        Assert.IsTrue(expected.Matches(actual));
    }

    [TestMethod]
    public void TestShearPoints()
    {
        Matrix transform = Transforms.Shear(1, 0, 0, 0, 0, 0);
        Point point = new (2, 3, 4);
        Point expected = new (5, 3, 4);
        Point actual = transform * point;

        Assert.IsTrue(expected.Matches(actual));

        transform = Transforms.Shear(0, 1, 0, 0, 0, 0);
        expected = new Point(6, 3, 4);
        actual = transform * point;

        Assert.IsTrue(expected.Matches(actual));

        transform = Transforms.Shear(0, 0, 1, 0, 0, 0);
        expected = new Point(2, 5, 4);
        actual = transform * point;

        Assert.IsTrue(expected.Matches(actual));

        transform = Transforms.Shear(0, 0, 0, 1, 0, 0);
        expected = new Point(2, 7, 4);
        actual = transform * point;

        Assert.IsTrue(expected.Matches(actual));

        transform = Transforms.Shear(0, 0, 0, 0, 1, 0);
        expected = new Point(2, 3, 6);
        actual = transform * point;

        Assert.IsTrue(expected.Matches(actual));

        transform = Transforms.Shear(0, 0, 0, 0, 0, 1);
        expected = new Point(2, 3, 7);
        actual = transform * point;

        Assert.IsTrue(expected.Matches(actual));
    }

    [TestMethod]
    public void TestAccumulation()
    {
        // Apply in series.
        Point point = new (1, 0, 1);
        Matrix a = Transforms.RotateAroundX(Math.PI / 2, true);
        Matrix b = Transforms.Scale(5);
        Matrix c = Transforms.Translate(10, 5, 7);
        Point p2 = a * point;

        Assert.IsTrue(new Point(1, -1, 0).Matches(p2));

        Point p3 = b * p2;

        Assert.IsTrue(new Point(5, -5, 0).Matches(p3));

        Point p4 = c * p3;

        Assert.IsTrue(new Point(15, 0, 7).Matches(p4));

        // All at once.
        Matrix transform = c * b * a;

        p4 = transform * point;

        Assert.IsTrue(new Point(15, 0, 7).Matches(p4));
    }
}
