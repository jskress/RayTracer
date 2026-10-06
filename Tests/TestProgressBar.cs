using RayTracer.General;
using RayTracer.Options;
using RayTracer.Renderer;

namespace Tests;

[TestClass]
public class TestProgressBar
{
    [TestMethod]
    public void TestFullLifecycleDoesNotThrow()
    {
        ProgressBar bar = new ();

        bar.SetTotal(10);

        for (int index = 0; index < 10; index++)
            bar.Bump();

        bar.Done();
    }

    [TestMethod]
    public void TestTheBarIsOnlyDrawnWhenItMoves()
    {
        // The bar is fifty characters wide, so two hundred bumps can move it fifty times at the very
        // most.  Bumping used to ask for the line to be rewritten regardless of whether anything had
        // changed, which for a real image meant a rewrite per pixel -- measured at 450,000 rewrites
        // and 54MB of terminal writes for one 1400x1050 render, all of it serialized through the one
        // lock that draws, and so through every thread doing the rendering.  That render took 13.2
        // seconds; once the drawing was asked for only when the bar had actually moved, it took 3.5.
        StringWriter captured = new ();
        TextWriter was = Console.Out;
        ProgressBar bar = new ();

        Console.SetOut(captured);

        try
        {
            bar.SetTotal(200);

            // Nothing is drawn until the bar has waited out its threshold, so watching it draw means
            // waiting too.
            Thread.Sleep(2_100);

            for (int index = 0; index < 200; index++)
                bar.Bump();

            bar.Done();
        }
        finally
        {
            Console.SetOut(was);
        }

        string drawn = captured.ToString();
        int redraws = drawn.Count(character => character == ']');

        Assert.IsTrue(redraws is > 0 and <= 60,
            $"the bar was drawn {redraws} times for 200 bumps");

        // And it finished full, which is what Done() is for.
        Assert.Contains(new string('=', 50), drawn, "the bar did not end up full");

        // A finished bar has no time left to report.  The estimate rounds up by a second so that a
        // render still working never claims to be done, which used to leave the finished bar sitting
        // there promising one more second of a render that had already ended.
        string finished = drawn[drawn.LastIndexOf(']')..];

        Assert.Contains("00:00:00", finished,
            $"the finished bar still had time to go: {finished.Trim()}");
    }

    [TestMethod]
    public void TestZeroTotalDoneDoesNotThrow()
    {
        // Done() unconditionally sets _current = _total and _used = 50, so it must not
        // divide by the (zero) total the way Bump() would.
        ProgressBar bar = new ();

        bar.SetTotal(0);
        bar.Done();
    }

    [TestMethod]
    public void TestReusingAfterDoneDoesNotThrow()
    {
        ProgressBar bar = new ();

        bar.SetTotal(5);
        bar.Bump();
        bar.Done();

        bar.SetTotal(3);
        bar.Bump();
        bar.Bump();
        bar.Done();
    }

    /// <summary>
    /// `quiet` says nothing until the render is done, and the bar is only the default, not something
    /// asked for; it used to draw anyway once a render ran past its two seconds.
    /// </summary>
    [TestMethod]
    public void TestQuietDrawsNoBar()
    {
        Assert.IsNull(ImageRenderer.ProgressReporterFor(Options("quiet", "bar"), new Statistics()));
    }

    /// <summary>
    /// Every other output level draws the bar, as it always has.
    /// </summary>
    [TestMethod]
    public void TestTheOtherLevelsDrawTheBar()
    {
        foreach (string level in new[] { "normal", "chatty", "verbose" })
        {
            Assert.IsInstanceOfType<ProgressBar>(
                ImageRenderer.ProgressReporterFor(Options(level, "bar"), new Statistics()), level);
        }
    }

    /// <summary>
    /// The tool style is asked for by name, by a program that wants its lines, so `quiet` leaves it
    /// reporting; and `none` reports nothing whatever the level.
    /// </summary>
    [TestMethod]
    public void TestQuietLeavesTheToolStyleAndNoneAlone()
    {
        Assert.IsInstanceOfType<ToolProgressReporter>(
            ImageRenderer.ProgressReporterFor(Options("quiet", "tool"), new Statistics()));
        Assert.IsNull(ImageRenderer.ProgressReporterFor(Options("normal", "none"), new Statistics()));
    }

    private static RenderOptions Options(string level, string style)
    {
        return new RenderOptions { OutputLevelText = level, ProgressStyleText = style };
    }
}
