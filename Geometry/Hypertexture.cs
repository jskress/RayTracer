using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Patterns;

namespace RayTracer.Geometry;

/// <summary>
/// This class is a <b>hypertexture</b> (Perlin and Hoffert, 1989): a solid's skin broken up by noise -- moved in and
/// out by it, drawn as a surface, or turned into stuff that thins out across it, drawn as a medium.
/// <para>
/// **Everything is measured from the solid's skin**, whatever the solid is, by a sampled distance field (see
/// <see cref="DistanceGrid"/>): the distance d to the skin, negative inside.  The noise -- any pattern, measured
/// over the hypertexture before the first ray and stretched so that from its second to its ninety-eighth
/// percentile it runs from nought to one, so that the reach means what it says whatever the pattern -- moves the
/// skin: the field is d + reach (2n - 1), or d + reach n when the skin is only <see cref="Eroded"/>.  And the skin
/// may be <see cref="Drawn"/> out along a direction, as far as its length where the skin faces that way and the noise
/// is lowest: flames rising, wax dripping.
/// </para>
/// <para>
/// **Hard**, the moved skin is a surface, found by marching as any distance is.  The field is not a true distance once
/// noise is added to it, so near the skin each step is the field over how steeply it was found to change (its
/// slope, sampled over the skin before the first ray, with half as much again for what the samples missed); further
/// off, the distance less the most the skin can have moved is a true bound, and nothing else is asked.
/// </para>
/// <para>
/// **Soft**, the skin is stuff: one where the field is less than minus <see cref="Soft"/>, nothing where it is more
/// than the soft width, and a smooth step between -- over as far again as the skin was drawn out, where it was, so a
/// flame thins from its root to its tip.  The hypertexture is then a clear shell round a medium whose
/// density it gives (<see cref="Density"/>).  The shell lies as far out as the stuff can reach -- the solid's skin
/// moved out by the most the noise, the drawing out and the soft width can move it -- which is a true distance and
/// marched in full strides, and keeps a ray's steps through the medium near the stuff rather than across an empty
/// box.  Traced along the stuff's own edge instead, a flame's shell was marched at a ninety-seventh of a stride.
/// </para>
/// </summary>
public class Hypertexture : DistanceEstimatedSurface
{
    /// <summary>How many places over the skin the noise and the field's slope are measured at.</summary>
    private const int Measures = 4096;

    /// <summary>
    /// This property holds the solid whose skin is broken up, in the hypertexture's own space.
    /// </summary>
    public Surface Solid { get; set; }

    /// <summary>
    /// This property holds how far the noise moves the skin; a twentieth of the solid's longest side unless said.
    /// </summary>
    public double? Reach { get; set; }

    /// <summary>
    /// This property holds the noise the skin is moved by; bozo stirred by half a turbulence, its features about four
    /// reaches across, unless said.
    /// </summary>
    public DensityShape Noise { get; set; }

    /// <summary>
    /// This property notes that the skin only moves in, eaten away, never out.
    /// </summary>
    public bool Eroded { get; set; }

    /// <summary>
    /// This property holds the direction, and how far, the skin is drawn out where it faces that way.
    /// </summary>
    public Vector Drawn { get; set; }

    /// <summary>
    /// This property holds, for a soft hypertexture, how far either side of the moved skin its stuff thins out
    /// over; <c>null</c> for a hard one.
    /// </summary>
    public double? Soft { get; set; }

    /// <summary>
    /// This property holds how many cells of the distance field span the longest side of what is measured.
    /// </summary>
    public int Resolution { get; set; } = 128;

    /// <summary>
    /// This property notes that the scene said an accuracy, which is otherwise a hundredth of a cell.
    /// </summary>
    public bool AccuracyIsSaid { get; set; }

    /// <summary>
    /// This property holds the density a soft hypertexture gives the medium it holds.
    /// </summary>
    public HypertextureDensity Density { get; }

    private readonly object _measuring = new ();
    private volatile bool _measured;
    private DistanceGrid _grid;
    private BoundingBox _domain;
    private double _reach, _drawnLength, _soft, _low, _span = 1, _slope = 1;
    private Vector _drawnWay;

    public Hypertexture()
    {
        Density = new HypertextureDensity(this);
    }

