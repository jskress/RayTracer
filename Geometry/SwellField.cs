using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Fields;

namespace RayTracer.Geometry;

/// <summary>
/// This class works out the shape of a body of water made of one or more wave trains, and is where
/// all the arithmetic of that shape lives.
/// <para>
/// **The waves are trochoids rather than sines, and that is the whole point of it.**  A sine is what
/// you get by assuming the water barely moves; the truth, worked out by Gerstner in 1802, is that each
/// particle travels in a *circle* as the wave passes, so the water bunches up under a crest and
/// spreads out under a trough.  What that traces is a curve with narrow peaked crests and long flat
/// troughs -- which is what water looks like, and what a sine cannot be made to look like by sharpening
/// it, because sharpening a crest necessarily flattens the trough as well.
/// </para>
/// <para>
/// The cost of the truth is that it comes out *parametric*: the maths says where a piece of water
/// goes, when a renderer needs to know what is at a given place.  Where a sine gives the height at
/// <c>x</c> directly, this has to be turned round first -- which is what <see cref="SolveFor"/> does,
/// and why every question here costs a small iteration rather than a formula.
/// </para>
/// </summary>
public class SwellField
{
    /// <summary>
    /// How close the inversion is worked out to, as a fraction of a unit of distance.  Newton's method
    /// doubles the digits it has right at every pass, so this is reached in about four of them, and
    /// asking for more costs a pass for nothing a picture could show.
    /// </summary>
    private const double Tolerance = 1e-9;

    /// <summary>
    /// How many passes the inversion is allowed before it gives up and answers with what it has.  It
    /// does not reach this below the breaking steepness; the cap is there so that a scene written past
    /// it renders something rather than hanging.
    /// </summary>
    private const int MaximumPasses = 24;

    private readonly double[] _amplitudes;
    private readonly double[] _waveNumbers;
    private readonly double[] _alongX;
    private readonly double[] _alongZ;
    private readonly double[] _phases;
    private readonly double _reach;

    /// <summary>
    /// This property reports how far the water can rise above, or fall below, its rest level: every
    /// train's amplitude added up, since at one point or another they will all crest together.
    /// </summary>
    public double Reach => _reach;

    /// <summary>
    /// This property reports the steepness of all the trains together, which is what decides whether
    /// the surface can be turned round at all.  At one the crests close to cusps; past it the water
    /// folds over and there is no longer a single height at a place.
    /// </summary>
    public double TotalSteepness { get; }

    /// <summary>
    /// This constructs the field from a set of trains, unpacking them into flat arrays: every question
    /// asked here runs over all of them, several times per question, and this is the innermost
    /// arithmetic in a scene that carries water.
    /// </summary>
    /// <param name="trains">The wave trains to sum.</param>
    public SwellField(IReadOnlyList<SwellTrain> trains)
    {
        int count = trains.Count;

        _amplitudes = new double[count];
        _waveNumbers = new double[count];
        _alongX = new double[count];
        _alongZ = new double[count];
        _phases = new double[count];

        for (int index = 0; index < count; index++)
        {
            SwellTrain train = trains[index];
            Vector direction = new Vector(train.Direction.X, 0, train.Direction.Z);
            Vector unit = direction.Magnitude.Near(0) ? new Vector(1, 0, 0) : direction.Unit;

            _amplitudes[index] = train.Amplitude;
            _waveNumbers[index] = train.WaveNumber;
            _alongX[index] = unit.X;
            _alongZ[index] = unit.Z;
            _phases[index] = train.Phase;
            _reach += Math.Abs(train.Amplitude);
            TotalSteepness += Math.Abs(train.Steepness);
        }
    }

