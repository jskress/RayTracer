using RayTracer.Basics;

namespace Tests;

/// <summary>
/// These tests cover the scattered values a scene draws on to make things differ from one another.
/// <para>
/// Two sorts of thing are checked here and they matter for different reasons.  That the values are
/// evenly spread and that neighbors have nothing to do with each other is what makes them useful at
/// all.  That the <i>particular</i> values never change is what makes a scene reproducible: every
/// picture in the gallery that scatters anything is built on these exact numbers, so a change to the
/// mixing below has to be a deliberate act with a re-rendering behind it, not something that slips in
/// while tidying.
/// </para>
/// </summary>
[TestClass]
public class TestScatterGenerator
{
    [TestMethod]
    public void TestTheSameKeysAlwaysGiveTheSameValue()
    {
        // Pinned, deliberately.  If these change, every scattered scene in the gallery changes with
        // them, so the failure is the point.
        Assert.AreEqual(0.468530690, ScatterGenerator.At(3), 1e-9);
        Assert.AreEqual(0.921333009, ScatterGenerator.At(3, 1), 1e-9);
        Assert.AreEqual(0.778186001, ScatterGenerator.At(3, 2), 1e-9);

        // And asking again gives the same answer, which a running stream could not do.
        Assert.AreEqual(ScatterGenerator.At(3), ScatterGenerator.At(3));
        Assert.AreEqual(ScatterGenerator.At(3, 2), ScatterGenerator.At(3, 2));
        Assert.AreEqual(ScatterGenerator.At(3, 2, 9), ScatterGenerator.At(3, 2, 9));
    }

    [TestMethod]
    public void TestEveryValueLiesBetweenZeroAndOne()
    {
        for (int key = 0; key < 5000; key++)
        {
            double value = ScatterGenerator.At(key);

            Assert.IsTrue(value is >= 0 and < 1, $"{key} gave {value}");
        }
    }

    [TestMethod]
    public void TestTheValuesAreEvenlySpread()
    {
        // Twenty thousand values over ten buckets should put about two thousand in each.  The bound
        // here is generous -- five per cent -- since this is checking for a lump, not measuring one.
        int[] buckets = new int[10];

        for (int key = 0; key < 20000; key++)
            buckets[(int) (ScatterGenerator.At(key) * 10)]++;

        foreach (int count in buckets)
            Assert.IsTrue(count is > 1800 and < 2200, $"a bucket held {count} of 20000");
    }

    [TestMethod]
    public void TestNeighboringKeysHaveNothingToDoWithEachOther()
    {
        // The whole difference between this and noise.  Nearby keys must not give nearby values, or a
        // row of things scattered by their position would come out sorted.
        double[] values = new double[20000];

        for (int key = 0; key < values.Length; key++)
            values[key] = ScatterGenerator.At(key);

        double mean = values.Average();
        double covariance = 0;
        double variance = 0;

        for (int index = 0; index < values.Length - 1; index++)
            covariance += (values[index] - mean) * (values[index + 1] - mean);

        foreach (double value in values)
            variance += (value - mean) * (value - mean);

        Assert.IsTrue(Math.Abs(covariance / variance) < 0.02,
            $"successive values correlate at {covariance / variance}");
    }

    [TestMethod]
    public void TestASecondKeyGivesAnUnrelatedValue()
    {
        // How one thing gets several numbers of its own: the first key says which thing and the second
        // says which of its numbers.  They must not be near one another.
        int near = 0;

        for (int key = 0; key < 2000; key++)
        {
            if (Math.Abs(ScatterGenerator.At(key, 1) - ScatterGenerator.At(key, 2)) < 0.01)
                near++;
        }

        // Two unrelated values land within a hundredth of each other about two per cent of the time.
        Assert.IsTrue(near < 80, $"{near} of 2000 pairs were nearly equal");
    }

    [TestMethod]
    public void TestNegativeZeroIsZero()
    {
        // They are the same number and a scene that wrote one meant the other, but their bits differ.
        Assert.AreEqual(ScatterGenerator.At(0), ScatterGenerator.At(-0.0));
        Assert.AreEqual(ScatterGenerator.At(1, 0), ScatterGenerator.At(1, -0.0));
    }

    [TestMethod]
    public void TestFractionsAreKeysToo()
    {
        // A key need not be a whole number, and two keys a millionth apart are as unrelated as any
        // others -- which is what lets a position be scattered by directly.
        Assert.AreNotEqual(ScatterGenerator.At(0.5), ScatterGenerator.At(0.5000001));
        Assert.IsTrue(Math.Abs(ScatterGenerator.At(0.5) - ScatterGenerator.At(0.5000001)) > 0.01);
    }
}
