namespace RayTracer.Core;

/// <summary>
/// This class carries the width of a camera's lens and the places across it that rays are taken
/// from.
/// <para>
/// A pinhole camera gathers every ray through a single point, so nothing it sees is ever out of
/// focus.  A real lens gathers across a disc, and the further a thing sits from the distance the
/// lens is focused at, the wider the spot each of its points smears into -- which is depth of
/// field.  This works that out the same way the scene is rendered otherwise: by sampling.  Each
/// ray is fired from one of several places across the disc, all aimed at the same point on the
/// focal plane, and the results averaged.  Whatever lies on that plane is hit by every one of
/// them and stays sharp; whatever does not is hit in a spread of places and blurs.
/// </para>
/// </summary>
public class Lens
{
    private const int DefaultSeed = 0;

    /// <summary>
    /// This property holds the radius of the lens, in the scene's units.  Zero is a pinhole.
    /// </summary>
    public double Aperture { get; }

    /// <summary>
    /// This property holds how far ahead of the camera things are in sharp focus.
    /// </summary>
    public double FocalDistance { get; }

    /// <summary>
    /// This property notes how many places across the lens a ray is taken from.  It is one for a
    /// pinhole, which is what keeps a camera that asked for no blur on exactly the path it was
    /// always on.
    /// </summary>
    public int SampleCount { get; }

    private readonly Lazy<(double X, double Y)[]> _offsets;

    public Lens(double aperture, double focalDistance, int blurSamples = 16, int? seed = null)
    {
        Aperture = aperture;
        FocalDistance = focalDistance;
        SampleCount = aperture > 0 ? Math.Max(1, blurSamples) : 1;

        int chosen = seed ?? DefaultSeed;

        // Built on first use rather than here, since a pinhole never needs it, and lazily so that
        // the parallel scanners may share the one lens safely.
        _offsets = new Lazy<(double X, double Y)[]>(() => BuildOffsets(SampleCount, chosen));
    }

    /// <summary>
    /// This method returns where across the lens the given sample is taken from, as an offset from
    /// the lens's centre in the camera's own left/up directions.
    /// </summary>
    /// <param name="index">Which sample, from zero up to <see cref="SampleCount"/>.</param>
    /// <returns>The offset across the lens, which is the centre itself for a pinhole.</returns>
    public (double X, double Y) OffsetFor(int index) =>
        SampleCount == 1 ? (0, 0) : _offsets.Value[index];

    /// <summary>
    /// This method lays the samples out across the disc.
    /// <para>
    /// Scattering them at random leaves clumps and gaps, and it takes a great many samples before
    /// the grain that causes settles down.  These are spread deliberately instead: one coordinate
    /// steps evenly through the samples, the other follows the radical inverse, which fills in
    /// between what has already been placed rather than landing near it.  Each is then nudged
    /// within its own share of the space so the pattern does not print itself on the image, and
    /// the nudges are drawn once from a seed rather than afresh at every pixel, so a render repeats
    /// exactly from one run to the next.  The pairs are square, and squeezing a square into a
    /// circle by polar coordinates alone would crowd the samples toward the middle, so they are
    /// mapped across concentrically, which keeps them as evenly spread on the disc as they were on
    /// the square.
    /// </para>
    /// </summary>
    private static (double X, double Y)[] BuildOffsets(int count, int seed)
    {
        Random random = new (seed);
        (double X, double Y)[] offsets = new (double, double)[count];
        // One turn of the whole pattern, so two lenses given different seeds do not merely repeat
        // each other's places.
        double turn = random.NextDouble();

        for (int index = 0; index < count; index++)
        {
            double u = (index + random.NextDouble()) / count;
            double v = (RadicalInverse(index) + turn) % 1.0;

            offsets[index] = ToDisc(u * 2 - 1, v * 2 - 1);
        }

        return offsets;
    }

    /// <summary>
    /// This method returns the given index reflected about the binary point -- 1 becomes .1, 2
    /// becomes .01, 3 becomes .11, and so on.  Taken in order, the results never bunch up.
    /// </summary>
    private static double RadicalInverse(int index)
    {
        double result = 0;
        double fraction = 0.5;

        for (uint bits = (uint) index; bits > 0; bits >>= 1)
        {
            result += fraction * (bits & 1);
            fraction *= 0.5;
        }

        return result;
    }

    /// <summary>
    /// This method maps a point on the square running from -1 to 1 each way onto the unit disc,
    /// keeping the spread of points even.  It is Shirley and Chiu's concentric mapping: the square
    /// is taken as four wedges, and a point's larger coordinate gives the radius while the smaller
    /// gives how far around the wedge it lies.
    /// </summary>
    private static (double X, double Y) ToDisc(double a, double b)
    {
        if (a == 0 && b == 0)
            return (0, 0);

        double radius, angle;

        if (a * a > b * b)
        {
            radius = a;
            angle = Math.PI / 4 * (b / a);
        }
        else
        {
            radius = b;
            angle = Math.PI / 2 - Math.PI / 4 * (a / b);
        }

        return (radius * Math.Cos(angle), radius * Math.Sin(angle));
    }
}
