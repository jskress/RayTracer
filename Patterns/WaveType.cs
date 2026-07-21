namespace RayTracer.Patterns;

/// <summary>
/// This enumeration names the ways a pattern's value may be shaped once the pattern has produced
/// it.  A pattern says how far through its range a point lies; the wave decides what that number
/// then does on its way to the colour map, and so decides whether a boundary arrives as a hard
/// edge, a gentle swell, or something that rises and falls twice over.
/// </summary>
public enum WaveType
{
    /// <summary>
    /// The value is left exactly as the pattern produced it, sweeping once from one end of its
    /// range to the other and snapping back.  This is the default, and what every pattern did
    /// before there was anything else to choose.
    /// </summary>
    Ramp,

    /// <summary>
    /// The value swells and falls away smoothly, as a sine does.  It is what turns the hard return
    /// of a ramp into something that eases in and out, and is the usual choice for veining.
    /// </summary>
    Sine,

    /// <summary>
    /// The value climbs to the middle and falls back evenly, so a band and its neighbour mirror one
    /// another rather than repeating.
    /// </summary>
    Triangle,

    /// <summary>
    /// The value rises and falls twice across the range, giving a scalloped edge.
    /// </summary>
    Scallop,

    /// <summary>
    /// The value follows a smoothstep, flattening at both ends so that bands meet gently rather
    /// than at a corner.
    /// </summary>
    Cubic,

    /// <summary>
    /// The value is raised to a power, which pushes it toward one end of its range.  The exponent
    /// says how hard.
    /// </summary>
    Poly
}
