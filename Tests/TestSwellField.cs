using RayTracer.Basics;
using RayTracer.Fields;
using RayTracer.Geometry;

namespace Tests;

/// <summary>
/// These test the arithmetic a body of water is made from, before any of it is a surface: turning the
/// shape round so a height can be had at a place, the normal that falls out of it, and the bound that
/// makes marching it affordable.
/// </summary>
[TestClass]
public class TestSwellField
{
    /// <summary>
    /// A handful of trains crossing at angles, at the steepness of a rough sea.
    /// </summary>
    private static SwellField Sea(double scale = 1) => new (
    [
        new SwellTrain { Amplitude = 0.80 * scale, Wavelength = 20, Direction = new Vector(0.92, 0, 0.39), Phase = 1.0 },
        new SwellTrain { Amplitude = 0.40 * scale, Wavelength = 13, Direction = new Vector(-0.42, 0, 0.91), Phase = 2.3 },
        new SwellTrain { Amplitude = 0.11 * scale, Wavelength = 4.1, Direction = new Vector(0.71, 0, -0.70), Phase = 3.7 },
        new SwellTrain { Amplitude = 0.05 * scale, Wavelength = 1.9, Direction = new Vector(0.26, 0, 0.97), Phase = 5.1 }
    ]);

    /// <summary>
    /// **The one thing everything else rests on.**  The shape says where a piece of water goes; a
    /// renderer needs the other way about.  So take a starting place, see where the water goes, then
    /// ask where the water over *that* came from -- and it must be where we began.
    /// </summary>
    [TestMethod]
    public void TestTurningTheSurfaceRoundGetsBackWhereItStarted()
    {
        SwellField field = Sea();
        Random random = new (20260827);

        for (int trial = 0; trial < 5000; trial++)
        {
            double u = random.NextDouble() * 200 - 100;
            double v = random.NextDouble() * 200 - 100;
            Point went = field.PointAt(u, v);
            (double backU, double backV) = field.SolveFor(went.X, went.Z);

            Assert.AreEqual(u, backU, 1e-6,
                $"the water over ({went.X:F4}, {went.Z:F4}) was not traced back to where it started");
            Assert.AreEqual(v, backV, 1e-6);
        }
    }

    /// <summary>
    /// And the height at a place has to be the height of the water that is actually there.
    /// </summary>
    [TestMethod]
    public void TestTheHeightIsTheHeightOfTheWaterThatIsThere()
    {
        SwellField field = Sea();
        Random random = new (7);

        for (int trial = 0; trial < 2000; trial++)
        {
            double u = random.NextDouble() * 200 - 100;
            double v = random.NextDouble() * 200 - 100;
            Point went = field.PointAt(u, v);

            Assert.AreEqual(went.Y, field.HeightAt(went.X, went.Z), 1e-6);
        }
    }

    /// <summary>
    /// **The crests must be narrower than a sine's, and the troughs broader.**  That asymmetry is the
    /// entire reason for doing this the hard way, so it is worth asserting rather than assuming: a
    /// train of some steepness must spend *less* of its length above half height than a sine does.
    /// A sine spends exactly a third of its wavelength above half its amplitude.
    /// </summary>
    [TestMethod]
    public void TestTheCrestsAreNarrowerThanASinesAndSharpenWithSteepness()
    {
        double previous = 1.0;

        foreach (double steepness in (double[]) [0.001, 0.3, 0.6, 0.9])
        {
            SwellField field = new ([new SwellTrain
            {
                Wavelength = 10, Amplitude = steepness * 10 / (2 * Math.PI),
                Direction = new Vector(1, 0, 0)
            }]);
            double reach = field.Reach;
            int above = 0, steps = 20000;

            for (int step = 0; step < steps; step++)
            {
                if (field.HeightAt(step * 10.0 / steps, 0) > reach * 0.5)
                    above++;
            }

            double fraction = (double) above / steps;

            if (steepness < 0.01)
            {
                Assert.AreEqual(1.0 / 3.0, fraction, 0.01,
                    "at no steepness at all this should be a plain sine, which is above half its " +
                    "height for a third of its length");
            }

            Assert.IsTrue(fraction < previous,
                $"at steepness {steepness} the crest covers {fraction:F3} of the wavelength, which " +
                $"is no narrower than the {previous:F3} before it -- the shape is not sharpening");

            previous = fraction;
        }

        Assert.IsTrue(previous < 0.12,
            $"at a steepness of 0.9 the crest still covers {previous:F3} of the wavelength; it should " +
            "be drawn up into far less than that");
    }

