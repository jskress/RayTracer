using RayTracer.Basics;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class is a light that is a <i>volume of glowing stuff</i> rather than a lamp: the medium
/// inside a surface, lighting what is around it.
/// <para>
/// Everything else in this renderer that glows only glows at the eye.  A medium adds its light to
/// rays that pass through it, so a flame is bright to look at -- and nothing carried that light out
/// to the ground, so a fire in a dark room left the room dark and every scene with a fire in it had
/// to stand a lamp inside the fire by hand and keep the two in step.  This is what removes that.
/// </para>
/// <para>
/// It is a light like any other from the outside: it is asked for a number of places to be looked at
/// from, it answers with a direction and a distance for each, and the scene casts its shadow ray and
/// applies its Phong exactly as for a lamp.  What differs is where the places are -- scattered through
/// the stuff, rather than over the face of a rectangle -- and that each carries its own color, since a
/// flame is white at its heart and red at its tip.
/// </para>
/// </summary>
public class VolumeLight : Light
{
    /// <summary>
    /// How many cells the stuff is measured over along each axis when the table is built.  This is
    /// not how many samples a render takes; it is how finely the shape is known before it starts.
    /// </summary>
    private const int Cells = 12;

    /// <summary>
    /// This property holds how many places this light is looked at from.
    /// </summary>
    public int Samples { get; set; } = 24;

    private readonly Surface _surface;
    private readonly Medium _medium;
    private readonly Point _corner;
    private readonly Vector _span;
    private readonly double[] _running;
    private readonly double _stuff;
    private readonly double _nearest;

    /// <summary>
    /// This constructor measures the stuff inside the surface once, so that the samples a render takes
    /// can be spent where there is something to see.
    /// <para>
    /// A flame fills perhaps a fifth of the shell it is carried in, and the rest is empty; samples
    /// spread evenly through the shell would spend four fifths of themselves on nothing.  So the box
    /// is walked once here, cell by cell, and a running total kept -- then a sample picks its place in
    /// proportion to how much stuff is there.
    /// </para>
    /// </summary>
    /// <param name="surface">The surface the stuff fills.</param>
    /// <param name="medium">The stuff.</param>
    public VolumeLight(Surface surface, Medium medium)
    {
        _surface = surface;
        _medium = medium;

        BoundingBox box = surface.BoundingBox;

        _corner = box.Minimum;
        _span = box.Maximum - box.Minimum;
        _running = new double[Cells * Cells * Cells];

        double total = 0;
        int at = 0;

        for (int x = 0; x < Cells; x++)
        {
            for (int y = 0; y < Cells; y++)
            {
                for (int z = 0; z < Cells; z++)
                {
                    total += medium.DensityAt(PlaceIn(x, y, z, 0.5, 0.5, 0.5));
                    _running[at++] = total;
                }
            }
        }

        // How much stuff there is, as a volume: the average density over the box times the size of the
        // box, and then times how much the surface's own transform stretches space.  Volume scales by
        // the determinant, and the density function is written in the surface's own space while the
        // light has to be right in the world's.
        double boxVolume = Math.Abs(_span.X * _span.Y * _span.Z);
        double average = total / _running.Length;

        _stuff = average * boxVolume * Math.Abs(surface.Transform.Determinant);

        // How close a point may get to one of these samples before the sample stops being a fair
        // stand-in for it.  Each sample speaks for a cell of the box, not for a point, and the inverse
        // square of a *point* runs away to infinity as something approaches it -- so a floor an inch
        // from a fire came out with a searing white spot on it where one sample happened to land near.
        // A cell holds a finite amount of stuff spread over a finite space, and nothing can be nearer
        // to all of it than its own size; this is the radius of a ball of that volume, which is the
        // honest floor.
        double cellVolume = boxVolume * Math.Abs(surface.Transform.Determinant) / _running.Length;

        _nearest = Math.Cbrt(cellVolume * 3 / (4 * Math.PI));
    }

    /// <summary>
    /// This property reports whether there is anything here to light with.  A medium that emits
    /// nothing, or one whose density came out at nought everywhere, is not a light and should not be
    /// added to a scene as one.
    /// </summary>
    public bool Lights => _stuff > 0;

