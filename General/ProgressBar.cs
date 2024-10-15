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

        Show(true);
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
        double elapsed = ticks - _start;
        double todo = _total - _current;
        long ticksLeft = Convert.ToInt64(elapsed / _current * todo);

        return TimeSpan.FromTicks(ticksLeft).Add(TimeSpan.FromSeconds(1));
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
