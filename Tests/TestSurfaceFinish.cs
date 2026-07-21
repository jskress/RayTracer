using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;
using RayTracer.Geometry;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// These tests cover the two knobs that shape how a surface takes diffuse light: brilliance, which
/// bends the falloff as the surface turns away, and grain, which takes fine flecks back out of it.
/// </summary>
[TestClass]
public class TestSurfaceFinish
{
    private static readonly PointLight Light = new () { Location = new Point(0, 0, -10) };

    /// <summary>
    /// Lights a flat white surface whose normal is turned away from the light by the given angle,
    /// and reports how bright it came out.
    /// </summary>
    private static double LitAt(Material material, double degrees)
    {
        double radians = degrees * Math.PI / 180;
        Vector normal = new (Math.Sin(radians), 0, -Math.Cos(radians));
        Sphere surface = new () { Material = material };

        return Light.ApplyPhong(Point.Zero, new Vector(0, 0, -1), normal, surface, Colors.White).Red;
    }

    private static Material Matte(double brilliance = 1, double grain = 0) => new ()
    {
        Pigment = new SolidPigment(Colors.White),
        Ambient = 0,
        Specular = 0,
        Brilliance = brilliance,
        Grain = grain
    };

    [TestMethod]
    public void TestTheDefaultsChangeNothing()
    {
        // The compatibility guarantee.  A brilliance of 1 is Lambert's law, which is exactly what
        // the diffuse term always did, and no grain takes nothing away.
        Material material = Matte();

        Assert.AreEqual(1, material.Brilliance, 1e-12);
        Assert.AreEqual(0, material.Grain, 1e-12);
        Assert.AreEqual(0, material.GrainAt(new Point(0.3, 0.7, 0.4)), 1e-12);

        // Straight on, the surface takes all the light its diffuse allows, whatever the brilliance.
        Assert.AreEqual(LitAt(material, 0), LitAt(Matte(brilliance: 5), 0), 1e-12);
    }

    [TestMethod]
    public void TestBrillianceTightensTheFalloff()
    {
        // Raising it makes a surface hold its brightness near the light and lose it faster as it
        // turns away, which is the burnished look POV-Ray's metals use it for.  It only ever
        // darkens: the cosine it shapes is never above one, so raising the power cannot raise it.
        foreach (double angle in new[] { 30.0, 45.0, 60.0, 75.0 })
        {
            double lambert = LitAt(Matte(), angle);
            double burnished = LitAt(Matte(brilliance: 4), angle);

            Assert.IsTrue(burnished < lambert,
                $"at {angle} degrees, brilliance 4 gave {burnished}, not less than {lambert}");
        }
    }

    [TestMethod]
    public void TestGrainOnlyEverTakesLightAway()
    {
        // POV-Ray's crand subtracts, and so does this: it speckles a surface with shadow, never
        // with glints.  Worth stating outright, since "randomness" would suggest both.
        Material material = Matte(grain: 0.3);

        for (int step = 0; step < 200; step++)
        {
            Point point = new (step * 0.017, step * 0.031, step * 0.043);
            double taken = material.GrainAt(point);

            Assert.IsTrue(taken >= 0 && taken <= 0.3, $"grain gave {taken}, outside [0, 0.3]");
        }
    }

    [TestMethod]
    public void TestGrainStaysWhereItIsPut()
    {
        // The reason this diverges from POV-Ray.  Its crand draws from a random number generator,
        // so the speckle belongs to the ray and crawls whenever the camera moves; here it is hashed
        // from the point, so the same place on a surface always has the same fleck, however many
        // rays find it and from wherever they come.
        Material material = Matte(grain: 0.25);
        Point point = new (1.5, -0.25, 3.75);

        Assert.AreEqual(material.GrainAt(point), material.GrainAt(point), 1e-15);
        Assert.AreEqual(material.GrainAt(point), material.GrainAt(new Point(1.5, -0.25, 3.75)), 1e-15);
    }

    [TestMethod]
    public void TestGrainIsGritRatherThanCloud()
    {
        // What separates this from a mottled pigment: neighbouring points must land on unrelated
        // values.  Coherent noise would give smooth blotches, which is a different effect entirely
        // and one we already have.
        Material material = Matte(grain: 1);
        Point point = new (1.0, 1.0, 1.0);
        int jumps = 0;

        for (int step = 1; step <= 100; step++)
        {
            Point nudged = new (1.0 + step * 1e-6, 1.0, 1.0);

            if (Math.Abs(material.GrainAt(nudged) - material.GrainAt(point)) > 0.1)
                jumps++;
        }

        Assert.IsTrue(jumps > 80, $"only {jumps} of 100 nearby points differed; this is cloud, not grit");
    }

    [TestMethod]
    public void TestGrainSpreadsAcrossItsWholeRange()
    {
        // A hash that clustered its answers would speckle unevenly -- or, at worst, not at all.
        Material material = Matte(grain: 1);
        int[] buckets = new int[10];

        for (int step = 0; step < 10_000; step++)
        {
            Point point = new (step * 0.013, step * 0.029, step * 0.037);

            buckets[Math.Min(9, (int) (material.GrainAt(point) * 10))]++;
        }

        foreach (int count in buckets)
            Assert.IsTrue(count > 700, $"a tenth of the range held only {count} of 10,000 samples");
    }

    [TestMethod]
    public void TestGrainDarkensASurfaceOverall()
    {
        // The visible upshot, end to end: a grainy surface comes out darker on average than the
        // same surface without it, since every fleck subtracts.
        double plain = 0;
        double grainy = 0;

        for (int step = 0; step < 100; step++)
        {
            double angle = step * 0.6;

            plain += LitAt(Matte(), angle);
            grainy += LitAt(Matte(grain: 0.2), angle);
        }

        Assert.IsTrue(grainy < plain, $"grainy {grainy} was not darker than plain {plain}");
    }

    [TestMethod]
    public void TestGrainCannotDriveASurfaceBelowBlack()
    {
        // Subtracting more than there is to take must floor at nothing rather than going negative,
        // which would light the surface with a colour the light never had.
        Material material = Matte(grain: 5);

        foreach (double angle in new[] { 0.0, 45.0, 85.0 })
            Assert.IsTrue(LitAt(material, angle) >= 0, $"at {angle} degrees the surface went negative");
    }
}
