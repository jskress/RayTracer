using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class holds the photons of a caustic: light followed from the lamps through mirrors and glass to
/// where it lands on something matte, and stored there to be read back by whatever looks at that place.
/// <para>
/// **A photon is a share of the light, left where it landed.**  Each carries how much light it stands
/// for, which way it was going, and which way the surface it landed on faces; where the photons lie
/// thick, much light landed.  So the light a place is lit by is the light of the photons nearest it,
/// divided by the area they were found over: a small area where they lie thick -- a sharp, bright focus
/// -- and a wide one where they lie thin.
/// </para>
/// <para>
/// **They are kept in a tree that halves the space at every level**, along whichever of its three
/// directions the photons spread furthest in, so that the nearest few to a place are found by looking
/// at a few dozen rather than at all of them.  It is built once, before the first pixel, and read by
/// every thread at once.
/// </para>
/// </summary>
public sealed class PhotonMap
{
    /// <summary>
    /// This record holds one photon: where it landed, the light it carries, which way it was going, which
    /// way the surface it landed on faces, toward the side it came from, and -- for one split into its colors
    /// by glass that spreads them -- the one band it carries, whose light is in <see cref="Red"/>.
    /// </summary>
    public readonly record struct Photon(
        float X, float Y, float Z, float Red, float Green, float Blue,
        float Dx, float Dy, float Dz, float Nx, float Ny, float Nz, byte Band)
    {
        /// <summary>
        /// This field marks a photon that carries every color, as a color.
        /// </summary>
        public const byte EveryBand = byte.MaxValue;

        /// <summary>
        /// This method makes a photon from the world's types.
        /// </summary>
        public static Photon At(Point place, Color light, Vector heading, Vector facing, byte band = EveryBand)
        {
            return new Photon(
                (float) place.X, (float) place.Y, (float) place.Z,
                (float) light.Red, (float) light.Green, (float) light.Blue,
                (float) heading.X, (float) heading.Y, (float) heading.Z,
                (float) facing.X, (float) facing.Y, (float) facing.Z, band);
        }

        internal double Along(int axis) => axis switch { 0 => X, 1 => Y, _ => Z };
    }

    // How sharply a photon's share falls off toward the edge of the area it is gathered over: Jensen's
    // cone filter, which keeps a focus sharp, and what it takes back out of the total.
    private const double ConeSlope = 1.1;
    private static readonly double ConeNormal = 1 - 2 / (3 * ConeSlope);

    // A photon counts at a place only if it landed on a surface facing nearly the same way, and no
    // further off the place's own surface than this share of the area's radius: what lands on the far
    // side of a thin wall, or on the wall beside a floor, is not the floor's.
    private const double LeastFacing = 0.9;
    private const double FlatShare = 0.25;

    // How far a photon's surface may turn from a place's before it no longer counts there, as an angle.
    private static readonly double FacingAngle = Math.Acos(LeastFacing);

    // What a branch's bound on its photons' facing gives up to the rounding of the floats photons and
    // bounds are kept in, so that it never passes over a photon the place would have taken: the cosine
    // that bounds how far they turn is lowered by the first before it is made an angle -- near nought
    // an angle is far less certain than its cosine -- and the least a place must share by the second.
    private const double TurningSlack = 1e-6;
    private const double FacingSlack = 1e-6;

    // The fewest photons a branch of the tree holds for it to be bounded; smaller ones are looked through.
    private const int LeastBounded = 8;

    private readonly Photon[] _photons;
    private readonly byte[] _axes;
    private readonly Bound[] _bounds;

