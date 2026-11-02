using RayTracer.Basics;
using RayTracer.Geometry;

namespace RayTracer.Pigments;

/// <summary>
/// This record holds which way a surface faces where a picture laid by its facing -- a triplanar one -- is read, and
/// how the surface's own space is stretched in the world there: the surface's own three axes as they stand in
/// whatever space the read has been carried into, how long each of them is in the world, and the surface's normal
/// told in its own axes.
/// <para>
/// **It rides along beside the point.**  A picture is handed a point already carried through every transform between
/// the surface and itself -- a pattern pigment's, a blend's, a bump's -- and a facing means nothing there unless it
/// is carried the same way.  So while a material holding such a picture is read, the bearing stands on the thread,
/// and every pigment the point passes through turns the axes as it turns the point (<see cref="Into"/>).  Every
/// other read never sees one.
/// </para>
/// <para>
/// **The stretch is what the picture leaves out.**  A height field is drawn in a unit square and stretched over the
/// ground, a boulder is a ball squashed flat; laid in its own space, a picture would be stretched with it.  From the
/// axes and their lengths a picture can tell how far a step along any of its own axes takes it in the world, and lay
/// itself at the world's size instead (<see cref="ImageTaps"/>).
/// </para>
/// </summary>
/// <param name="AlongX">The surface's own X axis, in the space being read.</param>
/// <param name="AlongY">The surface's own Y axis, in the space being read.</param>
/// <param name="AlongZ">The surface's own Z axis, in the space being read.</param>
/// <param name="Stretch">How long each of the surface's own axes is in the world.</param>
/// <param name="Facing">The surface's normal, told in its own axes: how much it faces along each.</param>
internal sealed record Bearing(Vector AlongX, Vector AlongY, Vector AlongZ, Vector Stretch, Vector Facing)
{
    private static readonly Vector UnitX = new (1, 0, 0);
    private static readonly Vector UnitY = new (0, 1, 0);
    private static readonly Vector UnitZ = new (0, 0, 1);

    [ThreadStatic]
    private static Bearing _current;

    /// <summary>
    /// This property holds the bearing of the read in hand on this thread, carried into the space of the pigment or
    /// pattern being asked, or <c>null</c> where nothing has been read that needs one.
    /// </summary>
    internal static Bearing Current => _current;

    /// <summary>
    /// This method returns the bearing of a surface where the world sees it facing the given way.
    /// </summary>
    /// <param name="surface">The surface being read.</param>
    /// <param name="normal">Which way it faces there, in the world.</param>
    /// <param name="timeIndex">Which instant of the shutter's opening it is read at.</param>
    /// <param name="portals">The instances it was found through, if it was.</param>
    /// <returns>Its bearing, in its own space.</returns>
    internal static Bearing At(Surface surface, Vector normal, int timeIndex, Portals portals)
    {
        (Vector x, Vector y, Vector z) = AxesOf(surface, timeIndex, portals);

        // A normal carried out of a space goes by the transposed inverse of the way out, so carried back in it goes by
        // the transpose of the way out: how much it faces along each of the axes as they stand in the world.
        return new Bearing(
            UnitX, UnitY, UnitZ, new Vector(x.Magnitude, y.Magnitude, z.Magnitude),
            new Vector(x.Dot(normal), y.Dot(normal), z.Dot(normal)));
    }

    /// <summary>
    /// This method returns the bearing of a surface facing the given way in its own space.
    /// </summary>
    /// <param name="surface">The surface being read.</param>
    /// <param name="normal">Which way it faces there, in its own space.</param>
    /// <param name="timeIndex">Which instant of the shutter's opening it is read at.</param>
    /// <param name="portals">The instances it was found through, if it was.</param>
    /// <returns>Its bearing, in its own space.</returns>
    internal static Bearing Of(Surface surface, Vector normal, int timeIndex, Portals portals)
    {
        (Vector x, Vector y, Vector z) = AxesOf(surface, timeIndex, portals);

        return new Bearing(UnitX, UnitY, UnitZ, new Vector(x.Magnitude, y.Magnitude, z.Magnitude), normal);
    }

    /// <summary>
    /// This method returns the bearing of a surface facing the given way in its own space, taken to be unstretched:
    /// what a roughening asked about a point with no surface to go by has to go on.
    /// </summary>
    /// <param name="normal">Which way it faces there, in its own space.</param>
    /// <returns>Its bearing, in its own space.</returns>
    internal static Bearing Unstretched(Vector normal) => new (UnitX, UnitY, UnitZ, new Vector(1, 1, 1), normal);

    /// <summary>
    /// This method returns the surface's own three axes as they stand in the world.
    /// </summary>
    private static (Vector X, Vector Y, Vector Z) AxesOf(Surface surface, int timeIndex, Portals portals) => (
        surface.DirectionToWorld(UnitX, timeIndex, portals),
        surface.DirectionToWorld(UnitY, timeIndex, portals),
        surface.DirectionToWorld(UnitZ, timeIndex, portals));

    /// <summary>
    /// This method makes the given bearing the one in hand on this thread, until the scope it returns is disposed of.
    /// </summary>
    /// <param name="bearing">The bearing of the read about to be made.</param>
    /// <returns>The scope, which puts back the bearing in hand before.</returns>
    internal static Scope Held(Bearing bearing)
    {
        Scope scope = new (true, _current);

        _current = bearing;

        return scope;
    }

    /// <summary>
    /// This method carries the bearing in hand, if there is one, into a space a point is carried into by the given
    /// matrix, until the scope it returns is disposed of.  With none in hand it does nothing at all.
    /// </summary>
    /// <param name="inverse">The matrix the point is carried by.</param>
    /// <returns>The scope, which puts back the bearing in hand before.</returns>
    internal static Scope Into(Matrix inverse)
    {
        Bearing was = _current;

        if (was is null)
            return default;

        _current = was with { AlongX = inverse * was.AlongX, AlongY = inverse * was.AlongY, AlongZ = inverse * was.AlongZ };

        return new Scope(true, was);
    }

    /// <summary>
    /// This struct puts back, when it is disposed of, the bearing that was in hand before a <see cref="Held"/> or an
    /// <see cref="Into"/> that set one.
    /// </summary>
    internal readonly struct Scope(bool set, Bearing was) : IDisposable
    {
        public void Dispose()
        {
            if (set)
                _current = was;
        }
    }
}
