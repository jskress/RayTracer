using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This record holds how an image is laid on one surface: the map, the radius of the ring a toroidal map's tube
/// runs round, and what lies past the image's edges each way.  An image read as a pigment and one read as a pattern
/// are laid alike.
/// </summary>
internal readonly record struct ImageLaying(ImageMapType Map, double Ring, TextureWrap WrapAcross, TextureWrap WrapDown)
{
    /// <summary>
    /// This method returns the map an image given none is laid on a surface by, from the shape it is, or
    /// <c>null</c> for a shape that has none.
    /// <para>
    /// Anything that is part of a height field is laid as the field lays its heights, whatever its own shape: a
    /// picture painted for a terrain -- where its snow lies, where its rock shows -- is painted texel for texel
    /// against the heights, and only the field's own placing puts the two together.
    /// </para>
    /// </summary>
    /// <param name="surface">The surface to lay an image on.</param>
    /// <returns>The map its shape implies, or <c>null</c>.</returns>
    internal static ImageMapType? DefaultMapFor(Surface surface) => surface switch
    {
        { Parent: HeightField } => ImageMapType.Field,
        Cube or Plane or Triangle or Parallelogram => ImageMapType.Planar,
        Sphere => ImageMapType.Spherical,
        Cylinder => ImageMapType.Cylindrical,
        Torus => ImageMapType.Toroidal,
        _ => null
    };

    /// <summary>
    /// This method returns how an image is laid by the given map on the given surface: round the surface's own
    /// ring, for a toroidal map on a torus.
    /// </summary>
    /// <param name="map">The map to lay the image by.</param>
    /// <param name="surface">The surface it is laid on, if any.</param>
    /// <param name="once">Whether the image is laid once rather than repeated.</param>
    /// <returns>How the image is laid.</returns>
    internal static ImageLaying For(ImageMapType map, Surface surface, bool once)
    {
        (TextureWrap across, TextureWrap down) = ((ImageMapType?) map).WrapFor(once);

        return new ImageLaying(map, surface is Torus torus ? torus.MajorRadius : 1, across, down);
    }

    /// <summary>
    /// This method returns how an image laid this way for anything that says no otherwise is laid on the surface
    /// being read (see <see cref="Pigment.Wearer"/>): by the map it was given, or the one the surface's own shape
    /// implies -- an image worn by a cube and a sphere lies flat on the one and round the other.  Read for no surface,
    /// or for one whose shape implies nothing, it is laid this way.
    /// </summary>
    /// <param name="wearer">The surface being read, if any.</param>
    /// <param name="said">The map the image was given, if any.</param>
    /// <param name="once">Whether the image is laid once rather than repeated.</param>
    /// <returns>How the image is laid on the wearer.</returns>
    internal ImageLaying On(Surface wearer, ImageMapType? said, bool once)
    {
        if (wearer is null || (said ?? DefaultMapFor(wearer)) is not { } map)
            return this;

        double ring = wearer is Torus torus ? torus.MajorRadius : 1;

        return map == Map && ring == Ring ? this : For(map, wearer, once);
    }
}
