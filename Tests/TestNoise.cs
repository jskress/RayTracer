using System.Reflection;
using RayTracer.Basics;
using RayTracer.Extensions;

namespace Tests;

[TestClass]
public class TestNoise
{
    /// <summary>
    /// This helper walks a wide, deliberately non-round-numbered lattice of points, so samples
    /// don't all land on the integer boundaries where gradient noise is always zero.
    /// </summary>
    private static IEnumerable<Point> SamplePoints()
    {
        for (double x = 0; x < 20; x += 0.19)
        for (double y = 0; y < 20; y += 0.23)
        for (double z = 0; z < 20; z += 0.29)
            yield return new Point(x, y, z);
    }

    [TestMethod]
    public void TestNoiseHonorsThePovRayZeroToOneContract()
    {
        // Every pattern here that consumes noise was ported from POV-Ray, so each one assumes
        // POV's contract for Noise(): never outside [0, 1], and centered near 0.5.  Raw
        // gradient noise is centered on 0 and half negative instead, which silently moves each
        // pattern's chosen bias point (granite's "0.5 - noise", say) from the middle of the
        // distribution out to its tail.  This test pins the contract those patterns rely on.
        NoiseGenerator noise = NoiseGenerator.ForSeed(12345);
        double min = double.MaxValue;
        double max = double.MinValue;
        double sum = 0;
        int count = 0;

        foreach (Point point in SamplePoints())
        {
            double value = noise.Noise(point);

            min = Math.Min(min, value);
            max = Math.Max(max, value);
            sum += value;
            count++;
        }

        Assert.IsTrue(min >= 0, $"noise went below 0: {min}");
        Assert.IsTrue(max <= 1, $"noise went above 1: {max}");

        // POV documents its own mean as 0.49 ("ideally it would be 0.5").
        double mean = sum / count;

        Assert.IsTrue(mean.Near(0.49, 0.02), $"mean was {mean}, expected ~0.49");
    }

