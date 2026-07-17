using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
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

        Pigment pigment = CreateStripedPigment(BandType.LinearX);
        
        pigment.Transform = Transforms.Scale(2);

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
        Assert.IsTrue(Colors.Gray75.Matches(source.GetColorFor(new Point(0.25, 0, 0))));
        Assert.IsTrue(Colors.Gray50.Matches(source.GetColorFor(new Point(0.5, 0, 0))));
        Assert.IsTrue(Colors.Gray25.Matches(source.GetColorFor(new Point(0.75, 0, 0))));
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

    // The tests below read a pattern's value straight off Evaluate rather than going through a
    // pigment the way the ones above do.  What is being pinned here is the value itself -- where
    // a falloff reaches zero, which quadrant an angle lands in -- and saying so directly beats
    // saying it as "black", especially since InvertedClip means the darker colour sits at the
    // *centre* of a falloff, which is easy to get backwards.

    [TestMethod]
    public void TestSphericalPatternFallsOffToTheUnitSphere()
    {
        SphericalPattern pattern = new ();

        Assert.AreEqual(1, pattern.Evaluate(Point.Zero));
        Assert.AreEqual(0.5, pattern.Evaluate(new Point(0.5, 0, 0)));
        Assert.AreEqual(0.5, pattern.Evaluate(new Point(0, 0.5, 0)));
        Assert.AreEqual(0.5, pattern.Evaluate(new Point(0, 0, 0.5)));

        // Every axis reaches the unit sphere at the same distance, and beyond it the falloff has
        // stopped rather than gone negative or wrapped.
        Assert.AreEqual(0, pattern.Evaluate(new Point(1, 0, 0)));
        Assert.AreEqual(0, pattern.Evaluate(new Point(0, 0, 5)));
    }

    [TestMethod]
    public void TestCylindricalPatternIgnoresY()
    {
        CylindricalPattern pattern = new ();

        Assert.AreEqual(1, pattern.Evaluate(Point.Zero));
        Assert.AreEqual(0.5, pattern.Evaluate(new Point(0.5, 0, 0)));
        Assert.AreEqual(0, pattern.Evaluate(new Point(1, 0, 0)));
        Assert.AreEqual(0, pattern.Evaluate(new Point(0, 0, 1)));

        // The distance is from the axis, so travelling along it changes nothing.  This is the
        // whole of what separates this pattern from the spherical one.
        Assert.AreEqual(1, pattern.Evaluate(new Point(0, 99, 0)));
        Assert.AreEqual(0.5, pattern.Evaluate(new Point(0.5, 99, 0)));
    }

    [TestMethod]
    public void TestRadialPatternWrapsOnceAroundTheYAxis()
    {
        RadialPattern pattern = new ();

        // A full turn, a quarter at a time.  Note the value runs 0.25 to 1.25 rather than 0 to 1
        // -- POV-Ray's own arrangement, and deliberate: colour maps wrap what they are handed, so
        // the extra quarter turn only decides where the seam falls, which is +X below.
        Assert.IsTrue(0.75.Near(pattern.Evaluate(new Point(0, 0, 1))));
        Assert.IsTrue(1.00.Near(pattern.Evaluate(new Point(1, 0, 0))));
        Assert.IsTrue(1.25.Near(pattern.Evaluate(new Point(0, 0, -1))));
        Assert.IsTrue(0.50.Near(pattern.Evaluate(new Point(-1, 0, 0))));

        // Y plays no part -- the angle is the same all the way up.
        Assert.IsTrue(1.00.Near(pattern.Evaluate(new Point(1, 99, 0))));
    }

    [TestMethod]
    public void TestRadialPatternCoversExactlyOneTurn()
    {
        // Whatever the seam's placement, going right round must cover the map exactly once and
        // no more: the span has to be one whole unit, or the colours would either repeat within
        // a single turn or fail to close up over it.
        RadialPattern pattern = new ();
        double min = double.MaxValue;
        double max = double.MinValue;

        for (double angle = 0; angle < 2 * Math.PI; angle += 0.01)
        {
            double value = pattern.Evaluate(new Point(Math.Sin(angle), 0, Math.Cos(angle)));

            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        Assert.IsTrue((max - min).Near(1, 0.01), $"a turn spanned {max - min}, expected 1");
    }

    [TestMethod]
    public void TestRadialPatternHasNoAngleOnItsOwnAxis()
    {
        // Right on the axis every direction is equally true, so rather than let atan2 pick one,
        // the pattern settles on a quarter turn.  Without this it would be at the mercy of the
        // sign of a zero.
        RadialPattern pattern = new ();

        Assert.AreEqual(0.25, pattern.Evaluate(Point.Zero));
        Assert.AreEqual(0.25, pattern.Evaluate(new Point(0, 5, 0)));
    }

    [TestMethod]
    public void TestMarbleIsPlainXWithoutTurbulence()
    {
        // Turbulence is optional, and a null one must not throw -- without it marble is just the
        // X coordinate, which the colour map wraps into stripes.
        MarblePattern pattern = new ();

        Assert.AreEqual(0, pattern.Evaluate(Point.Zero));
        Assert.AreEqual(0.25, pattern.Evaluate(new Point(0.25, 9, 9)));
        Assert.AreEqual(3.5, pattern.Evaluate(new Point(3.5, 0, 0)));
    }

    [TestMethod]
    public void TestMarbleIsPushedSidewaysByTurbulence()
    {
        Point point = new (0.25, 0.5, 0.75);
        MarblePattern plain = new ();
        MarblePattern turbulent = new () { Turbulence = new Turbulence { Seed = 3, Depth = 2 } };

        Assert.AreNotEqual(plain.Evaluate(point), turbulent.Evaluate(point));
    }

    [TestMethod]
    public void TestBozoIsRepeatableAndSteeredByItsSeed()
    {
        Point point = new (1.5, 2.5, 3.5);

        Assert.AreEqual(
            new BozoPattern { Seed = 5 }.Evaluate(point),
            new BozoPattern { Seed = 5 }.Evaluate(point));
        Assert.AreNotEqual(
            new BozoPattern { Seed = 5 }.Evaluate(point),
            new BozoPattern { Seed = 6 }.Evaluate(point));
    }

    [TestMethod]
    public void TestCrackleStaysWithinItsInterval()
    {
        CracklePattern pattern = new () { Seed = 5 };

        foreach (Point point in CrackleSamplePoints())
        {
            double value = pattern.Evaluate(point);

            Assert.IsTrue(value is >= 0 and <= 1, $"crackle left [0, 1] at {point}: {value}");
        }
    }

    [TestMethod]
    public void TestCrackleActuallyVaries()
    {
        // A guard against the feature points all landing in the same place, which is exactly what
        // would happen if this drew them from gradient noise -- that is identically zero at the
        // integer lattice points the cells are cut on.  Flat output would still pass the interval
        // test above, so it needs saying separately.
        CracklePattern pattern = new () { Seed = 5 };
        double min = double.MaxValue;
        double max = double.MinValue;

        foreach (Point point in CrackleSamplePoints())
        {
            double value = pattern.Evaluate(point);

            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        Assert.IsTrue(min < 0.05, $"crackle never approached a cell boundary; lowest was {min}");
        Assert.IsTrue(max > 0.3, $"crackle never got well inside a cell; highest was {max}");
    }

    [TestMethod]
    public void TestCrackleIsRepeatableAndSteeredByItsSeed()
    {
        Point point = new (1.5, 2.5, 3.5);

        Assert.AreEqual(
            new CracklePattern { Seed = 5 }.Evaluate(point),
            new CracklePattern { Seed = 5 }.Evaluate(point));
        Assert.AreNotEqual(
            new CracklePattern { Seed = 5 }.Evaluate(point),
            new CracklePattern { Seed = 6 }.Evaluate(point));
    }

    [TestMethod]
    public void TestCrackleCellsHoldStillAcrossTheirBoundaries()
    {
        // Each cell's feature point has to be a pure function of that cell, because neighbouring
        // positions in different cells ask about the same ones.  If it drifted, the cells would
        // shift underfoot and the pattern would tear at every boundary.  Stepping either side of
        // the boundary at x = 1 must therefore give near-equal values, since the feature points
        // in view barely change.
        CracklePattern pattern = new () { Seed = 5 };
        double before = pattern.Evaluate(new Point(0.9999, 0.5, 0.5));
        double after = pattern.Evaluate(new Point(1.0001, 0.5, 0.5));

        Assert.IsTrue(before.Near(after, 0.01), $"crackle tore at a cell boundary: {before} vs {after}");
    }

    /// <summary>
    /// A lattice of points deliberately off the whole numbers the cells are cut on, spread over
    /// enough cells to meet a good spread of feature points.
    /// </summary>
    private static IEnumerable<Point> CrackleSamplePoints()
    {
        for (double x = 0; x < 6; x += 0.13)
        for (double y = 0; y < 6; y += 0.17)
        for (double z = 0; z < 6; z += 0.19)
            yield return new Point(x, y, z);
    }

    internal static PatternPigment CreateStripedPigment(BandType bandType)
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
