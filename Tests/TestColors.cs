using RayTracer.Graphics;

namespace Tests;

[TestClass]
public class TestColors
{
    [TestMethod]
    public void TestConstruction()
    {
        Color color = new Color(-0.5, 0.4, 1.7);

        Assert.AreEqual(-0.5, color.Red);
        Assert.AreEqual(0.4, color.Green);
        Assert.AreEqual(1.7, color.Blue);
        Assert.AreEqual(1, color.Alpha);
    }

    [TestMethod]
    public void TestAddition()
    {
        Color color1 = new (0.9, 0.6, 0.75);
        Color color2 = new (0.7, 0.1, 0.25);
        Color expected = new (1.6, 0.7, 1.0);

        Color result = color1 + color2;

        Assert.IsTrue(expected.Matches(result));
    }

    [TestMethod]
    public void TestSubtraction()
    {
        Color color1 = new (0.9, 0.6, 0.75);
        Color color2 = new (0.7, 0.1, 0.25);
        Color expected = new (0.2, 0.5, 0.5);

        Color result = color1 - color2;

        Assert.IsTrue(expected.Matches(result));
    }

    [TestMethod]
    public void TestScalarMultiplication()
    {
        Color color = new (0.2, 0.3, 0.4);
        Color expected = new (0.4, 0.6, 0.8);

        Color result = color * 2;

        Assert.IsTrue(expected.Matches(result));
    }

    [TestMethod]
    public void TestMultiplication()
    {
        Color color1 = new (1, 0.2, 0.4);
        Color color2 = new (0.9, 1, 0.1);
        Color expected = new (0.9, 0.2, 0.04);

        Color result = color1 * color2;

        Assert.IsTrue(expected.Matches(result));
    }
}