    /// <summary>
    /// This record bounds the photons of one branch of the tree: the box they lie in, and which way they
    /// face, near enough -- the way most of them do, and the least that a place's own facing must share
    /// with it if any of them is to count there.  The branches are numbered from the whole tree at
    /// nought, a branch's lesser half following it at twice its number and one and its greater at twice
    /// and two.
    /// <para>
    /// **Without them, a place where few photons count looked at every photon near it that did not.**  A
    /// tree that halves space only along the way its photons spread furthest never halves a pool's floor
    /// by height, since it has none, so the water a meter and a quarter above it, looking a meter round
    /// itself, looked over a square two meters across of floor photons that all lay beyond its reach.
    /// Twenty thousand photons were looked at and passed over at every place, and a pool that rendered in
    /// twelve seconds without its caustic took forty-six minutes with it.  A branch whose box lies beyond a
    /// place's reach, or whose photons all face away from it, is passed by whole; what is found is exactly
    /// what was found before.
    /// </para>
    /// </summary>
    private readonly record struct Bound(
        float X, float Y, float Z, float Least,
        float LowX, float LowY, float LowZ, float HighX, float HighY, float HighZ);

    /// <summary>
    /// This property holds how many photons the map holds.
    /// </summary>
    public int Count => _photons.Length;

    /// <summary>
    /// This property holds how many photons a place gathers its light from.
    /// </summary>
    public int Nearest { get; }

    /// <summary>
    /// This property holds how far a place looks for them, at most.
    /// </summary>
    public double Farthest { get; }

    /// <summary>
    /// This property holds the photons, in the tree's order.
    /// </summary>
    internal IReadOnlyList<Photon> Photons => _photons;

    /// <summary>
    /// This property notes whether any photon carries one band alone.
    /// </summary>
    public bool HasBands { get; }

    [ThreadStatic]
    private static int[] _found;

    [ThreadStatic]
    private static double[] _distances;

    public PhotonMap(IEnumerable<Photon> photons, int nearest, double farthest)
    {
        _photons = photons.ToArray();
        _axes = new byte[_photons.Length];
        _bounds = new Bound[Math.Max(1, 2 * _photons.Length / LeastBounded)];
        HasBands = _photons.Any(photon => photon.Band != Photon.EveryBand);
        Nearest = nearest;
        Farthest = farthest;

        Build(0, _photons.Length, 0);
    }

    /// <summary>
    /// This method arranges the photons between two places in the array into a tree: the one at the
    /// middle splits them along the direction they spread furthest in, the lesser half before it and the
    /// greater after, each arranged the same way in turn.
    /// </summary>
    private void Build(int low, int high, int branch)
    {
        if (high - low < 2)
            return;

        double[] least = [double.MaxValue, double.MaxValue, double.MaxValue];
        double[] most = [double.MinValue, double.MinValue, double.MinValue];

        for (int index = low; index < high; index++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                double along = _photons[index].Along(axis);

                least[axis] = Math.Min(least[axis], along);
                most[axis] = Math.Max(most[axis], along);
            }
        }

        int widest = 0;

        for (int axis = 1; axis < 3; axis++)
        {
            if (most[axis] - least[axis] > most[widest] - least[widest])
                widest = axis;
        }

        if (high - low >= LeastBounded)
            _bounds[branch] = BoundOf(low, high, least, most);

        int middle = (low + high) / 2;

        Select(low, high - 1, middle, widest);
        _axes[middle] = (byte) widest;

