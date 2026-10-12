namespace RayTracer.Core;

/// <summary>
/// This class tallies how much of a shadow ray's light gets past the strands of hair it has met so far.
/// <para>
/// **A low light across a rug's pile meets thousands of strands on the way**, and a shadow ray finds
/// every crossing before any of them is counted.  What gets past is the product of what each lets by,
/// which does not care what order the strands are found in, so the strands themselves keep the tally as
/// they are found and stop looking once next to nothing is left: past that, the light is taken to be
/// stopped, as it all but is.
/// </para>
/// <para>
/// What a strand lets by depends on what is being lit.  Another strand is lit only by the light no strand
/// stopped, so a strand in the way passes the share of the ray it does not cover; that is the tally's
/// own reckoning.  A surface beneath is lit by what strands send onward as well, which the scene works out
/// and hands the tally as its brightest band -- never less than any band gets past, so the light is never
/// taken to be stopped while any of it still arrives.
/// </para>
/// </summary>
public sealed class StrandTally
{
    /// <summary>
    /// How little of the light may be left before it is taken to be stopped.
    /// </summary>
    public const double Enough = 1e-4;

    private readonly Func<HairIntersection, double> _passedBy;

    /// <summary>
    /// This creates a tally.
    /// </summary>
    /// <param name="passedBy">How much of the light a strand in the way lets by, at the most; unsaid, the
    /// share of the ray it does not cover.</param>
    public StrandTally(Func<HairIntersection, double> passedBy = null)
    {
        _passedBy = passedBy;
    }

    /// <summary>
    /// This property holds how much of the light gets past the strands met so far, at the most.
    /// </summary>
    public double Left { get; private set; } = 1;

    /// <summary>
    /// This property reports whether so little gets past that the light is taken to be stopped.
    /// </summary>
    public bool Stopped => Left < Enough;

    /// <summary>
    /// This method counts one more strand in the way.
    /// </summary>
    /// <param name="strand">The strand's crossing.</param>
    public void Pass(HairIntersection strand)
    {
        Left *= _passedBy?.Invoke(strand) ?? (strand.Coverage >= 1 ? 0 : 1 - strand.Coverage);
    }
}
