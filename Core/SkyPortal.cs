using RayTracer.Basics;

namespace RayTracer.Core;

/// <summary>
/// This record holds one opening the sky comes into a room through -- a window, a doorway, a skylight -- as a
/// rectangle in the world, with the way out to the sky.  Every portal a scene marks is one of these, found by
/// <c>Scene.OpenTheSky</c> once the scene is settled.
/// <para>
/// The rectangle is held in a frame of its own: its first edge along <see cref="Along"/>, its second along
/// <see cref="Across"/>, and <see cref="Outward"/> square to both, toward the sky -- so a point in the room
/// sees the opening with every direction through it leaning outward.  The three make a right-handed frame,
/// <see cref="Along"/> x <see cref="Across"/> being <see cref="Outward"/>, which is what lets openings in the
/// same wall share one map of the sky (see <see cref="SkyPortals"/>).
/// </para>
/// </summary>
/// <param name="Corner">The corner the rectangle's edges leave.</param>
/// <param name="Along">The unit direction of its first edge.</param>
/// <param name="Across">The unit direction of its second.</param>
/// <param name="Outward">The unit direction square to both, toward the sky.</param>
/// <param name="Length">How long its first edge is.</param>
/// <param name="Width">How long its second edge is.</param>
public sealed record SkyPortal(
    Point Corner, Vector Along, Vector Across, Vector Outward, double Length, double Width)
{
    /// <summary>
    /// This method returns the portal a rectangle makes, given its corner, its two edges and which way the sky
    /// lies, the edges taken in whichever order keeps the frame right-handed.
    /// </summary>
    /// <param name="corner">One corner of the rectangle.</param>
    /// <param name="first">One edge leaving it.</param>
    /// <param name="second">The other edge leaving it, square to the first.</param>
    /// <param name="outward">Any direction leaning toward the sky side.</param>
    /// <returns>The portal.</returns>
    public static SkyPortal Of(Point corner, Vector first, Vector second, Vector outward)
    {
        Vector normal = first.Cross(second).Unit;

        // Turned to face the sky, the frame must stay right-handed: swapping the edges turns their cross product
        // the other way round, so the rectangle is the same and its frame faces out.
        if (normal.Dot(outward) < 0)
            (first, second, normal) = (second, first, -normal);

        return new SkyPortal(corner, first.Unit, second.Unit, normal, first.Magnitude, second.Magnitude);
    }

    /// <summary>
    /// This method returns where a point stands in the portal's frame: along its first edge from its corner, along
    /// its second, and out toward the sky (negative on the room's side).
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>Its place in the frame.</returns>
    public (double X, double Y, double Z) Place(Point point)
    {
        Vector from = point - Corner;

        return (from.Dot(Along), from.Dot(Across), from.Dot(Outward));
    }
}
