namespace RayTracer.Pigments;

/// <summary>
/// This record holds how a picture laid without repeating is cut up and shuffled about: how far apart the middles of
/// its tiles stand, as a share of the picture, and how far, in radians either way, each tile may be turned.  See
/// <see cref="ImageTaps"/>.
/// </summary>
/// <param name="Size">How far apart neighboring tiles' middles stand, as a share of the picture.</param>
/// <param name="Turn">How far each tile may be turned either way, in radians.</param>
public sealed record Shuffle(double Size, double Turn)
{
    /// <summary>
    /// This is how far apart tiles stand unless told: as Mikkelsen's hex-tiling sets them, about 0.29 of the picture,
    /// so that each tile shows a patch of it big enough to keep its features whole and small enough to break up any
    /// repeat.
    /// </summary>
    public const double DefaultSize = 0.28867513459481287;
}
