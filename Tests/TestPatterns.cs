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
        StripeColorSource source = new (Color.White, Color.Black);

        Assert.AreSame(Color.White, source.GetColorFor(Point.Zero));
        Assert.AreSame(Color.White, source.GetColorFor(new Point(0, 1, 0)));
        Assert.AreSame(Color.White, source.GetColorFor(new Point(0, 2, 0)));

        Assert.AreSame(Color.White, source.GetColorFor(new Point(0, 0, 1)));
        Assert.AreSame(Color.White, source.GetColorFor(new Point(0, 0, 2)));

        Assert.AreSame(Color.White, source.GetColorFor(new Point(0.9, 0, 0)));
        Assert.AreSame(Color.Black, source.GetColorFor(new Point(1, 0, 0)));
        Assert.AreSame(Color.Black, source.GetColorFor(new Point(-0.1, 0, 0)));
        Assert.AreSame(Color.Black, source.GetColorFor(new Point(-1, 0, 0)));
        Assert.AreSame(Color.White, source.GetColorFor(new Point(-1.1, 0, 0)));
    }

    [TestMethod]
    public void TestPatternTransforms()
    {
        Sphere sphere = new ()
        {
            Material = new Material
            {
                ColorSource = new StripeColorSource(Color.White, Color.Black)
            },
            Transform = Transforms.Scale(2)
        };
        Point point = new (1.5, 0, 0);
        Color color = sphere.Material.ColorSource.GetColorFor(sphere, point);

        Assert.IsTrue(Color.White.Matches(color));

        ColorSource colorSource = new StripeColorSource(Color.White, Color.Black)
        {
            Transform = Transforms.Scale(2)
        };

        sphere = new Sphere
        {
            Material = new Material { ColorSource = colorSource }
        };
        color = sphere.Material.ColorSource.GetColorFor(sphere, point);

        Assert.IsTrue(Color.White.Matches(color));
    }

    [TestMethod]
    public void TestLinearGradientPattern()
    {
        LinearGradientColorSource source = new (Color.White, Color.Black);

        Assert.IsTrue(Color.White.Matches(source.GetColorFor(Point.Zero)));
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
        RingColorSource source = new (Color.White, Color.Black);

        Assert.IsTrue(Color.White.Matches(source.GetColorFor(Point.Zero)));
        Assert.IsTrue(Color.Black.Matches(source.GetColorFor(new Point(1, 0, 0))));
        Assert.IsTrue(Color.Black.Matches(source.GetColorFor(new Point(0, 0, 1))));
        Assert.IsTrue(Color.Black.Matches(source.GetColorFor(new Point(0.708, 0, 0.708))));
    }

    [TestMethod]
    public void TestCheckerboardPattern()
    {
        CheckerColorSource source = new (Color.White, Color.Black);

        Assert.IsTrue(Color.White.Matches(source.GetColorFor(Point.Zero)));
        Assert.IsTrue(Color.White.Matches(source.GetColorFor(new Point(0.99, 0, 0))));
        Assert.IsTrue(Color.Black.Matches(source.GetColorFor(new Point(1.01, 0, 0))));

        Assert.IsTrue(Color.White.Matches(source.GetColorFor(new Point(0, 0.99, 0))));
        Assert.IsTrue(Color.Black.Matches(source.GetColorFor(new Point(0, 1.01, 0))));

        Assert.IsTrue(Color.White.Matches(source.GetColorFor(new Point(0, 0, 0.99))));
        Assert.IsTrue(Color.Black.Matches(source.GetColorFor(new Point(0, 0, 1.01))));
    }
}