    /// <summary>
    /// This property notes how many places this light is looked at from.
    /// </summary>
    public override int SampleCount => Samples;

    /// <summary>
    /// This method answers where the middle of the stuff lies, for the one caller that wants a light
    /// as a single place rather than as a spread.
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <returns>Which way the middle of the stuff lies, and how far off.</returns>
    public override (Vector Direction, double Distance) TowardFrom(Point point)
    {
        Point middle = _surface.SurfaceToWorld(_corner + _span * 0.5);
        Vector toward = middle - point;

        return (toward.Unit, toward.Magnitude);
    }

    /// <summary>
    /// This method works out one of the places this light is looked at from: a point somewhere in the
    /// stuff, chosen in proportion to how much stuff is there.
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <param name="index">Which sample this is.</param>
    /// <param name="normal">The surface normal, which a volume has no use for.</param>
    /// <returns>The sample, carrying the color the stuff is at that place.</returns>
    public override LightSample SampleToward(Point point, int index, Vector normal = null)
    {
        // Two irrationals walked against each other, as the sky light does: it spreads any number of
        // samples about as evenly as they can be spread, needs nothing remembered between them, and is
        // the same pattern at every point, which is what keeps a render the same twice over.
        double which = (index + 0.5) / Samples;
        double along = index * 0.7548776662466927 % 1;
        double across = index * 0.5698402909980532 % 1;

        int cell = CellAt(which * _running[^1]);
        int z = cell % Cells;
        int y = cell / Cells % Cells;
        int x = cell / (Cells * Cells);

        Point local = PlaceIn(x, y, z, along, across, (along + across) % 1);
        Point where = _surface.SurfaceToWorld(local);
        Vector toward = where - point;
        double distance = toward.Magnitude;

        // The whole of the energy is here, and it is worked out rather than tuned.  A Lambertian
        // surface under an irradiance E gives back E/π, and this renderer's shading multiplies the
        // light's color by the pigment and the cosine and nothing else -- so what a sample must carry
        // is the irradiance its share of the stuff delivers, divided by π.
        //
        // Sampling in proportion to density is what makes it this short: the density that would appear
        // on top cancels against the density in the odds of having picked this place at all, and what
        // is left is how much stuff there is in total.  The inverse square is here and nowhere else,
        // which is why this light must never also be given a fade distance -- that would apply it
        // twice.
        double reach = Math.Max(distance, _nearest);
        Color carried = _medium.EmissionAt(local) * (_stuff / (Math.PI * reach * reach));

        return new LightSample(toward.Unit, distance, 1, carried);
    }

    /// <summary>
    /// This method returns the color this light carries along one of its samples, which for a volume
    /// differs from place to place: a flame is white at the heart and red at the tip.
    /// </summary>
    /// <param name="sample">The sample being asked about.</param>
    /// <returns>The color the stuff is where that sample was taken.</returns>
    public override Color ColorFor(LightSample sample)
    {
        return sample.Carried is null ? Color : sample.Carried * Color;
    }

    /// <summary>
    /// This method finds the cell holding the given amount of the running total, which is how a place
    /// is picked in proportion to how much stuff is in it.
    /// </summary>
    /// <param name="wanted">How far into the total to look.</param>
    /// <returns>The index of the cell there.</returns>
    private int CellAt(double wanted)
    {
        int low = 0;
        int high = _running.Length - 1;

        while (low < high)
        {
            int middle = (low + high) / 2;

            if (_running[middle] < wanted)
                low = middle + 1;
            else
                high = middle;
        }

        return low;
    }

    /// <summary>
    /// This method returns a place inside one cell of the box, in the surface's own space.
    /// </summary>
    private Point PlaceIn(int x, int y, int z, double alongX, double alongY, double alongZ)
    {
        return new Point(
            _corner.X + _span.X * (x + alongX) / Cells,
            _corner.Y + _span.Y * (y + alongY) / Cells,
            _corner.Z + _span.Z * (z + alongZ) / Cells);
    }
}