    [TestMethod]
    public void TestNoiseActuallyUsesItsWholeRange()
    {
        // A guard against "fixing" the range by simply crushing everything toward the middle:
        // the noise still has to reach out near both ends to be worth anything.
        NoiseGenerator noise = NoiseGenerator.ForSeed(12345);
        double min = double.MaxValue;
        double max = double.MinValue;

        foreach (Point point in SamplePoints())
        {
            double value = noise.Noise(point);

            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        Assert.IsTrue(min < 0.05, $"noise never got near 0; lowest was {min}");
        Assert.IsTrue(max > 0.95, $"noise never got near 1; highest was {max}");
    }

    [TestMethod]
    public void TestDefaultNoiseIsRepeatable()
    {
        // The default generator used to build its tables from a shared, arbitrarily seeded
        // random source, so a scene that named no seed of its own rendered differently every
        // run.  Asking for the default twice must give the very same generator.
        Assert.AreSame(NoiseGenerator.ForSeed(), NoiseGenerator.ForSeed());
    }

    [TestMethod]
    public void TestBuildingAGeneratorTwiceFromOneSeedGivesTheSameNoise()
    {
        // Generators are cached per seed, but the cache is a ConcurrentDictionary, and its
        // factory is free to run on several threads at once and keep only one of the results.
        // Scanners render in parallel, so that is not hypothetical -- it is what happens on the
        // first call of a render.  Every one of those runs therefore has to build identical
        // tables, which means construction must not draw from anything shared and stateful: two
        // threads drawing from one sequence interleave, take different numbers from the same
        // seed, and leave the noise -- and so the whole image -- hostage to whichever thread
        // won.  A shared generator here really did make renders differ run to run.
        //
        // Racing threads can't be made to reproduce that on demand, so this pins the property
        // underneath it instead: build twice from one seed and insist the two agree.
        NoiseGenerator first = Build(4242);
        NoiseGenerator second = Build(4242);

        foreach (Point point in SamplePoints().Take(500))
            Assert.AreEqual(first.Noise(point), second.Noise(point));
    }

    /// <summary>
    /// This builds a generator directly, sidestepping the per-seed cache, so that two of them
    /// for the same seed can be compared.
    /// </summary>
    private static NoiseGenerator Build(int seed)
    {
        return (NoiseGenerator) Activator.CreateInstance(
            typeof(NoiseGenerator), BindingFlags.NonPublic | BindingFlags.Instance,
            null, [seed], null);
    }

    [TestMethod]
    public void TestTheSameSeedAlwaysGivesTheSameNoise()
    {
        Point point = new (1.5, 2.5, 3.5);

        Assert.AreEqual(
            NoiseGenerator.ForSeed(77).Noise(point),
            NoiseGenerator.ForSeed(77).Noise(point));
    }

    [TestMethod]
    public void TestDifferentSeedsGiveDifferentNoise()
    {
        Point point = new (1.5, 2.5, 3.5);

        Assert.AreNotEqual(
            NoiseGenerator.ForSeed(77).Noise(point),
            NoiseGenerator.ForSeed(78).Noise(point));
    }

    [TestMethod]
    public void TestDNoiseComponentsAreIndependent()
    {
        // This is the root of the wood pattern's old bug: it needed a different displacement
        // per axis, but got the same number three times over.  Noise is a deterministic
        // function of its point, so the only way to get genuinely different components is for
        // DNoise to sample somewhere different for each -- which is what this checks.
        NoiseGenerator noise = NoiseGenerator.ForSeed(12345);
        int matches = 0;
        int count = 0;

        foreach (Point point in SamplePoints())
        {
            Vector vector = noise.DNoise(point);

            if (vector.X.Near(vector.Y) || vector.Y.Near(vector.Z) || vector.X.Near(vector.Z))
                matches++;

            count++;
        }

        // Independent components will coincide once in a long while purely by chance; what
        // must not happen is them agreeing systematically.
        Assert.IsTrue(matches < count / 100, $"{matches} of {count} DNoise samples had matching components");
    }

    [TestMethod]
    public void TestDNoiseIsZeroCenteredAndBounded()
    {
        // DNoise hands back raw, unbiased noise, so its components should sit around zero
        // rather than around 0.5 the way Noise's do.  A sweep of ~48 million points put the
        // underlying noise's extent at about -0.724 to 0.694, so allowing 0.8 here catches a
        // gross scaling mistake while leaving room for a sample worse than any of those.
        NoiseGenerator noise = NoiseGenerator.ForSeed(12345);
        double sum = 0;
        int count = 0;

        foreach (Point point in SamplePoints())
        {
            Vector vector = noise.DNoise(point);

            Assert.IsTrue(Math.Abs(vector.X) < 0.8, $"DNoise X out of range: {vector.X}");
            Assert.IsTrue(Math.Abs(vector.Y) < 0.8, $"DNoise Y out of range: {vector.Y}");
            Assert.IsTrue(Math.Abs(vector.Z) < 0.8, $"DNoise Z out of range: {vector.Z}");

            sum += vector.X + vector.Y + vector.Z;
            count += 3;
        }

        double mean = sum / count;

        Assert.IsTrue(mean.Near(0, 0.02), $"DNoise mean was {mean}, expected ~0");
    }

    [TestMethod]
    public void TestTurbulenceIsNeverNegative()
    {
        // POV-Ray's scalar Turbulence() has no absolute value in it, and doesn't need one:
        // it sums positively weighted samples of a function that never goes below zero.  An
        // Abs call used to sit here compensating for noise that was wrongly centered on zero;
        // this pins the property that made it unnecessary.
        Turbulence turbulence = new () { Seed = 12345, Depth = 3 };

        foreach (Point point in SamplePoints())
            Assert.IsTrue(turbulence.Generate(point) >= 0);
    }

    [TestMethod]
    public void TestTurbulenceVectorDisplacesEachAxisDifferently()
    {
        Turbulence turbulence = new () { Seed = 12345, Depth = 3 };
        int matches = 0;
        int count = 0;

        foreach (Point point in SamplePoints())
        {
            Vector vector = turbulence.GenerateVector(point);

            if (vector.X.Near(vector.Y))
                matches++;

            count++;
        }

        Assert.IsTrue(matches < count / 100, $"{matches} of {count} turbulence vectors had X == Y");
    }

    [TestMethod]
    public void TestPhasedTurbulenceRidesTheOriginalPoint()
    {
        // Phasing mixes the point's own Z into the result.  The octave loop walks a scaled
        // copy of the point, and phasing must not accidentally pick that copy up -- doing so
        // would silently multiply Z by 2^Depth and shift the phase.  Two points differing only
        // in Z must therefore phase differently, and a deeper turbulence must not change how
        // much a given Z shift moves the phase.
        Turbulence shallow = new () { Seed = 12345, Depth = 1, Phased = true, Scale = 1, Tightness = 0 };
        Turbulence deep = new () { Seed = 12345, Depth = 5, Phased = true, Scale = 1, Tightness = 0 };

        // With Tightness at 0 the noise term drops out, leaving only the phase: 1 + sin(Z).
        Point point = new (0.5, 0.5, 1.0);
        double expected = 1 + Math.Sin(1.0);

        Assert.IsTrue(expected.Near(shallow.Generate(point)));
        Assert.IsTrue(expected.Near(deep.Generate(point)));
    }
}
