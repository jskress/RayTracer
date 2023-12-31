using RayTracer.Basics;
using RayTracer.ColorSources;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace Tests;

[TestClass]
public class TestPatterns
{
    [TestMethod]
    public void TestStripedPattern()
    {
        StripeColorSource source = new (SolidColorSource.White, SolidColorSource.Black);

        Assert.AreSame(Colors.White, source.GetColorFor(Point.Zero));
        Assert.AreSame(Colors.White, source.GetColorFor(new Point(0, 1, 0)));
        Assert.AreSame(Colors.White, source.GetColorFor(new Point(0, 2, 0)));

        Assert.AreSame(Colors.White, source.GetColorFor(new Point(0, 0, 1)));
        Assert.AreSame(Colors.White, source.GetColorFor(new Point(0, 0, 2)));

        Assert.AreSame(Colors.White, source.GetColorFor(new Point(0.9, 0, 0)));
        Assert.AreSame(Colors.Black, source.GetColorFor(new Point(1, 0, 0)));
        Assert.AreSame(Colors.Black, source.GetColorFor(new Point(-0.1, 0, 0)));
        Assert.AreSame(Colors.Black, source.GetColorFor(new Point(-1, 0, 0)));
        Assert.AreSame(Colors.White, source.GetColorFor(new Point(-1.1, 0, 0)));
    }

    [TestMethod]
    public void TestPatternTransforms()
    {
        Sphere sphere = new ()
        {
            Material = new Material
            {
                ColorSource = new StripeColorSource(
                    SolidColorSource.White, SolidColorSource.Black)
            },
            Transform = Transforms.Scale(2)
        };
        Point point = new (1.5, 0, 0);
        Color color = sphere.Material.ColorSource.GetColorFor(sphere, point);

        Assert.IsTrue(Colors.White.Matches(color));

        ColorSource colorSource = new StripeColorSource(
            SolidColorSource.White, SolidColorSource.Black)
        {
            Transform = Transforms.Scale(2)
        };

        sphere = new Sphere
        {
            Material = new Material { ColorSource = colorSource }
        };
        color = sphere.Material.ColorSource.GetColorFor(sphere, point);

        Assert.IsTrue(Colors.White.Matches(color));
    }

    [TestMethod]
    public void TestLinearGradientPattern()
    {
        LinearGradientColorSource source = new (
            SolidColorSource.White, SolidColorSource.Black);

        Assert.IsTrue(Colors.White.Matches(source.GetColorFor(Point.Zero)));
        Assert.IsTrue(new Color(0.75, 0.75, 0.75).Matches(
            source.GetColorFor(new Point(0.25, 0, 0))));
        Assert.IsTrue(new Color(0.5, 0.5, 0.5).Matches(
            source.GetColorFor(new Point(0.5, 0, 0))));
        Assert.IsTrue(new Color(0.25, 0.25, 0.25).Matches(
            source.GetColorFor(new Point(0.75, 0, 0))));
    }

    [TestMethod]
    public void TestRingPattern()
    {
        RingColorSource source = new (
            SolidColorSource.White, SolidColorSource.Black);

        Assert.IsTrue(Colors.White.Matches(source.GetColorFor(Point.Zero)));
        Assert.IsTrue(Colors.Black.Matches(source.GetColorFor(new Point(1, 0, 0))));
        Assert.IsTrue(Colors.Black.Matches(source.GetColorFor(new Point(0, 0, 1))));
        Assert.IsTrue(Colors.Black.Matches(source.GetColorFor(new Point(0.708, 0, 0.708))));
    }

    [TestMethod]
    public void TestCheckerboardPattern()
    {
        CheckerColorSource source = new (
            SolidColorSource.White, SolidColorSource.Black);

        Assert.IsTrue(Colors.White.Matches(source.GetColorFor(Point.Zero)));
        Assert.IsTrue(Colors.White.Matches(source.GetColorFor(new Point(0.99, 0, 0))));
        Assert.IsTrue(Colors.Black.Matches(source.GetColorFor(new Point(1.01, 0, 0))));

        Assert.IsTrue(Colors.White.Matches(source.GetColorFor(new Point(0, 0.99, 0))));
        Assert.IsTrue(Colors.Black.Matches(source.GetColorFor(new Point(0, 1.01, 0))));

        Assert.IsTrue(Colors.White.Matches(source.GetColorFor(new Point(0, 0, 0.99))));
        Assert.IsTrue(Colors.Black.Matches(source.GetColorFor(new Point(0, 0, 1.01))));
    }
}