    /// <summary>
    /// This method turns the surface round: given a place on the flat, it finds the piece of water now
    /// standing over it.
    /// <para>
    /// The trains say where a piece of water starting at <c>(u, v)</c> has moved to.  What is wanted is
    /// the other way about, and there is no formula for it -- this is Kepler's equation wearing
    /// different clothes.  Newton's method solves it: guess, see how far the guess lands from the place
    /// asked about, and step against the slope.  The flat surface is the guess to start from, since
    /// that is where the water would be if it were not moving at all.
    /// </para>
    /// <para>
    /// **It converges because the waves are not breaking.**  Below a steepness of one the map from
    /// <c>u</c> to <c>x</c> only ever climbs, so there is exactly one answer and nothing to be led
    /// astray by.  At one the front face goes vertical, which is the wave breaking, and past that the
    /// question has more than one answer and this cannot be asked.
    /// </para>
    /// </summary>
    /// <param name="x">The X of the place to find the water over.</param>
    /// <param name="z">The Z of the place to find the water over.</param>
    /// <returns>Where that water started from, which is what the trains are written in terms of.</returns>
    public (double U, double V) SolveFor(double x, double z)
    {
        double u = x;
        double v = z;

        for (int pass = 0; pass < MaximumPasses; pass++)
        {
            double offsetX = u - x;
            double offsetZ = v - z;
            double slopeXu = 1, slopeXv = 0, slopeZu = 0, slopeZv = 1;

            for (int index = 0; index < _amplitudes.Length; index++)
            {
                double along = _alongX[index];
                double across = _alongZ[index];
                double angle = _waveNumbers[index] * (along * u + across * v) + _phases[index];
                double amplitude = _amplitudes[index];
                double sine = Math.Sin(angle);
                double slope = amplitude * _waveNumbers[index] * Math.Cos(angle);

                offsetX -= amplitude * along * sine;
                offsetZ -= amplitude * across * sine;

                slopeXu -= slope * along * along;
                slopeXv -= slope * along * across;
                slopeZu -= slope * across * along;
                slopeZv -= slope * across * across;
            }

            double determinant = slopeXu * slopeZv - slopeXv * slopeZu;

            if (determinant == 0)
                break;

            double stepU = (offsetX * slopeZv - offsetZ * slopeXv) / determinant;
            double stepV = (offsetZ * slopeXu - offsetX * slopeZu) / determinant;

            u -= stepU;
            v -= stepV;

            if (Math.Abs(stepU) < Tolerance && Math.Abs(stepV) < Tolerance)
                break;
        }

        return (u, v);
    }

    /// <summary>
    /// This method returns where the piece of water that started at the given place now stands.
    /// </summary>
    /// <param name="u">Where it started, along X.</param>
    /// <param name="v">Where it started, along Z.</param>
    /// <returns>Where it is now.</returns>
    public Point PointAt(double u, double v)
    {
        double x = u, y = 0, z = v;

        for (int index = 0; index < _amplitudes.Length; index++)
        {
            double angle = _waveNumbers[index] * (_alongX[index] * u + _alongZ[index] * v) +
                           _phases[index];
            double amplitude = _amplitudes[index];
            double sine = amplitude * Math.Sin(angle);

            x -= sine * _alongX[index];
            z -= sine * _alongZ[index];
            y += amplitude * Math.Cos(angle);
        }

        return new Point(x, y, z);
    }

    /// <summary>
    /// This method returns how high the water stands over a place on the flat.
    /// </summary>
    /// <param name="x">The X of the place.</param>
    /// <param name="z">The Z of the place.</param>
    /// <returns>The height of the water there, above its rest level.</returns>
    public double HeightAt(double x, double z)
    {
        (double u, double v) = SolveFor(x, z);
        double height = 0;

        for (int index = 0; index < _amplitudes.Length; index++)
        {
            height += _amplitudes[index] * Math.Cos(
                _waveNumbers[index] * (_alongX[index] * u + _alongZ[index] * v) + _phases[index]);
        }

        return height;
    }

    /// <summary>
    /// This method returns which way the surface faces over a place on the flat.
    /// <para>
    /// **Worked out rather than sampled.**  Because the shape is written as where each piece of water
    /// goes, the two directions the surface runs in are had by differentiating that directly, and the
    /// normal is their cross product.  Nothing here is a difference of two nearby evaluations, which
    /// is what an implicit surface has to fall back on -- so the normal is exact, and costs one pass
    /// over the trains rather than three or six extra solves.
    /// </para>
    /// </summary>
    /// <param name="x">The X of the place.</param>
    /// <param name="z">The Z of the place.</param>
    /// <returns>The unit normal of the water there.</returns>
    public Vector NormalAt(double x, double z)
    {
        (double u, double v) = SolveFor(x, z);

        // How the point moves as its starting place moves: the two tangents of the surface.
        double alongU_X = 1, alongU_Y = 0, alongU_Z = 0;
        double alongV_X = 0, alongV_Y = 0, alongV_Z = 1;

        for (int index = 0; index < _amplitudes.Length; index++)
        {
            double along = _alongX[index];
            double across = _alongZ[index];
            double wave = _waveNumbers[index];
            double angle = wave * (along * u + across * v) + _phases[index];
            double scaled = _amplitudes[index] * wave;
            double cosine = scaled * Math.Cos(angle);
            double sine = scaled * Math.Sin(angle);

            alongU_X -= cosine * along * along;
            alongU_Y -= sine * along;
            alongU_Z -= cosine * along * across;

            alongV_X -= cosine * across * along;
            alongV_Y -= sine * across;
            alongV_Z -= cosine * across * across;
        }

        Vector alongU = new (alongU_X, alongU_Y, alongU_Z);
        Vector alongV = new (alongV_X, alongV_Y, alongV_Z);
        Vector normal = alongU.Cross(alongV);

        // The two tangents are taken in the order that points the result upward for water lying flat;
        // a crest steep enough to lean could turn it over, and up is always the outward way here.
        return normal.Y < 0 ? -normal.Unit : normal.Unit;
    }

