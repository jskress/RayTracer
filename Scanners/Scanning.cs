namespace RayTracer.Scanners;

/// <summary>
/// This class holds what the parallel scanners have in common.
/// </summary>
internal static class Scanning
{
    /// <summary>
    /// This field holds the parallel options every scanner uses, and exists for the one setting on
    /// them that matters: how many threads at once.
    /// <para>
    /// Left to itself, <c>Parallel.For</c> puts no limit on that and leans on the thread pool to work
    /// out a sensible number.  The pool works it out by watching its queue, and adds a thread whenever
    /// work seems to be sitting there undone -- a rule written for work that waits on something, and
    /// quite wrong for work that is simply long.  A pixel looking into dense foliage can take the
    /// better part of a tenth of a second, which to the pool is indistinguishable from a thread stuck
    /// on a socket, so it adds another thread, and another.  One render measured here had reached
    /// <b>891 threads on twelve cores</b> three quarters of an hour in, by which point it was getting
    /// through thirteen pixels a second against the twelve hundred it managed in its first minute --
    /// the same work per pixel throughout, with almost all of the machine going into changing context
    /// rather than tracing rays.
    /// </para>
    /// <para>
    /// A ray tracer never waits for anything.  Every pixel is pure arithmetic, so the useful number of
    /// threads is the number of processors, and any more than that can only take cache away from the
    /// ones already working.
    /// </para>
    /// </summary>
    internal static readonly ParallelOptions Options = new ()
    {
        MaxDegreeOfParallelism = Environment.ProcessorCount
    };
}
