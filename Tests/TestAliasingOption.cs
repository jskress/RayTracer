using System.Globalization;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
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

    /// <summary>
    /// A threshold is written with a point wherever the render runs: a machine set to write a comma in its numbers
    /// read "0.35" as thirty-five, and refused it.
    /// </summary>
    [TestMethod]
    public void TestAThresholdIsReadTheSameInEveryLocale()
    {
        CultureInfo was = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.AreEqual("adaptive:3:0.35", Configured("adaptive:3:0.35").ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = was;
        }
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
    /// A square the sampler looks closer at shares its middle with the corner of the square it subdivides, so it
    /// traces four new places rather than five: a pixel on a ball's edge, looked at to one level, takes five places
    /// and four more for each corner it looks closer at.  (Traced again, that middle was a fifth of every
    /// subdivision's rays, and taken five at a time the count came out a multiple of five over the first five.)
    /// </summary>
    [TestMethod]
    public void TestASubdivisionDoesNotTraceItsMiddleAgain()
    {
        Scene scene = TestScenes.DefaultScene();
        // The default scene's ball, looked at from where its edge falls in the middle of one tiny pixel.
        Camera camera = new () { LookAt = new Point(1.0206, 0, 0), FieldOfView = 0.5.ToRadians() };
        Statistics statistics = new ();
        RenderContext context = new ()
        {
            Width = 1, Height = 1, AntiAliasing = Configured("adaptive:1:0.02"), Statistics = statistics
        };

        camera.Render(context, scene);

        long more = statistics.Samples - 5;

        Assert.IsTrue(more > 0, "the pixel on the ball's edge should have been looked at closer");
        Assert.AreEqual(0, more % 4, $"{statistics.Samples} places: four new for each corner looked at closer");
        Assert.AreNotEqual(0, more % 5, $"{statistics.Samples} places: the middle of each was traced again");
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
