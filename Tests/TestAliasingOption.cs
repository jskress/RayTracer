using RayTracer.Core;
using RayTracer.General;
using RayTracer.Pixels;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover how anti-aliasing is asked for, and in particular the threshold — how far two
/// samples within a pixel must disagree before the sampler looks closer.
/// <para>
/// That number was a constant written into the sampler where nobody could reach it, and it is worth
/// reaching: on a scene with fine detail in it the default is conservative.  Measured on a sea of
/// isosurface waves at 300x225, `adaptive:3` took 3m18s at 16.86 samples a pixel; the same scene at
/// `adaptive:3:0.35` took 1m30s at 7.83 samples, and the two pictures differ by a mean of a quarter of
/// one level in 255, with a third of a percent of pixels differing by more than eight.
/// </para>
/// </summary>
[TestClass]
public class TestAliasingOption
{
    private static AliasingOption Configured(string text)
    {
        AliasingOption option = new ();

        option.Configure(text);

        return option;
    }

    [TestMethod]
    public void TestEveryFormThatWorkedBeforeStillWorks()
    {
        // Nothing written before the threshold existed may change meaning.
        Assert.AreEqual("off", Configured("off").ToString());
        Assert.AreEqual("adaptive:5", Configured("adaptive").ToString());
        Assert.AreEqual("adaptive:3", Configured("adaptive:3").ToString());
        Assert.AreEqual("adaptive:4", Configured("4").ToString());
        Assert.AreEqual("adaptive:5", Configured("").ToString());
    }

    [TestMethod]
    public void TestAThresholdMayBeGivenAfterTheDepth()
    {
        Assert.AreEqual("adaptive:3:0.35", Configured("adaptive:3:0.35").ToString());
        Assert.AreEqual("adaptive:5:0.2", Configured("adaptive:5:0.2").ToString());
    }

    [TestMethod]
    public void TestTheUsualThresholdIsNotWrittenBackOut()
    {
        // Saying the default out loud should read back as though it had not been said, so that the
        // common case round-trips to the shorter form.
        Assert.AreEqual("adaptive:3", Configured("adaptive:3:0.1").ToString());
    }

    [TestMethod]
    public void TestAThresholdOutsideItsRangeIsRefused()
    {
        // Nought would mean any difference at all is worth subdividing, which recurses to the depth
        // limit on every pixel of every scene; past one, no two colors can differ enough to subdivide
        // at all, which is anti-aliasing that quietly does nothing.
        foreach (string bad in new[] { "adaptive:3:0", "adaptive:3:-0.2", "adaptive:3:1.5", "adaptive:3:wide" })
            Assert.ThrowsExactly<ArgumentException>(() => Configured(bad), $"'{bad}' should be refused");
    }

    [TestMethod]
    public void TestADepthIsStillCheckedToo()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Configured("adaptive:deep"));
        Assert.ThrowsExactly<ArgumentException>(() => Configured("adaptive:-1"));
        Assert.ThrowsExactly<ArgumentException>(() => Configured("sideways"));
    }

    [TestMethod]
    public void TestARaisedThresholdReallyDoesTakeFewerSamples()
    {
        // The point of the whole thing, and the only test here that would notice if the number were
        // parsed perfectly and then never used.
        long tight = SamplesTaken("adaptive:3:0.02");
        long loose = SamplesTaken("adaptive:3:0.9");

        Assert.IsTrue(tight > loose,
            $"a tight threshold took {tight} samples and a loose one {loose}");

        // And the tight one must actually be subdividing, or this is comparing two of nothing.
        Assert.IsTrue(tight > 121 * 5,
            $"{tight} samples over 121 pixels is not enough subdivision to be measuring anything");
    }

    /// <summary>
    /// Renders a small scene and reports how many places within pixels were looked at.
    /// </summary>
    private static long SamplesTaken(string aliasing)
    {
        Scene scene = TestScenes.DefaultScene();
        Camera camera = new ();
        Statistics statistics = new ();
        RenderContext context = new ()
        {
            Width = 11,
            Height = 11,
            AntiAliasing = Configured(aliasing),
            Statistics = statistics
        };

        camera.Render(context, scene);

        return statistics.Samples;
    }
}
