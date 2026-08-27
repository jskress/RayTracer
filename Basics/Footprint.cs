namespace RayTracer.Basics;

/// <summary>
/// This class represents how much of a surface one ray is answerable for, as a little parallelogram
/// lying in the surface at the place the ray struck it.
/// <para>
/// It exists because a pixel is not a point.  A pixel gathers the light arriving over a small solid
/// angle, so the honest colour for it is the surface's colour *averaged* over the patch that angle
/// lands on.  Sampling a pattern at the single point where a ray happens to hit is not a cheaper
/// version of that average; it is a different and wrong answer, and when the error lines up between
/// neighbouring pixels -- which perspective makes it do -- it shows as broad curving bands across a
/// wall that ought to be brick.  This is what a pattern needs in order to answer the question that
/// was actually asked.
/// </para>
/// <para>
/// **It is a parallelogram rather than a radius because the shape matters.**  A ray meeting a wall
/// head-on covers a disc; the same ray meeting it at a glance covers a long thin ellipse, because
/// the same cone of light is smeared across far more surface.  A single radius cannot tell those
/// apart, and it is the glancing one that a street of buildings is full of.
/// </para>
/// </summary>
public class Footprint
{
    /// <summary>
    /// This property holds a footprint of no size at all, which is what a ray that was never told
    /// how fast it spreads has.  A pattern handed this samples at a point, exactly as it did before
    /// any of this existed.
    /// </summary>
    public static readonly Footprint None = new (new Vector(0, 0, 0), new Vector(0, 0, 0));

    /// <summary>
    /// This property holds one edge of the patch.
    /// </summary>
    public Vector Across { get; }

    /// <summary>
    /// This property holds the other edge of the patch.
    /// </summary>
    public Vector Along { get; }

    /// <summary>
    /// This property reports whether this footprint has any size to it.  A pattern should ask this
    /// before doing any work to filter itself, since the answer is very often no.
    /// </summary>
    public bool IsEmpty => _isEmpty;

    /// <summary>
    /// This property reports the width of the patch: the longer of its two edges.
    /// <para>
    /// The *longer*, deliberately.  Detail finer than this cannot be told apart along at least one
    /// direction, so anything relying on a single number -- the layered noise, which has to decide
    /// how many of its octaves are worth summing -- should use this and blur a little more than it
    /// strictly must, rather than use the shorter edge and alias along the longer one.  It is the
    /// same trade a mip map makes, and the same small cost: a wall seen at a glance is softened
    /// slightly more than an ideal filter would soften it.
    /// </para>
    /// </summary>
    public double Width => _width;

    private readonly bool _isEmpty;
    private readonly double _width;

    public Footprint(Vector across, Vector along)
    {
        Across = across;
        Along = along;

        _width = Math.Max(across.Magnitude, along.Magnitude);
        _isEmpty = _width <= 0;
    }

    /// <summary>
    /// This method works out the patch a cone of the given radius covers where it meets a surface
    /// with the given normal, coming in along the given direction.
    /// <para>
    /// The cone's cross-section is a disc of that radius square to its direction.  Sliding that disc
    /// along the direction until it lies in the surface is what turns it into the ellipse the
    /// surface actually sees, and the flatter the angle the longer that ellipse gets.
    /// </para>
    /// </summary>
    /// <param name="radius">How wide the cone has grown where it lands.</param>
    /// <param name="direction">The direction the ray was going, which must be a unit vector.</param>
    /// <param name="normal">The surface normal there, which must be a unit vector.</param>
    /// <returns>The patch of surface the ray is answerable for.</returns>
    public static Footprint For(double radius, Vector direction, Vector normal)
    {
        if (radius <= 0)
            return None;

        double facing = direction.Dot(normal);

        // Square on to the surface, the disc stays a disc.  As the angle flattens, `facing` goes to
        // nought and the ellipse runs away to infinity -- so it is held to a limit, for the reason
        // every renderer holds it: the true footprint at a hair's breadth off the surface is most of
        // the wall, and honouring that turns a whole building to one flat colour.
        const double Flattest = 0.05;

        if (Math.Abs(facing) < Flattest)
            facing = facing < 0 ? -Flattest : Flattest;

        // Any two directions square to the ray and to each other will do: sliding both of them into
        // the surface spans the same ellipse whichever pair is picked.
        Vector aside = Math.Abs(direction.X) < 0.9
            ? direction.Cross(new Vector(1, 0, 0)).Unit
            : direction.Cross(new Vector(0, 1, 0)).Unit;
        Vector other = direction.Cross(aside).Unit;

        // The edges span the *diameter*, not the radius, and that distinction cost a measurement to
        // find.  Whatever samples the patch walks each edge from -0.5 to +0.5 of it, so an edge one
        // radius long covers only one radius of surface -- half the pixel -- and everything filtered
        // through it was filtered at half the width it should have been.  It showed as a residual
        // banding that survived every other fix tried at it, and as a filter that came out *further*
        // from a supersampled truth than deliberately widening it by two did.
        return new Footprint(
            Flatten(aside * (radius * 2), direction, normal, facing),
            Flatten(other * (radius * 2), direction, normal, facing));
    }

    /// <summary>
    /// This method slides a vector along the ray's direction until it lies in the surface.
    /// </summary>
    private static Vector Flatten(Vector vector, Vector direction, Vector normal, double facing)
    {
        return vector - direction * (vector.Dot(normal) / facing);
    }

    /// <summary>
    /// This method carries the patch through a transform, so that a footprint measured in the
    /// world can be handed to a pattern that thinks in its own space.
    /// <para>
    /// Both edges go through as *vectors* rather than as points, which is what keeps a translation
    /// from moving a size, and what lets a stretch along one axis stretch the patch with it.
    /// </para>
    /// </summary>
    /// <param name="matrix">The transform to carry the patch through.</param>
    /// <returns>The patch, in the space the transform leads to.</returns>
    public Footprint TransformedBy(Matrix matrix)
    {
        return _isEmpty ? None : new Footprint(matrix * Across, matrix * Along);
    }

    /// <summary>
    /// This method reports how far the patch reaches along one axis, which is what something
    /// filtering itself axis by axis -- a brick lattice, a checker -- needs to know.
    /// </summary>
    /// <param name="index">Which axis: nought for X, one for Y, two for Z.</param>
    /// <returns>Half the patch's extent along that axis.</returns>
    public double ExtentOn(int index)
    {
        return index switch
        {
            0 => Math.Abs(Across.X) + Math.Abs(Along.X),
            1 => Math.Abs(Across.Y) + Math.Abs(Along.Y),
            _ => Math.Abs(Across.Z) + Math.Abs(Along.Z)
        };
    }
}
