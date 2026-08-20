using RayTracer.General;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the progress style meant to be read by a program rather than a person.
/// <para>
/// What is being tested here is mostly a set of promises about the <em>shape</em> of the output, since
/// that shape is the only thing a reader on the other end of a pipe has to work with.  A bar drawn
/// with carriage returns and color is unreadable there, so the test that matters most is the one
/// saying this output has neither.
/// </para>
/// </summary>
[TestClass]
public class TestToolProgressReporter
{
    /// <summary>
    /// This method runs a little render past the reporter and hands back the lines it wrote.
    /// </summary>
    private static string[] Watch(Action<ToolProgressReporter> render, Statistics statistics = null)
    {
        StringWriter captured = new ();
        TextWriter was = Console.Out;
        ToolProgressReporter reporter = new (statistics);

        Console.SetOut(captured);

        try
        {
            render(reporter);
        }
        finally
        {
            Console.SetOut(was);
        }

        return captured.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    [TestMethod]
    public void TestNothingIsWrittenThatAPipeWouldSwallow()
    {
        // The whole point of the style.  Escape sequences and carriage returns are what make the
        // progress bar unreadable to anything but a terminal, so the lines are held to carrying no
        // control character at all rather than to being free of any particular one.
        string[] lines = Watch(reporter =>
        {
            reporter.SetTotal(100);

            for (int index = 0; index < 100; index++)
                reporter.Bump();

            reporter.Done();
        });

        foreach (string line in lines)
        {
            Assert.IsFalse(line.Any(char.IsControl),
                $"a control character reached the output in '{line}'");
        }
    }

    [TestMethod]
    public void TestEveryLineIsAWordFollowedByKeyValuePairs()
    {
        string[] lines = Watch(reporter =>
        {
            reporter.SetTotal(10);
            reporter.Bump();
            reporter.Done();
        });

        Assert.IsTrue(lines.Length >= 2, "there should be a line to begin with and one to end with");

        foreach (string line in lines)
        {
            string[] fields = line.Split(' ');

            Assert.IsFalse(fields[0].Contains('='), $"'{line}' does not begin with a word");

            foreach (string field in fields[1..])
            {
                Assert.AreEqual(1, field.Count(character => character == '='),
                    $"'{field}' in '{line}' is not one key and one value");
            }
        }
    }

    [TestMethod]
    public void TestARenderTooShortToNeedProgressStillSaysItBeganAndEnded()
    {
        // The bar shows nothing at all for a render under its threshold, which is right for a person
        // and wrong for a program: a reader with no line to read cannot tell a render that finished
        // in a moment from one that never started.
        string[] lines = Watch(reporter =>
        {
            reporter.SetTotal(4);
            reporter.Bump();
            reporter.Bump();
            reporter.Bump();
            reporter.Bump();
            reporter.Done();
        });

        Assert.AreEqual(2, lines.Length);
        StringAssert.StartsWith(lines[0], "start ");
        StringAssert.StartsWith(lines[1], "done ");
        StringAssert.Contains(lines[1], "pixels=4/4");
        StringAssert.Contains(lines[1], "done=1.0000");
    }

    [TestMethod]
    public void TestTheElapsedTimeIsTheSecondFieldOfEveryLine()
    {
        // A reader watching for a stall wants one field, in one place, whatever the line says.
        string[] lines = Watch(reporter =>
        {
            reporter.SetTotal(2);
            reporter.Bump();
            reporter.Done();
        });

        foreach (string line in lines)
            StringAssert.StartsWith(line.Split(' ')[1], "elapsed=", line);
    }

    [TestMethod]
    public void TestProgressIsReportedOnTheClockRatherThanByFractionsDone()
    {
        // This is the difference that lets a reader tell slow from stuck.  Lines keyed to fractions of
        // the work go quiet when the work goes quiet, and so say nothing at all about whether the
        // render is still alive; lines keyed to the clock keep coming with the numbers standing still.
        string[] lines = Watch(reporter =>
        {
            reporter.SetTotal(1_000_000);

            // Barely any of the work, but plenty of the interval.
            reporter.Bump();

            Thread.Sleep(2_100);

            reporter.Bump();
            reporter.Done();
        });

        string[] progress = lines
            .Where(line => line.StartsWith("progress "))
            .ToArray();

        Assert.AreEqual(1, progress.Length, string.Join(" | ", lines));
        StringAssert.Contains(progress[0], "pixels=2/1000000");

        // Two millionths of the way through after two seconds is a very long render, and saying so is
        // the estimate doing its job rather than failing at it.
        StringAssert.Contains(progress[0], "eta=");
    }

    [TestMethod]
    public void TestTimeSpentBeforeTheFirstPixelIsNotChargedToTheRender()
    {
        // Found in the wild, on a scene that spends six seconds building trees before it renders
        // anything.  The clock used to start when the reporter was made, so by the time the first
        // pixel arrived the reporting interval was already several intervals overdue -- and each
        // thread that noticed claimed one of them, so the render opened with a burst of lines about
        // four pixels apiece, estimating two hundred hours.
        StringWriter captured = new ();
        TextWriter was = Console.Out;
        ToolProgressReporter reporter = new ();

        Console.SetOut(captured);

        try
        {
            // Stand in for the parsing and the geometry building, which happen before a render can
            // say how many pixels it is going to have.
            Thread.Sleep(2_500);

            reporter.SetTotal(480_000);

            for (int index = 0; index < 5_000; index++)
                reporter.Bump();

            reporter.Done();
        }
        finally
        {
            Console.SetOut(was);
        }

        string[] lines = captured.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.AreEqual(2, lines.Length, string.Join(" | ", lines));

        // And the elapsed time is the render's own, not the render's plus the waiting.
        double elapsed = double.Parse(lines[1].Split(' ')[1]["elapsed=".Length..]);

        Assert.IsTrue(elapsed < 1, $"the render was charged {elapsed} seconds it did not spend");
    }

    [TestMethod]
    public void TestTheClosingLineSaysHowFarItActuallyGot()
    {
        // Done() is called from a finally, so it is also what gets called when a render is abandoned
        // part way through.  Reporting the count reached rather than the total is what lets a reader
        // tell those two apart.
        string[] lines = Watch(reporter =>
        {
            reporter.SetTotal(1000);

            for (int index = 0; index < 250; index++)
                reporter.Bump();

            reporter.Done();
        });

        string done = lines.Last();

        StringAssert.StartsWith(done, "done ");
        StringAssert.Contains(done, "pixels=250/1000");
        StringAssert.Contains(done, "done=0.2500");
    }

    [TestMethod]
    public void TestTheCountsRideAlongWhenThereAreSomeToReport()
    {
        Statistics statistics = new ();

        statistics.CountSample(1);
        statistics.CountSceneRay();
        statistics.CountSceneRay();
        statistics.CountSceneRay();

        string[] lines = Watch(reporter =>
        {
            reporter.SetTotal(1);
            reporter.Bump();
            reporter.Done();
        }, statistics);

        StringAssert.Contains(lines.Last(), "samples=1");
        StringAssert.Contains(lines.Last(), "sceneRays=3");
    }

    [TestMethod]
    public void TestNoCountsAreClaimedWhenNoneAreBeingGathered()
    {
        string[] lines = Watch(reporter =>
        {
            reporter.SetTotal(1);
            reporter.Bump();
            reporter.Done();
        });

        foreach (string line in lines)
        {
            Assert.IsFalse(line.Contains("samples="), line);
            Assert.IsFalse(line.Contains("sceneRays="), line);
        }
    }
}