    /// <summary>
    /// **The normal is worked out rather than sampled, so it must be checked against the shape itself.**
    /// Two points either side, along the surface, give a direction the surface really runs in; the
    /// normal has to be square to it.  Nothing here uses the same arithmetic the normal does.
    /// </summary>
    [TestMethod]
    public void TestTheNormalIsSquareToTheSurface()
    {
        SwellField field = Sea();
        Random random = new (99);

        for (int trial = 0; trial < 500; trial++)
        {
            double u = random.NextDouble() * 100 - 50;
            double v = random.NextDouble() * 100 - 50;
            Point here = field.PointAt(u, v);
            Vector normal = field.NormalAt(here.X, here.Z);

            const double Step = 1e-5;

            foreach ((double du, double dv) in ((double, double)[]) [(Step, 0), (0, Step)])
            {
                Point there = field.PointAt(u + du, v + dv);
                Vector along = (there - here).Unit;

                Assert.AreEqual(0, normal.Dot(along), 1e-4,
                    $"the normal {normal} is not square to the surface, which runs {along} there");
            }

            Assert.AreEqual(1, normal.Magnitude, 1e-9);
            Assert.IsTrue(normal.Y > 0, "water lying flat faces upward");
        }
    }

    /// <summary>
    /// **The bound must hold, and must be worth having.**  A marcher throws away stretches of a ray
    /// on the strength of it, so a bound that is too small loses the surface; and one that never
    /// narrows saves nothing.  Both are checked: the height over a box must lie inside it, and the
    /// bound must tighten as the box shrinks.
    /// </summary>
    [TestMethod]
    public void TestTheBoundHoldsAndIsWorthHaving()
    {
        SwellField field = Sea();
        Random random = new (4242);

        for (int trial = 0; trial < 400; trial++)
        {
            double width = Math.Pow(10, random.NextDouble() * 2 - 1);
            double x = random.NextDouble() * 120 - 60;
            double z = random.NextDouble() * 120 - 60;
            FieldRange acrossX = new (x, x + width);
            FieldRange acrossZ = new (z, z + width);
            FieldRange bound = field.HeightOver(acrossX, acrossZ);

            for (int i = 0; i <= 12; i++)
            for (int j = 0; j <= 12; j++)
            {
                double sampleX = acrossX.Low + acrossX.Width * i / 12;
                double sampleZ = acrossZ.Low + acrossZ.Width * j / 12;
                double height = field.HeightAt(sampleX, sampleZ);

                Assert.IsTrue(bound.Contains(height, 1e-9),
                    $"over x{acrossX} z{acrossZ} the height was bounded to {bound}, but at " +
                    $"({sampleX:F4}, {sampleZ:F4}) it is {height}");
            }
        }

        // And it has to be worth asking: a small box far from a crest must rule something out.
        FieldRange wide = field.HeightOver(new FieldRange(0, 400), new FieldRange(0, 400));
        FieldRange narrow = field.HeightOver(new FieldRange(10, 10.2), new FieldRange(10, 10.2));

        Assert.IsTrue(narrow.Width < wide.Width * 0.75,
            $"a box a fifth of a unit across was bounded to {narrow.Width:F4} where the whole sea is " +
            $"{wide.Width:F4}; a bound that never tightens rules nothing out and saves nothing");
    }

    [TestMethod]
    public void MeasureBoundTightness()
    {
        SwellField field = Sea();
        Random random = new (5);

        System.Console.WriteLine($"  reach = {field.Reach:F3}, total steepness = {field.TotalSteepness:F3}");
        System.Console.WriteLine($"  {"box",8} {"bound width",12} {"true width",12} {"slack",8}");

        foreach (double width in (double[]) [40, 10, 2, 0.5, 0.1, 0.02])
        {
            double boundTotal = 0, trueTotal = 0;

            for (int trial = 0; trial < 60; trial++)
            {
                double x = random.NextDouble() * 100 - 50;
                double z = random.NextDouble() * 100 - 50;
                FieldRange bx = new (x, x + width), bz = new (z, z + width);
                FieldRange bound = field.HeightOver(bx, bz);
                double low = double.MaxValue, high = double.MinValue;

                for (int i = 0; i <= 20; i++)
                for (int j = 0; j <= 20; j++)
                {
                    double h = field.HeightAt(x + width * i / 20, z + width * j / 20);

                    low = Math.Min(low, h);
                    high = Math.Max(high, h);
                }

                boundTotal += bound.Width;
                trueTotal += high - low;
            }

            System.Console.WriteLine(
                $"  {width,8:F2} {boundTotal / 60,12:F4} {trueTotal / 60,12:F4} {boundTotal / trueTotal,8:F1}x");
        }
    }
}
