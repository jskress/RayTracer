using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the counts a render reports about itself.
/// <para>
/// The counters are striped across threads so that gathering them does not serialize the very
/// rendering they are measuring, which makes their thread safety the thing worth testing: a striped
/// counter that loses increments under contention would give a number that looks entirely reasonable
/// and is quietly wrong.  Measured on a ray-dense scene, counting cost nothing that could be told
/// from run-to-run noise -- three runs with the counters on came out at 0.60/0.54/0.54 seconds
/// against 0.63/0.60/0.60 with them off.
/// </para>
/// </summary>
[TestClass]
public class TestStatistics
{
    [TestMethod]
    public void TestNothingCountedIsNothingReported()
    {
        Statistics statistics = new ();

        Assert.AreEqual(0, statistics.Pixels);
        Assert.AreEqual(0, statistics.Samples);
        Assert.AreEqual(0, statistics.PrimaryRays);
        Assert.AreEqual(0, statistics.SceneRays);

        // The ratios divide by those zeros, so they have to answer for themselves rather than
        // reporting a NaN into the middle of a line of text a program has to read.
        Assert.AreEqual(0, statistics.SamplesPerPixel);
        Assert.AreEqual(0, statistics.SceneRaysPerSample);
    }

    [TestMethod]
    public void TestEachCountIsKeptApartFromTheOthers()
    {
        Statistics statistics = new ();

        statistics.CountPixel();
        statistics.CountSample(3);
        statistics.CountSceneRay();
        statistics.CountSceneRay();

        Assert.AreEqual(1, statistics.Pixels);
        Assert.AreEqual(1, statistics.Samples);
        Assert.AreEqual(3, statistics.PrimaryRays);
        Assert.AreEqual(2, statistics.SceneRays);
    }

    [TestMethod]
    public void TestTheRatiosAreWhatTheySay()
    {
        Statistics statistics = new ();

        statistics.CountPixel();
        statistics.CountPixel();

        for (int index = 0; index < 10; index++)
        {
            statistics.CountSample(1);
            statistics.CountSceneRay();
            statistics.CountSceneRay();
            statistics.CountSceneRay();
        }

        Assert.AreEqual(5, statistics.SamplesPerPixel, 1e-9);
        Assert.AreEqual(3, statistics.SceneRaysPerSample, 1e-9);
    }

    [TestMethod]
    public void TestNoCountIsLostAcrossThreads()
    {
        // The counters are striped by thread id, and two threads may share a stripe, so this is the
        // test that says a shared stripe costs nothing.
        //
        // Making the contention happen took three tries, and the two that failed are worth recording
        // because each looked convincing.  Parallel.For will not do it: it borrows a handful of pool
        // threads, and a handful of threads over four stripes per processor each land somewhere of
        // their own.  Nor is asking for four hundred real threads and a gate to release them
        // together: only a dozen of them are ever running at once on a dozen processors, so at most
        // two share a stripe at any moment, and two threads rarely land inside the same few
        // nanoseconds.  Both of those pass with a plain increment and so prove nothing at all.
        //
        // What does it is asking for a single stripe.  Then every thread is on the same counter by
        // construction, and there is nothing left to be lucky about: with the increment left plain,
        // this loses counts by the hundred thousand.
        const int threads = 400;
        const int each = 20_000;

        Statistics statistics = new (1);
        List<Thread> running = [];

        using ManualResetEventSlim gate = new (false);

        for (int index = 0; index < threads; index++)
        {
            Thread thread = new (() =>
            {
                gate.Wait();

                for (int count = 0; count < each; count++)
                {
                    statistics.CountPixel();
                    statistics.CountSample(2);
                    statistics.CountSceneRay();
                }
            });

            running.Add(thread);
        }

        running.ForEach(thread => thread.Start());

        gate.Set();

        running.ForEach(thread => thread.Join());

        Assert.AreEqual((long) threads * each, statistics.Pixels);
        Assert.AreEqual((long) threads * each, statistics.Samples);
        Assert.AreEqual((long) threads * each * 2, statistics.PrimaryRays);
        Assert.AreEqual((long) threads * each, statistics.SceneRays);
    }

    [TestMethod]
    public void TestTheTextLineCarriesEveryCount()
    {
        Statistics statistics = new ();

        statistics.CountPixel();
        statistics.CountSample(2);
        statistics.CountSceneRay();

        string text = statistics.AsText();

        // A reader of these lines splits on spaces and then on the equals sign, so every field past
        // the first word has to have exactly one of those and no spaces inside it.
        string[] fields = text.Split(' ');

        Assert.AreEqual("statistics", fields[0]);

        foreach (string field in fields[1..])
            Assert.AreEqual(1, field.Count(character => character == '='), field);

        StringAssert.Contains(text, "pixels=1");
        StringAssert.Contains(text, "samples=1");
        StringAssert.Contains(text, "primaryRays=2");
        StringAssert.Contains(text, "sceneRays=1");
    }
}
