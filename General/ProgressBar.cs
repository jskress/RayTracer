namespace RayTracer.General;

/// <summary>
/// This class provides a progress bar.  It only displays if rendering takes longer than
/// a defined threshold.
/// </summary>
public class ProgressBar
{
    private static readonly TimeSpan Threshold = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    private Timer _timer;
    private long _start;
    private long _threshold;
    private long _current;
    private long _total;
    private int _used;
    private int _lastUsed;
    private bool _cursorHidden;
    private bool _interrupted;
    private ConsoleCancelEventHandler _onInterrupt;

    /// <summary>
    /// This method is used to set the total count the progress bar should expect.  This
    /// has the effect of setting the current count to zero.
    /// </summary>
    /// <param name="total">The total count to expect.</param>
    public void SetTotal(long total)
    {
        DateTime now = DateTime.Now;

        _timer = new Timer(_ => Show(true), null, Threshold, OneSecond);
        _start = now.Ticks;
        _threshold = now.Add(Threshold).Ticks;
        _current = 0;
        _total = total;
        _used = 0;
        _lastUsed = 0;
        _interrupted = false;

        ClearLine();
    }

    /// <summary>
    /// This method is used to bump the progress bar.
    /// </summary>
    public void Bump()
    {
        lock (this)
        {
            _current++;
            _used = (int) (_current * 50 / _total);
        }

        // A pixel finishing is only worth drawing if it moved the bar, which one pixel in tens of
        // thousands does.  Asking for the drawing to happen regardless -- as this used to -- rewrote
        // the whole line once per pixel: a million times over for a large image, tens of megabytes of
        // terminal writes, all of it serialized through this one lock and so through every thread
        // doing the rendering.  The clock still asks unconditionally once a second, which is what
        // keeps the estimate moving while the bar itself stands still.
        Show(false);
    }

    /// <summary>
    /// This method is used to indicate that we are done.
    /// </summary>
    public void Done()
    {
        _current = _total;
        _used = 50;
        _lastUsed = _used - 1;
        _timer.Change(-1, -1);
        _timer.Dispose();

        Show(false);

        if (Console.CursorLeft > 0)
            Console.WriteLine();

        ShowCursor();
    }

    /// <summary>
    /// This method shows the current state of the progress bar.
    /// </summary>
    private void Show(bool fromClock)
    {
        long now = DateTime.Now.Ticks;

        lock (this)
        {
            // Only show the progress bar if, we've past our time threshold.
            if (now > _threshold && (_used != _lastUsed || fromClock))
            {
                // The bar is drawn by walking back to the start of the line and writing over it in
                // several pieces, once for each color it uses.  The terminal draws its cursor
                // wherever each piece leaves it, so with the cursor showing, the line appears to
                // flicker as it is rewritten.  It is put away before the first piece and left away
                // for as long as the bar owns the line, rather than being restored between passes,
                // which would only trade a flicker for a blink.
                HideCursor();

                ConsoleColor hold = Console.ForegroundColor;

                Console.CursorLeft = 0;
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write('[');

                if (_used > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;

                    if (_current >= _total)
                        Console.Write(new string('=', 50));
                    else if (_used > 1)
                    {
                        Console.Write(new string('=', _used - 1));
                        Console.Write('>');
                    }
                }

                Console.ForegroundColor = hold;
                Console.Write(new string(' ', 50 - _used));

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write(']');
                Console.ForegroundColor = hold;
                Console.Write($@" {TimeRemaining(now):hh\:mm\:ss} ");

                _lastUsed = _used;
            }
        }
    }

    /// <summary>
    /// This method determines an estimate as to the amount of time remaining for the render.
    /// </summary>
    /// <param name="ticks">The current time in ticks.</param>
    /// <returns>The time remaining estimate.</returns>
    private TimeSpan TimeRemaining(long ticks)
    {
        // No progress has been recorded yet, so there's nothing to base an estimate on.
        if (_current == 0)
            return TimeSpan.Zero;

        double elapsed = ticks - _start;
        double todo = _total - _current;

        // Nothing left to do is no time left to wait, and saying so is the whole of what makes the
        // finished bar read as finished.  The rounded-up second below exists so that a render with
        // work still in it never claims to be done -- a fraction of a second left shows as one
        // second, not as none -- and it has no business here, where there really is none left.
        if (todo <= 0)
            return TimeSpan.Zero;

        long ticksLeft = Convert.ToInt64(elapsed / _current * todo);

        return TimeSpan.FromTicks(ticksLeft).Add(TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// This method puts the cursor away while the progress bar has the line, and arranges to put it
    /// back if the user gives up on the render.
    /// <para>
    /// That last part is not a nicety.  The runtime does not restore the cursor when a program ends
    /// -- it writes the sequence that hides one and nothing more -- so a render stopped with a
    /// keystroke would otherwise leave the terminal with no cursor at all, long after the render that
    /// took it away is gone.
    /// </para>
    /// <para>
    /// Note that the cursor is only ever set, never asked about: reading
    /// <c>Console.CursorVisible</c> throws on everything but Windows.  Setting it is safe everywhere,
    /// and writes nothing at all when the output is not a terminal.
    /// </para>
    /// </summary>
    private void HideCursor()
    {
        if (_cursorHidden || _interrupted)
            return;

        // Marking the render given up on before handing the cursor back is what keeps a draw that
        // races the keystroke from taking it away again a moment later.
        _onInterrupt = (_, _) =>
        {
            _interrupted = true;

            ShowCursor();
        };

        Console.CancelKeyPress += _onInterrupt;
        Console.CursorVisible = false;

        _cursorHidden = true;
    }

    /// <summary>
    /// This method gives the cursor back, once the progress bar is finished with the line.
    /// </summary>
    private void ShowCursor()
    {
        if (!_cursorHidden)
            return;

        _cursorHidden = false;

        Console.CancelKeyPress -= _onInterrupt;
        Console.CursorVisible = true;
    }

    /// <summary>
    /// This method is used to clear the current line.
    /// </summary>
    private static void ClearLine()
    {
        int pos = Console.CursorLeft;

        if (pos > 0)
        {
            Console.CursorLeft = 0;
            Console.Write(new string(' ', pos));
            Console.CursorLeft = 0;
        }
    }
}
