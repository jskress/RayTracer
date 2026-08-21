using RayTracer.Scanners;

namespace Tests;

/// <summary>
/// These tests cover how many pixels the parallel scanners work on at once.
/// <para>
/// This is not a detail.  Left to itself, <c>Parallel.For</c> hands the question to the thread pool,
/// and the pool decides by watching its queue: work that sits there undone means, to the pool, a
/// thread stuck waiting on something, so it adds another.  Rendering never waits on anything -- it is
/// arithmetic all the way down -- but a pixel looking into dense foliage can take the better part of a
/// tenth of a second, which looks exactly the same from the outside.  One render measured here had
/// reached 891 threads on twelve cores three quarters of an hour in, and was getting through thirteen
/// pixels a second against the twelve hundred of its first minute.  The same scene at 400x300 took
/// 20m 13s that way against 11m 59s with the threads capped, on identical work -- 2,211,450 scene
/// rays either way, and a pixel-for-pixel identical image.
/// </para>
/// <para>
/// <b>What these tests do not do is reproduce that.</b>  The pool adds threads slowly -- measured at
/// two extra threads over twenty-three seconds of long work items -- so the 891 took the better part
/// of an hour to accumulate, and no test worth running is long enough to see it.  Removing the cap
/// again would leave every one of these passing.  They pin the invariant and they say what it is for;
/// the evidence that it matters is the pair of measurements above, not a red test.
/// </para>
/// </summary>
[TestClass]
public class TestScanners
{
    /// <summary>
    /// This class watches how many threads are inside the action at once.
    /// </summary>
    private sealed class Crowd
    {
        private int _inside;
        private int _peak;

        public int Peak => Volatile.Read(ref _peak);

        public void Enter()
        {
            int now = Interlocked.Increment(ref _inside);
            int was = Volatile.Read(ref _peak);

            while (now > was)
            {
                int seen = Interlocked.CompareExchange(ref _peak, now, was);

                if (seen == was)
                    break;

                was = seen;
            }
        }

        public void Leave()
        {
            Interlocked.Decrement(ref _inside);
        }
    }

    /// <summary>
    /// This method scans a small image with an action slow enough that the threads working on it
    /// overlap, which is the only condition under which the count means anything.
    /// </summary>
    private static int PeakThreadsFor(IScanner scanner, int width, int height)
    {
        Crowd crowd = new ();

        scanner.Scan(width, height, (_, _) =>
        {
            crowd.Enter();

            // A pixel that takes a while is exactly what provokes the thread pool into adding
            // threads, so the test has to have some.
            Thread.Sleep(2);

            crowd.Leave();
        });

        return crowd.Peak;
    }

    [TestMethod]
    public void TestThePixelScannerKeepsToOneThreadPerProcessor()
    {
        int peak = PeakThreadsFor(new PixelParallelScanner(), 60, 40);

        Assert.IsTrue(peak <= Environment.ProcessorCount,
            $"{peak} pixels were in flight at once on {Environment.ProcessorCount} processors");
    }

    [TestMethod]
    public void TestTheLineScannerKeepsToOneThreadPerProcessor()
    {
        int peak = PeakThreadsFor(new LineParallelScanner(), 12, 40);

        Assert.IsTrue(peak <= Environment.ProcessorCount,
            $"{peak} lines were in flight at once on {Environment.ProcessorCount} processors");
    }

    [TestMethod]
    public void TestEveryPixelIsStillVisitedExactlyOnce()
    {
        // Capping the threads must not cost any pixels, and a scanner that visited one twice would
        // report progress that overshot its own total.
        const int width = 37;
        const int height = 23;

        int[] counts = new int[width * height];

        new PixelParallelScanner().Scan(width, height,
            (x, y) => Interlocked.Increment(ref counts[y * width + x]));

        Assert.IsTrue(counts.All(count => count == 1),
            $"{counts.Count(count => count != 1)} pixels were not visited exactly once");
    }

    [TestMethod]
    public void TestTheSingleThreadedScannerStaysSingleThreaded()
    {
        Assert.AreEqual(1, PeakThreadsFor(new SingleThreadScanner(), 12, 8));
    }
}
