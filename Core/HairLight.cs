using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class holds the light that has been scattered at least once among the strands of a head of hair:
/// how much of it there is, and which way it is going on the whole, at each place in a grid laid over the
/// hair.
/// <para>
/// **It is gathered by following light from each lamp into the hair, the long way**, one scattering
/// after another, as <see cref="Scene"/> does once before the hair is first shaded; see
/// <see cref="Scene"/>'s gathering.  Light travelling between strands is recorded all along its way, which
/// is to say that the grid holds the light's fluence, and its net flow -- the sum of every bit of it times
/// the way it is going.  From the two, the light arriving from any one direction is worked out as the
/// smoothest spread of directions that has both: more of it coming from where it flows from.
/// </para>
/// <para>
/// **The direction matters as much as the amount.**  Near the lit side of fur, most of the light among the
/// strands has passed forward through the outer ones and is going inward, away from whoever is looking at
/// them; what a strand sends toward the eye is mostly what comes at it from behind, which is the smaller
/// share coming back out.  Taken as arriving from every direction alike, the light gave fur half as much
/// again to twice the brightness the long way round says it has.
/// </para>
/// <para>
/// The grid's spacing, a fraction of a strand's length, blurs the light, which smooths away the grain of
/// the paths followed while keeping how it falls off deeper into the hair.
/// </para>
/// </summary>
public sealed class HairLight
{
    private readonly Point _corner;
    private readonly double _cell;
    private readonly int _across;
    private readonly int _up;
    private readonly int _deep;
    private readonly double[] _light;

    // At each place: the fluence in red, green and blue, then the net flow in each, as three numbers.
    internal const int Quantities = 12;

    /// <summary>
    /// This property holds how many places the grid holds the light at.
    /// </summary>
    public int Size => _across * _up * _deep;

    /// <summary>
    /// This creates an empty grid over a box, at the given spacing.
    /// </summary>
    /// <param name="low">The box's corner with the smallest coordinates.</param>
    /// <param name="high">The box's corner with the largest.</param>
    /// <param name="cell">The spacing between the places the light is held at.</param>
    public HairLight(Point low, Point high, double cell)
    {
        _corner = low;
        _cell = cell;
        _across = Math.Max(2, (int) Math.Ceiling((high.X - low.X) / cell));
        _up = Math.Max(2, (int) Math.Ceiling((high.Y - low.Y) / cell));
        _deep = Math.Max(2, (int) Math.Ceiling((high.Z - low.Z) / cell));
        _light = new double[Quantities * Size];
    }

    /// <summary>
    /// This property holds the box's corner with the smallest coordinates.
    /// </summary>
    public Point Low => _corner;

    /// <summary>
    /// This property holds the box's corner with the largest coordinates.
    /// </summary>
    public Point High => new (_corner.X + _across * _cell, _corner.Y + _up * _cell, _corner.Z + _deep * _cell);

    /// <summary>
    /// This property holds the spacing between the places the light is held at.
    /// </summary>
    public double Cell => _cell;

    /// <summary>
    /// This method returns which cell of the grid a point lies in, counted as the places are, or -1 for a
    /// point outside the grid.
    /// </summary>
    /// <param name="point">The point, in the world.</param>
    /// <returns>The cell's number.</returns>
    public int CellOf(Point point)
    {
        int i = (int) Math.Floor((point.X - _corner.X) / _cell);
        int j = (int) Math.Floor((point.Y - _corner.Y) / _cell);
        int k = (int) Math.Floor((point.Z - _corner.Z) / _cell);

        return i < 0 || j < 0 || k < 0 || i >= _across || j >= _up || k >= _deep ? -1 : (k * _up + j) * _across + i;
    }

    /// <summary>
    /// This method reports whether a point lies within the box the grid covers.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns><c>true</c>, if it does.</returns>
    public bool Holds(Point point)
    {
        Point high = High;

        return point.X >= _corner.X && point.Y >= _corner.Y && point.Z >= _corner.Z &&
               point.X <= high.X && point.Y <= high.Y && point.Z <= high.Z;
    }

    /// <summary>
    /// This method records light travelling a stretch of the way, spread over the places around each point
    /// along it in proportion to how near each is, into the given tally.
    /// </summary>
    /// <param name="tally">The tally, twelve numbers to a place; see <see cref="Quantities"/>.</param>
    /// <param name="from">Where the stretch starts.</param>
    /// <param name="to">Where it ends.</param>
    /// <param name="power">The light travelling it.</param>
    public void Record(float[] tally, Point from, Point to, RgbSpectrum power)
    {
        Point high = High;
        Vector way = to - from;
        double length = way.Magnitude;

        if (length <= 0)
            return;

        // Kept to the part of the stretch inside the box.
        double start = 0, end = 1;

        for (int axis = 0; axis < 3; axis++)
        {
            double origin = axis switch { 0 => from.X, 1 => from.Y, _ => from.Z };
            double step = axis switch { 0 => way.X, 1 => way.Y, _ => way.Z };
            double low = axis switch { 0 => _corner.X, 1 => _corner.Y, _ => _corner.Z };
            double top = axis switch { 0 => high.X, 1 => high.Y, _ => high.Z };

            if (Math.Abs(step) < 1e-15)
            {
                if (origin < low || origin > top)
                    return;

                continue;
            }

            double near = (low - origin) / step;
            double far = (top - origin) / step;

            if (near > far)
                (near, far) = (far, near);

            start = Math.Max(start, near);
            end = Math.Min(end, far);
        }

        if (end <= start)
            return;

        double inside = (end - start) * length;
        int steps = Math.Max(1, (int) Math.Ceiling(inside / (_cell * 0.5)));
        double each = inside / steps;
        Vector going = way / length;

        for (int index = 0; index < steps; index++)
        {
            double t = start + (end - start) * (index + 0.5) / steps;

            Spread(tally, from + way * t, going, power[0] * each, power[1] * each, power[2] * each);
        }
    }