    /// <summary>
    /// This property reports how far apart the distance field's nodes are, once it is measured.
    /// </summary>
    internal double Cell => _grid?.Cell ?? 0;

    /// <summary>
    /// This property reports how steeply the field was found to change near the skin, once it is measured.
    /// </summary>
    internal double Slope => _slope;

    /// <summary>
    /// This method measures the solid, once, before the first ray.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        Measure();
    }

    /// <summary>
    /// This method returns the box the hypertexture fills: the solid's, grown by as far as its skin can move.
    /// </summary>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        Measure();

        return new BoundingBox().Add(_domain.Minimum).Add(_domain.Maximum);
    }

    /// <summary>
    /// This property holds the region the march is confined to.
    /// </summary>
    protected override BoundingBox MarchingDomain => _domain;

    /// <summary>
    /// This property holds how far the normal is looked for: a quarter of a cell, finer than which the field knows
    /// nothing of the solid and the noise is grain.
    /// </summary>
    protected override double NormalStep => Cell / 4;

    /// <summary>
    /// This property notes that the field's answer inside is as good a bound as outside: strided through faster, the
    /// march leapt the thin gaps between pieces of a rough skin and never found the pieces beyond them.
    /// </summary>
    protected override bool TrustedInside => true;

    /// <summary>
    /// This method returns the field the march walks: the moved skin, as a bound on how far it is; or for a soft
    /// hypertexture its shell, the solid's skin moved out by the most its stuff can reach, a true distance.
    /// </summary>
    protected override double DistanceAt(double x, double y, double z)
    {
        double d = _grid.DistanceAt(x, y, z);

        // The most the skin can have moved out.  Past it, the distance less that is a true bound on how far the moved
        // skin is -- and it is all a soft hypertexture's shell needs to be, since there is no stuff beyond it.
        double outward = d - (Eroded ? 0 : _reach) - _drawnLength - _soft;

        if (Soft is not null || outward > 0)
            return outward;

        double inward = d + _reach;

        if (inward < 0)
            return inward;

        return FieldAt(x, y, z, d) / _slope;
    }

    /// <summary>
    /// This method returns how far the solid's own skin is from a place, as the hypertexture measured it.
    /// </summary>
    internal double SkinDistanceAt(Point point)
    {
        Measure();

        return _grid.DistanceAt(point.X, point.Y, point.Z);
    }

    /// <summary>
    /// This method returns the moved skin's field at a place: negative inside the moved skin, positive outside.
    /// </summary>
    internal double FieldAt(Point point)
    {
        Measure();

        return FieldAt(point.X, point.Y, point.Z, _grid.DistanceAt(point.X, point.Y, point.Z));
    }

    /// <summary>
    /// This method returns the moved skin's field at a place whose distance from the solid's skin is known.
    /// </summary>
    internal double FieldAt(double x, double y, double z, double d) => FieldAt(x, y, z, d, out _);

    /// <summary>
    /// This method returns the moved skin's field at a place whose distance from the solid's skin is known, and how
    /// far the skin was drawn out there.
    /// </summary>
    private double FieldAt(double x, double y, double z, double d, out double drawn)
    {
        double noise = NoiseAt(new Point(x, y, z));
        double field = Eroded ? d + _reach * noise : d + _reach * (2 * noise - 1);

        drawn = 0;

        if (_drawnLength > 0)
        {
            // Which way the skin faces, read four cells either way: a cell either way turns the facing sharply round
            // every corner, and a flame's field was found to change ninety-seven times as fast as a distance there.
            Vector gradient = _grid.GradientAt(x, y, z, 4 * _grid.Cell);
            double length = gradient.Magnitude;

            if (length > 0)
                drawn = _drawnLength * Math.Max(0, gradient.Dot(_drawnWay) / length) * (1 - noise);

            field -= drawn;
        }

        return field;
    }

    /// <summary>
    /// This method returns the noise at a place, stretched to run from nought to one over its middle 96 percent.
    /// </summary>
    private double NoiseAt(Point point) => Math.Clamp((Noise.ValueAt(point) - _low) / _span, 0, 1);

    /// <summary>
    /// This method returns how much of a soft hypertexture's stuff there is at a place, in its own space.
    /// </summary>
    internal double StuffAt(Point point)
    {
        Measure();

        double d = _grid.DistanceAt(point.X, point.Y, point.Z);

        if (d - (Eroded ? 0 : _reach) - _drawnLength >= _soft)
            return 0;

        if (d + _reach <= -_soft)
            return 1;

        // Where the skin is drawn out the stuff thins out over as far as it was drawn, so a flame runs from whole at
        // its root to nothing at its tip; thinned over the soft width alone, a flame was whole stuff to the tip, and
        // its light, read by its density, the same white all through.
        double field = FieldAt(point.X, point.Y, point.Z, d, out double drawn);
        double share = Math.Clamp((_soft - field) / (2 * _soft + drawn), 0, 1);

        return share * share * (3 - 2 * share);
    }

    /// <summary>
    /// This method returns which way the skin faces: the solid's distance taken a cell either way, so it turns
    /// smoothly across the field's cells, and the rest of the field a quarter of a cell either way; a soft
    /// hypertexture's shell, as the solid's distance alone.
    /// </summary>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        if (intersection is PrecomputedNormalIntersection)
            return base.SurfaceNormalAt(point, intersection);

        (double x, double y, double z) = (point.X, point.Y, point.Z);

        // A soft hypertexture's shell is the solid's skin moved out evenly, so it faces as the solid's distance does:
        // given the noise's slope too, it faced off at random, rays leaving it set off on the wrong side, and its
        // rim came out in black blots.
        if (Soft is not null)
        {
            Vector facing = _grid.GradientAt(x, y, z);

            return facing.Magnitude < 1e-300 ? Directions.Up : facing.Unit;
        }

        double h = NormalStep;
        Vector normal = _grid.GradientAt(x, y, z) * (2 * h) + new Vector(
            Moved(x + h, y, z) - Moved(x - h, y, z),
            Moved(x, y + h, z) - Moved(x, y - h, z),
            Moved(x, y, z + h) - Moved(x, y, z - h));

        return normal.Magnitude < 1e-300 ? Directions.Up : normal.Unit;
    }

    // How far the noise and the drawing out have moved the skin at a place.
    private double Moved(double x, double y, double z)
    {
        double d = _grid.DistanceAt(x, y, z);

        return FieldAt(x, y, z, d) - d;
    }

    /// <summary>
    /// This method measures the solid and the noise over it, once.
    /// </summary>
    private void Measure()
    {
        if (_measured)
            return;

        lock (_measuring)
        {
            if (_measured)
                return;

            MeasureNow();

            _measured = true;
        }
    }

    private void MeasureNow()
    {
        if (Solid is null)
            throw new Exception("A hypertexture needs a solid: the thing whose skin it breaks up.");

        if (Resolution < 8)
            throw new Exception($"A hypertexture's resolution must be at least 8 cells, not {Resolution}.");

        if (Reach is < 0 || Soft is <= 0)
            throw new Exception("A hypertexture's reach may not be less than nought, nor its soft width nought or less.");

        if (!Solid.SharedAndReady)
        {
            Solid.SharedAndReady = true;
            Solid.PrepareForRendering();
        }

        BoundingBox box = BoxAround(Solid, true);

        if (box is null || box.IsEmpty || !double.IsFinite(box.Minimum.X + box.Minimum.Y + box.Minimum.Z) ||
            !double.IsFinite(box.Maximum.X + box.Maximum.Y + box.Maximum.Z))
        {
            throw new Exception("A hypertexture's solid must end somewhere: a plane or an open-ended shape has no skin to measure from.");
        }

        Vector size = box.Maximum - box.Minimum;
        double longest = Math.Max(size.X, Math.Max(size.Y, size.Z));

        _reach = Reach ?? 0.05 * longest;
        _soft = Soft ?? 0;
        _drawnLength = Drawn?.Magnitude ?? 0;
        _drawnWay = _drawnLength > 0 ? Drawn.Unit : null;

        double pad = _reach + _soft + _drawnLength;
        double cell = (longest + 2 * pad) / Resolution;

        pad += 3 * cell;

        Vector margin = new (pad, pad, pad);
        BoundingBox domain = new BoundingBox().Add(box.Minimum - margin).Add(box.Maximum + margin);
        DistanceGrid grid = DistanceGrid.Of(Solid, domain, cell);

        if (grid is null)
            throw new Exception("A hypertexture's solid has no skin that a ray crosses: it must be a closed solid.");

        Noise ??= new DensityShape
        {
            Pattern = new BozoPattern { Turbulence = new Turbulence { Amplitude = new Vector(0.5, 0.5, 0.5) } },
            Transform = Transforms.Scale(Math.Max(4 * _reach, cell * 4))
        };

        if (!AccuracyIsSaid)
            Accuracy = cell / 100;

        _domain = domain;
        _grid = grid;

        MeasureTheNoise(domain);
    }

    /// <summary>
    /// This method measures the noise over places near the skin, for how it is spread and how steeply the field
    /// changes there.  The places are hashed, so the hypertexture comes out the same every time.
    /// </summary>
    private void MeasureTheNoise(BoundingBox domain)
    {
        DistanceGrid grid = _grid;
        Vector size = domain.Maximum - domain.Minimum;
        double band = _reach + _drawnLength + _soft + 2 * grid.Cell;
        List<Point> places = [];

        for (int draw = 0; draw < Measures * 40 && places.Count < Measures; draw++)
        {
            Point place = new (
                domain.Minimum.X + Unit(draw, 0) * size.X, domain.Minimum.Y + Unit(draw, 1) * size.Y,
                domain.Minimum.Z + Unit(draw, 2) * size.Z);

            if (Math.Abs(grid.DistanceAt(place.X, place.Y, place.Z)) < band)
                places.Add(place);
        }

        if (places.Count == 0)
            return;

        double[] values = places.Select(Noise.ValueAt).Order().ToArray();
        double low = values[(int) (0.02 * (values.Length - 1))];
        double high = values[(int) (0.98 * (values.Length - 1))];

        _low = low;
        _span = high - low > 1e-9 ? high - low : 1;

        // The field's slope, by differences a quarter of a cell either way, over the same places.
        double h = grid.Cell / 4;
        double steepest = 0;

        foreach (Point place in places)
        {
            (double x, double y, double z) = (place.X, place.Y, place.Z);
            double dx = Field(x + h, y, z) - Field(x - h, y, z);
            double dy = Field(x, y + h, z) - Field(x, y - h, z);
            double dz = Field(x, y, z + h) - Field(x, y, z - h);

            steepest = Math.Max(steepest, Math.Sqrt(dx * dx + dy * dy + dz * dz) / (2 * h));
        }

        _slope = Math.Max(1, 1.5 * steepest);

        double Field(double x, double y, double z) => FieldAt(x, y, z, grid.DistanceAt(x, y, z));
    }

    // A number from nought to one for a draw and one of its coordinates, hashed (splitmix).
    private static double Unit(int draw, int purpose)
    {
        ulong bits = (ulong) (uint) draw * 0x9E3779B97F4A7C15UL ^ (ulong) (uint) purpose * 0xC2B2AE3D27D4EB4FUL;

        bits = (bits ^ (bits >> 30)) * 0xBF58476D1CE4E5B9UL;
        bits = (bits ^ (bits >> 27)) * 0x94D049BB133111EBUL;
        bits ^= bits >> 31;

        return (bits >> 11) * (1.0 / (1UL << 53));
    }
}

/// <summary>
/// This class is the density a soft hypertexture gives the medium it holds: how much of its stuff there is at a
/// place, read from the hypertexture itself.
/// </summary>
public sealed class HypertextureDensity : DensityShape
{
    private readonly Hypertexture _hypertexture;

    internal HypertextureDensity(Hypertexture hypertexture)
    {
        _hypertexture = hypertexture;
    }

    /// <summary>
    /// This method returns how much of the stuff there is at a place in the hypertexture's own space.
    /// </summary>
    public override double ValueAt(Point point) => _hypertexture.StuffAt(point);

    /// <summary>
    /// This method notes whether this is the same density as another: only if it is the same hypertexture's.
    /// </summary>
    public override bool Matches(DensityShape other) =>
        other is HypertextureDensity density && ReferenceEquals(density._hypertexture, _hypertexture);
}