        Build(low, middle, 2 * branch + 1);
        Build(middle + 1, high, 2 * branch + 2);
    }

    /// <summary>
    /// This method bounds the photons between two places in the array: the box they lie in, and which way
    /// they face -- the way they face on average, and how far the furthest of them turns from it, widened
    /// by how far a place's facing may turn from a photon's.  A place whose facing shares less than the
    /// bound's least with its way can take none of them.
    /// </summary>
    private Bound BoundOf(int low, int high, double[] least, double[] most)
    {
        double x = 0, y = 0, z = 0;

        for (int index = low; index < high; index++)
        {
            x += _photons[index].Nx;
            y += _photons[index].Ny;
            z += _photons[index].Nz;
        }

        double length = Math.Sqrt(x * x + y * y + z * z);

        // Facing every way at once, as both faces of a sheet may: no place can be turned from all of them.
        if (length < 1e-9)
            return BoundOf(1, 0, 0, -2, least, most);

        float ax = (float) (x / length), ay = (float) (y / length), az = (float) (z / length);
        double nearest = 1;

        for (int index = low; index < high; index++)
        {
            Photon photon = _photons[index];

            nearest = Math.Min(nearest, photon.Nx * ax + photon.Ny * ay + photon.Nz * az);
        }

        double spread = Math.Acos(Math.Clamp(nearest - TurningSlack, -1, 1)) + FacingAngle;

        return BoundOf(ax, ay, az, spread >= Math.PI ? -2 : Math.Cos(spread) - FacingSlack, least, most);
    }

    /// <summary>
    /// This method makes a bound from its facing and its box.  The box's corners are photons' own floats,
    /// so it holds them exactly.
    /// </summary>
    private static Bound BoundOf(float x, float y, float z, double sharing, double[] least, double[] most)
    {
        return new Bound(
            x, y, z, (float) sharing,
            (float) least[0], (float) least[1], (float) least[2], (float) most[0], (float) most[1], (float) most[2]);
    }

    /// <summary>
    /// This method notes whether any photon of a branch of the tree could count at a place: whether its box
    /// comes within the reach the place looks over, and, if the place's facing is given, whether any of
    /// its photons faces near enough the same way.
    /// </summary>
    private bool MayHold(
        int branch, double x, double y, double z, double reach, Vector facing, bool bothSides, bool bounded)
    {
        Bound bound = _bounds[branch];
        double dx = Math.Max(Math.Max(bound.LowX - x, x - bound.HighX), 0);
        double dy = Math.Max(Math.Max(bound.LowY - y, y - bound.HighY), 0);
        double dz = Math.Max(Math.Max(bound.LowZ - z, z - bound.HighZ), 0);

        // A photon is never nearer than the box it lies in, in the same arithmetic it is measured by.
        if (dx * dx + dy * dy + dz * dz >= reach)
            return false;

        if (!bounded)
            return true;

        double sharing = bound.X * facing.X + bound.Y * facing.Y + bound.Z * facing.Z;

        return (bothSides ? Math.Abs(sharing) : sharing) >= bound.Least;
    }

    /// <summary>
    /// This method notes whether a facing is of unit length, as the bounds on the tree's branches take it
    /// to be; a place asking with any other is looked for the long way, so as to find exactly what it did.
    /// </summary>
    private static bool IsAUnit(Vector facing)
    {
        return Math.Abs(facing.X * facing.X + facing.Y * facing.Y + facing.Z * facing.Z - 1) < 1e-9;
    }

    /// <summary>
    /// This method puts the photon that belongs at a place in the order along an axis there, the lesser
    /// before it and the greater after (Hoare's selection), the same way every time.
    /// </summary>
    private void Select(int low, int high, int place, int axis)
    {
        while (high > low)
        {
            double pivot = _photons[(low + high) / 2].Along(axis);
            int left = low;
            int right = high;

            while (left <= right)
            {
                while (_photons[left].Along(axis) < pivot)
                    left++;

                while (_photons[right].Along(axis) > pivot)
                    right--;

                if (left <= right)
                {
                    (_photons[left], _photons[right]) = (_photons[right], _photons[left]);
                    left++;
                    right--;
                }
            }

            if (place <= right)
                high = right;
            else if (place >= left)
                low = left;
            else
                return;
        }
    }

    /// <summary>
    /// This method gathers the light the photons nearest a place bring it: the sum over them of the light
    /// each carries, weighed by how near it lies and by what the surface makes of light arriving at its
    /// slant, divided by the area they were found over.
    /// </summary>
    /// <param name="place">The place.</param>
    /// <param name="facing">Which way the surface faces there, toward the eye.</param>
    /// <param name="bothSides">Whether the surface is lit from either side, as a sheet is.</param>
    /// <param name="taking">What the surface makes of light arriving at the given cosine to it, as a share
    /// of what that light would give it square-on.</param>
    /// <param name="least">The least radius to gather over: the width of what is looking at the place,
    /// finer than which nothing can be told apart.  A lens brings a sun, which has no width here, to a
    /// point, and the nearest photons to that point crowd into no area at all; gathered over a pixel's
    /// width instead, the focus is as bright as a pixel's worth of it.</param>
    /// <param name="bands">Where to add the irradiance photons carrying one band alone bring, band by band;
    /// left empty, they are passed over, a render in colors having none.</param>
    /// <returns>The irradiance photons carrying every color bring, as a color of light.</returns>
    public Color Gather(
        Point place, Vector facing, bool bothSides, Func<double, double> taking, double least = 0,
        Span<double> bands = default)
    {
        if (_photons.Length == 0)
            return Colors.Black;

        _found ??= new int[Nearest];
        _distances ??= new double[Nearest];

        if (_found.Length < Nearest)
        {
            _found = new int[Nearest];
            _distances = new double[Nearest];
        }

        Query query = new (this, place, facing, bothSides, Farthest * Farthest);

        query.Visit(0, _photons.Length, 0);

        if (query.Count == 0)
            return Colors.Black;

        // The area is out to the farthest photon found, or the farthest a place looks if it found fewer
        // than it looks for.
        double radius = query.Count < Nearest ? Farthest : Math.Sqrt(query.Reach);

        // Every photon within the least radius, where the nearest lie closer than that.
        if (radius < least)
            return Sum(place, facing, bothSides, taking, least, bands);

        double red = 0, green = 0, blue = 0;
        double area = ConeNormal * Math.PI * radius * radius;

        for (int index = 0; index < query.Count; index++)
        {
            Photon photon = _photons[_found[index]];
            double cosine = -(photon.Dx * facing.X + photon.Dy * facing.Y + photon.Dz * facing.Z);

            if (bothSides)
                cosine = Math.Abs(cosine);

            double share = (1 - Math.Sqrt(_distances[index]) / (ConeSlope * radius)) * taking(cosine);

            if (photon.Band != Photon.EveryBand)
            {
                if (!bands.IsEmpty)
                    bands[photon.Band] += photon.Red * share / area;

                continue;
            }

            red += photon.Red * share;
            green += photon.Green * share;
            blue += photon.Blue * share;
        }

        return new Color(red / area, green / area, blue / area);
    }

    /// <summary>
    /// This method sums the light of every photon within a radius of a place, weighed as the nearest are.
    /// </summary>
    private Color Sum(
        Point place, Vector facing, bool bothSides, Func<double, double> taking, double radius, Span<double> bands)
    {
        double red = 0, green = 0, blue = 0;
        double reach = radius * radius;
        double area = ConeNormal * Math.PI * radius * radius;
        Stack<(int Low, int High, int Branch)> ranges = new ();
        bool bounded = IsAUnit(facing);

        ranges.Push((0, _photons.Length, 0));

        while (ranges.Count > 0)
        {
            (int low, int high, int branch) = ranges.Pop();

            if (high <= low)
                continue;

            if (high - low >= LeastBounded &&
                !MayHold(branch, place.X, place.Y, place.Z, reach, facing, bothSides, bounded))
                continue;

            int middle = (low + high) / 2;
            Photon photon = _photons[middle];
            int axis = _axes[middle];
            double offset = axis switch { 0 => place.X - photon.X, 1 => place.Y - photon.Y, _ => place.Z - photon.Z };

            if (offset < 0 || offset * offset < reach)
                ranges.Push((low, middle, 2 * branch + 1));

            if (offset >= 0 || offset * offset < reach)
                ranges.Push((middle + 1, high, 2 * branch + 2));

            double dx = photon.X - place.X, dy = photon.Y - place.Y, dz = photon.Z - place.Z;
            double distance = dx * dx + dy * dy + dz * dz;

            if (distance >= reach)
                continue;

            double facingBy = photon.Nx * facing.X + photon.Ny * facing.Y + photon.Nz * facing.Z;
            double off = dx * facing.X + dy * facing.Y + dz * facing.Z;

            if ((bothSides ? Math.Abs(facingBy) : facingBy) < LeastFacing || off * off > FlatShare * FlatShare * reach)
                continue;

            double cosine = -(photon.Dx * facing.X + photon.Dy * facing.Y + photon.Dz * facing.Z);

            if (bothSides)
                cosine = Math.Abs(cosine);

            double share = (1 - Math.Sqrt(distance) / (ConeSlope * radius)) * taking(cosine);

            if (photon.Band != Photon.EveryBand)
            {
                if (!bands.IsEmpty)
                    bands[photon.Band] += photon.Red * share / area;

                continue;
            }

            red += photon.Red * share;
            green += photon.Green * share;
            blue += photon.Blue * share;
        }

        return new Color(red / area, green / area, blue / area);
    }

    /// <summary>
    /// This structure carries one search for the photons nearest a place: those found so far, kept as a
    /// heap with the farthest on top, and how far the farthest of them is.
    /// </summary>
    private ref struct Query
    {
        private readonly PhotonMap _map;
        private readonly double _x, _y, _z;
        private readonly Vector _facing;
        private readonly bool _bothSides;
        private readonly int[] _found;
        private readonly double[] _distances;
        private readonly double _farthest;
        private readonly bool _bounded;

        public int Count;
        public double Reach;

        public Query(PhotonMap map, Point place, Vector facing, bool bothSides, double farthest)
        {
            _map = map;
            _x = place.X;
            _y = place.Y;
            _z = place.Z;
            _facing = facing;
            _bothSides = bothSides;
            _found = PhotonMap._found;
            _distances = PhotonMap._distances;
            _farthest = farthest;
            _bounded = IsAUnit(facing);
            Count = 0;
            Reach = farthest;
        }

        public void Visit(int low, int high, int branch)
        {
            while (high > low)
            {
                if (high - low >= LeastBounded &&
                    !_map.MayHold(branch, _x, _y, _z, Reach, _facing, _bothSides, _bounded))
                    return;

                int middle = (low + high) / 2;
                Photon photon = _map._photons[middle];
                int axis = _map._axes[middle];
                double offset = axis switch { 0 => _x - photon.X, 1 => _y - photon.Y, _ => _z - photon.Z };

                // The near side first; the far side only if the area could reach across the split.
                if (offset < 0)
                {
                    Visit(low, middle, 2 * branch + 1);
                    Consider(middle, photon);

                    if (offset * offset >= Reach)
                        return;

                    low = middle + 1;
                    branch = 2 * branch + 2;
                }
                else
                {
                    Visit(middle + 1, high, 2 * branch + 2);
                    Consider(middle, photon);

                    if (offset * offset >= Reach)
                        return;

                    high = middle;
                    branch = 2 * branch + 1;
                }
            }
        }

        private void Consider(int index, Photon photon)
        {
            double dx = photon.X - _x, dy = photon.Y - _y, dz = photon.Z - _z;
            double distance = dx * dx + dy * dy + dz * dz;

            if (distance >= Reach)
                return;

            double facing = photon.Nx * _facing.X + photon.Ny * _facing.Y + photon.Nz * _facing.Z;

            if ((_bothSides ? Math.Abs(facing) : facing) < LeastFacing)
                return;

            double off = dx * _facing.X + dy * _facing.Y + dz * _facing.Z;

            if (off * off > FlatShare * FlatShare * Reach)
                return;

            int nearest = _map.Nearest;

            if (Count < nearest)
            {
                _found[Count] = index;
                _distances[Count] = distance;
                Count++;
                Rise(Count - 1);

                if (Count == nearest)
                    Reach = _distances[0];
            }
            else
            {
                _found[0] = index;
                _distances[0] = distance;
                Sink(0);
                Reach = _distances[0];
            }
        }

        private void Rise(int child)
        {
            while (child > 0)
            {
                int parent = (child - 1) / 2;

                if (_distances[parent] >= _distances[child])
                    return;

                Swap(parent, child);
                child = parent;
            }
        }

        private void Sink(int parent)
        {
            while (true)
            {
                int child = 2 * parent + 1;

                if (child >= Count)
                    return;

                if (child + 1 < Count && _distances[child + 1] > _distances[child])
                    child++;

                if (_distances[parent] >= _distances[child])
                    return;

                Swap(parent, child);
                parent = child;
            }
        }

        private void Swap(int one, int other)
        {
            (_found[one], _found[other]) = (_found[other], _found[one]);
            (_distances[one], _distances[other]) = (_distances[other], _distances[one]);
        }
    }
}
