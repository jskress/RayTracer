using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Patterns;
using RayTracer.Pigments;

namespace Tests;

[TestClass]
public class TestPatterns
{
    [TestMethod]
    public void TestStripedPattern()
    {
        PatternPigment pigment = CreateStripedPigment(BandType.LinearX);
        
        Assert.AreSame(Colors.White, pigment.GetColorFor(Point.Zero));
        Assert.AreSame(Colors.White, pigment.GetColorFor(new Point(0, 1, 0)));
        Assert.AreSame(Colors.White, pigment.GetColorFor(new Point(0, 2, 0)));

        Assert.AreSame(Colors.White, pigment.GetColorFor(new Point(0, 0, 1)));
        Assert.AreSame(Colors.White, pigment.GetColorFor(new Point(0, 0, 2)));

        Assert.AreSame(Colors.White, pigment.GetColorFor(new Point(0.9, 0, 0)));
        Assert.AreSame(Colors.Black, pigment.GetColorFor(new Point(1, 0, 0)));
        Assert.AreSame(Colors.Black, pigment.GetColorFor(new Point(-0.1, 0, 0)));
        Assert.AreSame(Colors.Black, pigment.GetColorFor(new Point(-1, 0, 0)));
        Assert.AreSame(Colors.White, pigment.GetColorFor(new Point(-1.1, 0, 0)));
    }

    [TestMethod]
    public void TestPatternTransforms()
    {
        Sphere sphere = new ()
        {
            Material = new Material
            {
                Pigment = CreateStripedPigment(BandType.LinearX)
            },
            Transform = Transforms.Scale(2)
        };
        Point point = new (1.5, 0, 0);
        Color color = sphere.Material.Pigment.GetColorFor(sphere, point);

        Assert.IsTrue(Colors.White.Matches(color));

        Pigment pigment = new StripePigment(
            SolidPigment.White, SolidPigment.Black)
        {
            Transform = Transforms.Scale(2)
        };

        sphere = new Sphere
        {
            Material = new Material { Pigment = pigment }
        };
        color = sphere.Material.Pigment.GetColorFor(sphere, point);

        Assert.IsTrue(Colors.White.Matches(color));
    }

    [TestMethod]
    public void TestLinearGradientPattern()
    {
        Pigment source = CreateGradientPigment(BandType.LinearX);

        Assert.IsTrue(Colors.White.Matches(source.GetColorFor(Point.Zero)));
        Assert.IsTrue(Colors.Gray25.Matches(source.GetColorFor(new Point(0.25, 0, 0))));
        Assert.IsTrue(Colors.Gray50.Matches(source.GetColorFor(new Point(0.5, 0, 0))));
        Assert.IsTrue(Colors.Gray75.Matches(source.GetColorFor(new Point(0.75, 0, 0))));
    }

    [TestMethod]
    public void TestRingPattern()
    {
        Pigment source = CreateStripedPigment(BandType.Cylindrical);

        Assert.IsTrue(Colors.White.Matches(source.GetColorFor(Point.Zero)));
        Assert.IsTrue(Colors.Black.Matches(source.GetColorFor(new Point(1, 0, 0))));
        Assert.IsTrue(Colors.Black.Matches(source.GetColorFor(new Point(0, 0, 1))));
        Assert.IsTrue(Colors.Black.Matches(source.GetColorFor(new Point(0.708, 0, 0.708))));
    }

    [TestMethod]
    public void TestCheckerboardPattern()
    {
        Pigment source = CreatePigment(new CheckerPattern());

        Assert.IsTrue(Colors.White.Matches(source.GetColorFor(Point.Zero)));
        Assert.IsTrue(Colors.White.Matches(source.GetColorFor(new Point(0.99, 0, 0))));
        Assert.IsTrue(Colors.Black.Matches(source.GetColorFor(new Point(1.01, 0, 0))));

        Assert.IsTrue(Colors.White.Matches(source.GetColorFor(new Point(0, 0.99, 0))));
        Assert.IsTrue(Colors.Black.Matches(source.GetColorFor(new Point(0, 1.01, 0))));

        Assert.IsTrue(Colors.White.Matches(source.GetColorFor(new Point(0, 0, 0.99))));
        Assert.IsTrue(Colors.Black.Matches(source.GetColorFor(new Point(0, 0, 1.01))));
    }

    private static PatternPigment CreateStripedPigment(BandType bandType)
    {
        return CreatePigment(new StripedPattern
        {
            BandType = bandType
        });
    }

    private static PatternPigment CreateGradientPigment(BandType bandType)
    {
        return CreatePigment(new GradientPattern
        {
            BandType = bandType
        });
    }

    private static PatternPigment CreatePigment(Pattern pattern)
    {
        PatternPigment pigment = new PatternPigment
        {
            Pattern = pattern,
            PigmentSet = new PigmentSet()
        };
        
        pigment.PigmentSet.AddEntry(SolidPigment.White);
        pigment.PigmentSet.AddEntry(SolidPigment.Black, 1);

        return pigment;
    }
}