    /// <summary>
    /// This method returns the range the water's height can take over a box of the flat.
    /// <para>
    /// **This is what makes the surface affordable**, and it is asked far more often than the height
    /// itself: a marcher throws away whole stretches of a ray by asking whether the water could
    /// possibly reach them, and only looks closer at the stretches where it could.
    /// </para>
    /// <para>
    /// The looseness is in turning the box round.  The height is a sum of cosines of where the water
    /// *started*, and what is known is where it *is* -- and those differ by however far the water has
    /// moved, which is at most every amplitude added up.  So the box is grown by that much before the
    /// cosines are taken over it.  That is slack a sine surface does not carry, and it is the price of
    /// the shape being true.
    /// </para>
    /// </summary>
    /// <param name="x">The range of X the box covers.</param>
    /// <param name="z">The range of Z the box covers.</param>
    /// <returns>The range the height could take over it.</returns>
    public FieldRange HeightOver(FieldRange x, FieldRange z)
    {
        // **Turning the box round is the whole difficulty, and doing it badly makes the surface
        // unrenderable.**  The height is a sum of cosines of where the water *started*; what the box
        // is written in is where the water *is*.  Padding the box by how far water can move -- every
        // amplitude added up -- is correct but hopeless: it puts a *floor* under the bound, so that
        // halving a span stops tightening it, and a marcher that cannot tighten subdivides to its
        // limit everywhere.  Measured, that bound was 206 times looser than the truth on a box a
        // fiftieth of a unit across, and a sea took six and a half minutes to draw.
        //
        // The way out is that the map only stretches so far.  Along a ray, `dx/du` is
        // `1 - sum(A k cos)`, which cannot fall below `1 - steepness` -- so distance in `u` is at
        // most `1 / (1 - steepness)` times distance in `x`.  Solve once at the middle of the box and
        // that factor bounds how far the corners can have come from, which *shrinks with the box*.
        if (TotalSteepness >= 1)
            return new FieldRange(-_reach, _reach);

        (double middleU, double middleV) = SolveFor(
            (x.Low + x.High) / 2, (z.Low + z.High) / 2);
        double halfDiagonal = 0.5 * Math.Sqrt(
            x.Width * x.Width + z.Width * z.Width);
        double slack = halfDiagonal / (1 - TotalSteepness);
        FieldRange u = new (middleU - slack, middleU + slack);
        FieldRange v = new (middleV - slack, middleV + slack);
        double low = 0, high = 0;

        for (int index = 0; index < _amplitudes.Length; index++)
        {
            double along = _alongX[index] * _waveNumbers[index];
            double across = _alongZ[index] * _waveNumbers[index];

            // Where the angle can reach over the box, which is the two directions scaled and added.
            double first = along * u.Low + across * v.Low;
            double second = along * u.Low + across * v.High;
            double third = along * u.High + across * v.Low;
            double fourth = along * u.High + across * v.High;
            FieldRange angle = new (
                Math.Min(Math.Min(first, second), Math.Min(third, fourth)) + _phases[index],
                Math.Max(Math.Max(first, second), Math.Max(third, fourth)) + _phases[index]);
            FieldRange cosine = CosineOver(angle);
            double amplitude = _amplitudes[index];

            if (amplitude >= 0)
            {
                low += amplitude * cosine.Low;
                high += amplitude * cosine.High;
            }
            else
            {
                low += amplitude * cosine.High;
                high += amplitude * cosine.Low;
            }
        }

        return new FieldRange(low, high);
    }

    /// <summary>
    /// This method returns the range a cosine takes over a span of angle: the two ends, unless the
    /// span takes in a crest or a trough, in which case that side is pinned to one or minus one.
    /// </summary>
    /// <param name="angle">The span of angle.</param>
    /// <returns>The range the cosine takes over it.</returns>
    private static FieldRange CosineOver(FieldRange angle)
    {
        if (angle.Width >= 2 * Math.PI)
            return new FieldRange(-1, 1);

        double low = Math.Cos(angle.Low);
        double high = Math.Cos(angle.High);

        if (low > high)
            (low, high) = (high, low);

        if (TakesIn(angle, 0))
            high = 1;

        if (TakesIn(angle, Math.PI))
            low = -1;

        return new FieldRange(low, high);
    }

    /// <summary>
    /// This method reports whether a span of angle takes in the given angle, at any turn of it.
    /// </summary>
    /// <param name="angle">The span to look in.</param>
    /// <param name="wanted">The angle to look for.</param>
    /// <returns><c>true</c>, if the span reaches it.</returns>
    private static bool TakesIn(FieldRange angle, double wanted)
    {
        double turns = Math.Ceiling((angle.Low - wanted) / (2 * Math.PI));

        return wanted + turns * 2 * Math.PI <= angle.High;
    }
}