    /// <summary>
    /// This method adds the tallies, in order, into the grid, turning what was recorded into fluence and
    /// flow: light times length over the volume of a cell.
    /// </summary>
    /// <param name="tallies">The tallies, in the order they are to be added.</param>
    public void Settle(IEnumerable<float[]> tallies)
    {
        double scale = 1 / (_cell * _cell * _cell);

        foreach (float[] tally in tallies)
        {
            for (int index = 0; index < _light.Length; index++)
                _light[index] += tally[index] * scale;
        }
    }

    /// <summary>
    /// This struct holds the light among the strands at one place: its fluence and its net flow, in red,
    /// green and blue.
    /// </summary>
    public readonly struct Here
    {
        private readonly double[] _quantities;

        internal Here(double[] quantities) => _quantities = quantities;

        /// <summary>
        /// This method returns the light arriving from the given direction: the fluence spread over the
        /// sphere, more of it from where the light flows from, never less than none.
        /// </summary>
        /// <typeparam name="TS">The kind of light being carried.</typeparam>
        /// <param name="from">The direction it arrives from, of unit length.</param>
        /// <returns>The radiance, per band.</returns>
        public TS From<TS>(Vector from)
            where TS : struct, ISpectrum<TS>
        {
            double[] quantities = _quantities;

            double Band(int band) => Math.Max(0, (quantities[band] - 3 * (
                quantities[3 + 3 * band] * from.X + quantities[4 + 3 * band] * from.Y +
                quantities[5 + 3 * band] * from.Z)) / (4 * Math.PI));

            return TS.FromUnbounded(new Color(Band(0), Band(1), Band(2)));
        }

        /// <summary>
        /// This method returns the light arriving from every direction, on average.
        /// </summary>
        /// <typeparam name="TS">The kind of light being carried.</typeparam>
        /// <returns>The radiance, per band.</returns>
        public TS Everywhere<TS>()
            where TS : struct, ISpectrum<TS>
        {
            return TS.FromUnbounded(new Color(
                _quantities[0] / (4 * Math.PI), _quantities[1] / (4 * Math.PI), _quantities[2] / (4 * Math.PI)));
        }
    }

    /// <summary>
    /// This method returns the light among the strands at a place, read between the grid places around it.
    /// </summary>
    /// <param name="point">The place, in the world.</param>
    /// <returns>The light there.</returns>
    public Here At(Point point)
    {
        (int x, int y, int z, double fx, double fy, double fz) = Locate(point);
        double[] quantities = new double[Quantities];

        for (int corner = 0; corner < 8; corner++)
        {
            int i = x + (corner & 1), j = y + ((corner >> 1) & 1), k = z + ((corner >> 2) & 1);

            if (i < 0 || j < 0 || k < 0 || i >= _across || j >= _up || k >= _deep)
                continue;

            double weight = ((corner & 1) == 0 ? 1 - fx : fx) * (((corner >> 1) & 1) == 0 ? 1 - fy : fy) *
                            (((corner >> 2) & 1) == 0 ? 1 - fz : fz);
            int at = Quantities * ((k * _up + j) * _across + i);

            for (int quantity = 0; quantity < Quantities; quantity++)
                quantities[quantity] += _light[at + quantity] * weight;
        }

        return new Here(quantities);
    }

    /// <summary>
    /// This method adds an amount of light going one way to the eight places around a point, in proportion
    /// to how near each is.
    /// </summary>
    private void Spread(float[] tally, Point point, Vector going, double red, double green, double blue)
    {
        (int x, int y, int z, double fx, double fy, double fz) = Locate(point);

        for (int corner = 0; corner < 8; corner++)
        {
            int i = x + (corner & 1), j = y + ((corner >> 1) & 1), k = z + ((corner >> 2) & 1);

            if (i < 0 || j < 0 || k < 0 || i >= _across || j >= _up || k >= _deep)
                continue;

            double weight = ((corner & 1) == 0 ? 1 - fx : fx) * (((corner >> 1) & 1) == 0 ? 1 - fy : fy) *
                            (((corner >> 2) & 1) == 0 ? 1 - fz : fz);
            int at = Quantities * ((k * _up + j) * _across + i);
            ReadOnlySpan<double> amounts = [red * weight, green * weight, blue * weight];

            for (int band = 0; band < 3; band++)
            {
                tally[at + band] += (float) amounts[band];
                tally[at + 3 + 3 * band] += (float) (amounts[band] * going.X);
                tally[at + 4 + 3 * band] += (float) (amounts[band] * going.Y);
                tally[at + 5 + 3 * band] += (float) (amounts[band] * going.Z);
            }
        }
    }

    /// <summary>
    /// This method finds the grid place below a point on each axis, counting places at the middles of the
    /// cells, and how far past it the point lies.
    /// </summary>
    private (int X, int Y, int Z, double Fx, double Fy, double Fz) Locate(Point point)
    {
        double x = (point.X - _corner.X) / _cell - 0.5;
        double y = (point.Y - _corner.Y) / _cell - 0.5;
        double z = (point.Z - _corner.Z) / _cell - 0.5;
        int i = (int) Math.Floor(x), j = (int) Math.Floor(y), k = (int) Math.Floor(z);

        return (i, j, k, x - i, y - j, z - k);
    }
}
