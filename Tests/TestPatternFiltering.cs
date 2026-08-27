using RayTracer.Basics;
using RayTracer.Patterns;

namespace Tests;

[TestClass]
public class TestPatternFiltering
{
    [TestMethod]
    public void TestTheNoiseAveragesAreWhatTheyAreWrittenDownAs()
    {
        // Two constants are measured off the noise generator rather than derived, and band-limiting
        // leans on both: a layer of noise too fine to see is faded toward its own average, and
        // fading toward the wrong number stains everything it touches rather than merely softening
        // it.  Measured wrong once already -- fading toward nought took a granite wall twice as far
        // from a supersampled truth as no filtering at all.
        //
        // So they are checked here.  If the noise is ever changed, this is what says so.
        NoiseGenerator generator = NoiseGenerator.ForSeed(11);
        double value = 0, rectified = 0, cubed = 0;
        Vector displacement = new (0, 0, 0);
        int count = 0;

        for (int x = 0; x < 40; x++)
        for (int y = 0; y < 40; y++)
        for (int z = 0; z < 40; z++)
        {
            Point point = new (x * 0.37, y * 0.41, z * 0.43);
            double noise = generator.Noise(point);

            value += noise;
            cubed += noise * noise * noise;
            rectified += Math.Abs(0.5 - noise);
            displacement += generator.DNoise(point);
            count++;
        }

        Assert.AreEqual(NoiseGenerator.AverageValue, value / count, 0.01,
            "the noise no longer averages what the band-limiting believes it does");
        Assert.AreEqual(NoiseGenerator.AverageRectifiedValue, rectified / count, 0.01,
            "the rectified noise no longer averages what granite's band-limiting believes");
        Assert.AreEqual(NoiseGenerator.AverageCubedValue, cubed / count, 0.01,
            "the cubed noise no longer averages what the dents band-limiting believes");

        // And this is the one that may be faded toward nothing, which is why it is worth pinning.
        Assert.AreEqual(0, displacement.Magnitude / count, 0.01,
            "the displacement noise is no longer centred on nothing, so fading it out now shifts " +
            "every pattern that turbulence stirs");
    }

    [TestMethod]
    public void TestALatticeAveragesItselfOverThePatchItIsGiven()
    {
        // A brick pattern answers 0 for mortar and 1 for brick.  Asked about a patch big enough to
        // hold a great many of both, it must answer with the *proportion* -- somewhere between the
        // two -- because that is what the patch honestly looks like.  Answering 0 or 1 for a wall
        // too far off to show its courses is the whole cause of the banding this exists to stop.
        BrickPattern brick = new ();
        Point point = new (3.3, 2.7, 1.9);

        double sampled = brick.Evaluate(point);
        double averaged = brick.Evaluate(point, new Footprint(
            new Vector(40, 0, 0), new Vector(0, 40, 0)));

        Assert.IsTrue(sampled is 0 or 1,
            $"a point sample of a brick should be one thing or the other, and was {sampled}");
        Assert.IsTrue(averaged is > 0.05 and < 0.95,
            $"asked about a patch forty units across, the brick answered {averaged:F4} -- it is " +
            "still answering for a point, so nothing is being filtered");
    }

    [TestMethod]
    public void TestAPatchOfNothingLeavesAPatternExactlyAsItWas()
    {
        // The guarantee that lets all of this be added without disturbing a single existing render:
        // a ray that was never told it spreads gives a footprint of nothing, and a footprint of
        // nothing must give the very same answer the point sample always did.
        BrickPattern brick = new ();
        CheckerPattern checker = new ();

        foreach (Point point in (Point[])
                 [new (0.1, 0.2, 0.3), new (3.3, 2.7, 1.9), new (-4.5, 0.05, 12.25)])
        {
            Assert.AreEqual(brick.Evaluate(point), brick.Evaluate(point, Footprint.None), 1e-12);
            Assert.AreEqual(checker.Evaluate(point), checker.Evaluate(point, Footprint.None), 1e-12);
            Assert.AreEqual(brick.ValueFor(point), brick.ValueFor(point, Footprint.None), 1e-12);
        }
    }

    [TestMethod]
    public void TestBandLimitingKeepsTheShapeAndDropsOnlyTheGrain()
    {
        // The reason this is done by layer rather than by fading the whole pattern toward its
        // average.  A granite wall too far off to show its grain is still granite -- it keeps the
        // large mottling that is the shape of the stone.  Fading the lot would leave flat paint.
        GranitePattern granite = new () { Seed = 3 };
        Point[] points =
            [new (0.05, 0, 0), new (0.55, 0, 0), new (1.05, 0, 0), new (1.55, 0, 0)];

        // A patch wide enough to swallow the grain but not the mottling.
        Footprint patch = new (new Vector(0.02, 0, 0), new Vector(0, 0.02, 0));
        double[] filtered = points.Select(point => granite.Evaluate(point, patch)).ToArray();
        double spread = filtered.Max() - filtered.Min();

        Assert.IsTrue(spread > 0.02,
            $"across places half a unit apart the filtered granite varied by only {spread:F4}; the " +
            "coarse mottling has gone as well as the grain, which is flat paint rather than stone");
    }
}
