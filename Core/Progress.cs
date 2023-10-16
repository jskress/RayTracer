namespace RayTracer.Core;

internal class Progress
{
    private readonly long _total;

    private long _count;
    private long _percent;

    internal Progress(long total)
    {
        _total = total;
        _count = 0;
        _percent = 0;
    }

    internal void Bump()
    {
        Interlocked.Increment(ref _count);

        long newPercent = _count * 100 / _total;

        if (newPercent > _percent)
        {
            _percent = newPercent;

            Show();
        }
    }

    private void Show()
    {
        Console.CursorLeft = 0;
        Console.Write($"{_percent}%");
    }
}
