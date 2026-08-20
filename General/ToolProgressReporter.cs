using System.Diagnostics;
using RayTracer.Renderer;

namespace RayTracer.General;

/// <summary>
/// This class reports progress as whole lines of key/value text, for a program to read rather than a
/// person.
/// <para>
/// Each line is complete and newline-terminated, carries no colour and no cursor movement, and is
/// flushed as soon as it is written, so it survives a pipe.  A line is written on a fixed interval
/// rather than at fixed fractions of the way through, and that choice is the point of the whole
/// class: on an interval, a render that has wedged stops producing lines, and one that is merely slow
/// goes on producing them with the counts barely moving.  At fixed fractions those two look exactly
/// the same -- silence -- which is the question a tool most needs answered.
/// </para>
/// </summary>
public class ToolProgressReporter : IProgressReporter
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    private readonly Stopwatch _clock = new ();
    private readonly Statistics _statistics;

    private long _total;
    private long _current;
    private long _nextReport;

    /// <summary>
    /// Creates a reporter, optionally alongside the statistics being gathered so its lines can carry
    /// the counts as well as the count of pixels.
    /// </summary>
    /// <param name="statistics">The statistics to report from, or null.</param>
    public ToolProgressReporter(Statistics statistics = null)
    {
        _statistics = statistics;
    }

    /// <inheritdoc />
    public void SetTotal(long total)
    {
        // The clock starts here rather than when the reporter was made, because the elapsed time on
        // these lines is meant to be the render's, and there can be a good deal of parsing and
        // geometry building between the two.  Timing from construction also produced a burst of
        // nonsense at the start: the first interval had already gone by before the first pixel, so
        // several threads each claimed a tick that was overdue and wrote a line about four pixels
        // with an estimate of two hundred hours.
        _total = total;
        _current = 0;
        _nextReport = (long) Interval.TotalMilliseconds;

        _clock.Restart();

        // How long the process took to get here -- loading, jitting, parsing the scene file and
        // building its geometry -- reported once, on the opening line.  It is the fixed cost of an
        // invocation, which is the number worth having when weighing whether a batch of scenes would
        // be better rendered by one process than by many.
        Write("start", setup: SecondsSinceTheProcessStarted());
    }

    /// <summary>
    /// This method works out how long ago this process began.
    /// </summary>
    private static double SecondsSinceTheProcessStarted()
    {
        try
        {
            return (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds;
        }
        catch (Exception)
        {
            // Asking the operating system about our own process is not guaranteed to work, and a
            // number missing from a progress line is not worth failing a render over.
            return -1;
        }
    }

    /// <inheritdoc />
    public void Bump()
    {
        long current = Interlocked.Increment(ref _current);
        long elapsed = _clock.ElapsedMilliseconds;

        if (elapsed < Interlocked.Read(ref _nextReport))
            return;

        // Whichever thread gets here first claims the next slot and writes; the rest carry on.  A
        // missed tick matters far less than holding up a scanner thread.
        //
        // The next slot is set an interval on from *now* rather than from the slot just claimed, so
        // that a gap longer than the interval costs one line rather than being made up for with a
        // burst of them.
        long due = Interlocked.Read(ref _nextReport);

        if (Interlocked.CompareExchange(
                ref _nextReport, elapsed + (long) Interval.TotalMilliseconds, due) == due)
            Write("progress", current);
    }

    /// <inheritdoc />
    public void Done()
    {
        Write("done", Interlocked.Read(ref _current));
    }

    /// <summary>
    /// Writes one line.  Elapsed time comes first on every line so that a reader watching for a
    /// stall has the same field in the same place whatever the line says.
    /// </summary>
    private void Write(string what, long? current = null, double? setup = null)
    {
        List<string> parts =
        [
            what,
            $"elapsed={_clock.Elapsed.TotalSeconds:F1}"
        ];

        if (setup is >= 0)
            parts.Add($"setup={setup:F2}");

        if (current is not null && _total > 0)
        {
            parts.Add($"pixels={current}/{_total}");
            parts.Add($"done={(double) current / _total:F4}");

            if (current > 0)
            {
                double remaining = _clock.Elapsed.TotalSeconds * (_total - current.Value) / current.Value;

                parts.Add($"eta={remaining:F1}");
            }
        }
        else if (_total > 0)
            parts.Add($"pixels=0/{_total}");

        if (_statistics is not null)
        {
            parts.Add($"samples={_statistics.Samples}");
            parts.Add($"sceneRays={_statistics.SceneRays}");
        }

        Terminal.OutLine(string.Join(" ", parts));
    }
}
