namespace RayTracer.Core;

/// <summary>
/// This class carries the places a camera looks from and the instants it looks at, and hands them
/// out a sample at a time.
/// <para>
/// A camera by default is a pinhole whose shutter does not linger: every ray leaves one point and
/// sees the scene frozen, so nothing is ever out of focus and nothing moving is ever smeared.  Two
/// things spoil that, and a real camera has both.  A lens gathers across a disc, so only what lies
/// at the distance it is focused on stays sharp -- that is depth of field.  A shutter stays open
/// for a while, so anything that moves while it is open is caught everywhere it passed through --
/// that is motion blur.
/// </para>
/// <para>
/// Both are worked out by sampling, and both are drawn from one set of samples rather than two.
/// That matters: a scene wanting sixteen places across the lens and sixteen instants of the shutter
/// wants sixteen rays with a place and an instant apiece, not the two hundred and fifty-six rays
/// every pairing of them would come to.  The pairing buys nothing, since what is being worked out
/// is a single average over both at once.
/// </para>
/// </summary>
public class CameraSampler
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
    /// This property holds how long the shutter stays open, measured against the motions a scene
    /// gives its surfaces: at one, a thing moves the whole of its motion while the shutter is open.
    /// Zero is a shutter that does not linger, so nothing smears.
    /// </summary>
    public double Shutter { get; }

    /// <summary>
    /// This property notes how many samples the camera takes.  It is one when the lens has no
    /// width and the shutter does not linger, which is what keeps a plain camera on exactly the
    /// path it was always on.
    /// </summary>
    public int SampleCount { get; }

    private readonly Lazy<(double X, double Y)[]> _offsets;
    private readonly Lazy<double[]> _times;

    public CameraSampler(
        double aperture, double focalDistance, double shutter = 0, int samples = 16,
        int? seed = null)
    {
        Aperture = aperture;
        FocalDistance = focalDistance;
        Shutter = shutter;
        SampleCount = aperture > 0 || shutter > 0 ? Math.Max(1, samples) : 1;

        int chosen = seed ?? DefaultSeed;

        // Built on first use rather than here, since a plain camera needs neither, and lazily so
        // that the parallel scanners may share the one sampler safely.
        _offsets = new Lazy<(double X, double Y)[]>(() => BuildOffsets(SampleCount, chosen));
        _times = new Lazy<double[]>(() => BuildTimes(SampleCount, chosen));
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
    /// This method returns how far through the shutter's opening the given sample looks, from zero
    /// as it opens to one as it closes.
    /// </summary>
    /// <param name="index">Which sample, from zero up to <see cref="SampleCount"/>.</param>
    /// <returns>How far into its motion a surface has got by the time this sample sees it: from
    /// nothing up to the shutter's own value, so that a shutter of a half catches everything half
    /// way through what it was set to do.  The very start, for a shutter that does not linger.</returns>
    public double TimeFractionFor(int index) =>
        SampleCount == 1 || Shutter <= 0 ? 0 : _times.Value[index] * Shutter;

    /// <summary>
    /// This method lays the instants out across the time the shutter is open, one to each equal
    /// slice of it, so that no stretch of the opening is looked at twice while another is missed.
    /// <para>
    /// The slices are handed out in a shuffled order rather than in turn, and that matters, because
    /// the places across the lens are laid out in index order too.  Taken in step, the samples
    /// early in the opening would all sit on one side of the lens and the late ones on the other,
    /// and a moving thing would smear crookedly rather than along its path.  Shuffling breaks the
    /// tie between where a sample looks from and when it looks.
    /// </para>
    /// </summary>
    private static double[] BuildTimes(int count, int seed)
    {
        // A stream of its own, so that changing one of the two patterns does not shift the other.
        Random random = new (seed + 8191);
        int[] order = Enumerable.Range(0, count).ToArray();

        for (int index = count - 1; index > 0; index--)
        {
            int swap = random.Next(index + 1);

            (order[index], order[swap]) = (order[swap], order[index]);
        }

        return order
            .Select(slice => (slice + random.NextDouble()) / count)
            .ToArray();
    }

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
