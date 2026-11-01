using RayTracer.Basics;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Pigments;

namespace RayTracer.Patterns;

/// <summary>
/// This enumeration notes which of a picture's channels a pattern read from it takes its value from.
/// </summary>
public enum ImageChannel
{
    /// <summary>
    /// The picture's gray, read as a height field reads its heights.
    /// </summary>
    Gray,

    /// <summary>
    /// The picture's red channel alone.
    /// </summary>
    Red,

    /// <summary>
    /// The picture's green channel alone.
    /// </summary>
    Green,

    /// <summary>
    /// The picture's blue channel alone.
    /// </summary>
    Blue,

    /// <summary>
    /// The picture's alpha channel: how much of each place it covers.
    /// </summary>
    Alpha
}

/// <summary>
/// This class is a pattern read from a picture: a picture's gray at each place, or one of its channels.  It is what
/// lets a picture do more than color a surface -- how deep its bumps run, where it is worn, how rough it is -- since
/// anything that takes a pattern takes this one.
/// <para>
/// **A picture read as a pattern is read as a height field reads one**, as the values the file holds: such a picture
/// is a measurement rather than a color, and is written as one, unencoded.  Its gray is the height field's own, so a
/// single picture may raise a terrain and roughen or mark it, place for place alike.  It is laid on a surface as an
/// image pigment is, by the map it is given or the one the surface's shape implies -- the height field's own placing
/// for anything that is part of one -- and flat on X and Z when read for no surface at all, as a field's patches are
/// read.
/// </para>
/// </summary>
public class ImagePattern : Pattern
{
    // How far apart the two ends of an edge are set to measure how fast the picture's places change, in the pattern's
    // own units.  Every map but the round ones is a straight line, which any length measures; round ones turn, and a
    // ten-thousandth of a unit is a texel's fraction on any picture laid on a shape of ordinary size.
    private const double Measure = 1e-4;

    // The largest number below one: a map's break values lie in [0, 1), and it wraps anything at one back round to its
    // start, where a picture's white must keep the map's last entry.  Nothing else can tell the two apart.
    private static readonly double BelowOne = Math.BitDecrement(1.0);

    /// <summary>
    /// This property holds the information for the picture to read.
    /// </summary>
    public ImageReference ImageReference { get; set; }

    /// <summary>
    /// This property notes the map the picture is laid on a surface by, or <c>null</c> to lay it by the surface's own
    /// shape.
    /// </summary>
    public ImageMapType? MapType { get; set; }

    /// <summary>
    /// This property notes whether the picture is laid once rather than repeated, for the maps that repeat.
    /// </summary>
    public bool Once { get; set; }

    /// <summary>
    /// This property notes which of the picture's channels the pattern takes its value from.
    /// </summary>
    public ImageChannel Channel { get; set; }

    /// <summary>
    /// This property reports the number of discrete pigments this pattern supports: none, since its value runs from
    /// nought to one.
    /// </summary>
    public override int DiscretePigmentsNeeded => 0;

    private Texture _texture;

    /// <summary>
    /// This property holds the picture, read as it stands the first time it is asked for.
    /// </summary>
    private Texture Texture => LazyInitializer.EnsureInitialized(ref _texture, () => ImageReference.TextureFor(false));

    /// <summary>
    /// This method reads the picture now, rather than at the first place asked about, so that one that cannot be read
    /// is reported before the render starts rather than from the middle of it.
    /// </summary>
    public void Load() => _ = Texture;

    /// <summary>
    /// This method returns how the picture is laid on the surface being read (see <see cref="Pigment.Wearer"/>).
    /// </summary>
    private ImageLaying Laid() =>
        ImageLaying.For(MapType ?? ImageMapType.Planar, null, Once).On(Pigment.Wearer, MapType, Once);

    /// <summary>
    /// This method returns the pattern's value at a point, read between the four texels nearest it.
    /// </summary>
    /// <param name="point">The point, in the pattern's own space.</param>
    /// <returns>The value there, from nought to one.</returns>
    public override double Evaluate(Point point) => ValueOf(ColorAt(point, null));

    /// <summary>
    /// This method returns the pattern's value averaged over the patch a ray covers, as an image pigment averages its
    /// colors: one read at a point would beat a fine picture seen from far off into moiré.
    /// </summary>
    /// <param name="point">The point, in the pattern's own space.</param>
    /// <param name="footprint">How much of the surface the ray covers there.</param>
    /// <returns>The value there, from nought to one.</returns>
    public override double Evaluate(Point point, Footprint footprint) => ValueOf(ColorAt(point, footprint));

    /// <summary>
    /// This method returns the color the picture holds at a point, as the file holds it: read between the four texels
    /// nearest it, or averaged over a patch.
    /// </summary>
    /// <param name="point">The point, in the pattern's own space.</param>
    /// <param name="footprint">The patch to average over, if any.</param>
    /// <returns>The color there; clear where the picture is not laid.</returns>
    internal Color ColorAt(Point point, Footprint footprint)
    {
        Texture texture = Texture;
        ImageLaying laid = Laid();
        ImageMapType? map = laid.Map;
        int width = texture.Width, height = texture.Height;
        (double u, double v) = map.GetTexelLocationFor(point, width, height, laid.Ring);

        if (double.IsNaN(u) || double.IsNaN(v))
            return Colors.Transparent;

        if (footprint is null || footprint.IsEmpty)
            return texture.Bilinear(u, v, laid.WrapAcross, laid.WrapDown);

        (double du0, double dv0) = map.AxesFor(point, footprint.Across, width, height, laid.Ring);
        (double du1, double dv1) = map.AxesFor(point, footprint.Along, width, height, laid.Ring);

        return double.IsNaN(du0 + dv0 + du1 + dv1)
            ? texture.Bilinear(u, v, laid.WrapAcross, laid.WrapDown)
            : texture.Filtered(u, v, du0, dv0, du1, dv1, laid.WrapAcross, laid.WrapDown);
    }

    /// <summary>
    /// This method returns how fast the place in the picture changes about a point: the way across the picture and
    /// the way down it each climb fastest, as many texels a unit as they climb.
    /// </summary>
    /// <param name="point">The point, in the pattern's own space.</param>
    /// <returns>The two rates, or <c>null</c> for a point the picture cannot be laid at.</returns>
    internal (Vector Across, Vector Down)? RatesAt(Point point)
    {
        Texture texture = Texture;
        ImageLaying laid = Laid();
        ImageMapType? map = laid.Map;
        (double ux, double vx) = map.AxesFor(point, new Vector(Measure, 0, 0), texture.Width, texture.Height, laid.Ring);
        (double uy, double vy) = map.AxesFor(point, new Vector(0, Measure, 0), texture.Width, texture.Height, laid.Ring);
        (double uz, double vz) = map.AxesFor(point, new Vector(0, 0, Measure), texture.Width, texture.Height, laid.Ring);

        if (double.IsNaN(ux + vx + uy + vy + uz + vz))
            return null;

        return (new Vector(ux, uy, uz) / Measure, new Vector(vx, vy, vz) / Measure);
    }

    /// <summary>
    /// This method returns the way the picture climbs about a point, and how steeply, taken along the picture's own
    /// axes: from a texel either way across it and a texel either way down it, or as far as the patch the ray covers
    /// reaches, if that is further.
    /// <para>
    /// Between its texels a picture is read along straight lines, so its slope is the same from one texel's middle
    /// to the next and jumps at each, and taken over a thousandth of a unit -- as a pattern's slope is -- a picture of
    /// a thousand texels a unit roughens a surface into a grid of facets.  Taken from a texel either side, the slope
    /// at a texel's middle is the one its two neighbors make, and slides evenly to the next.  Taken along the picture's
    /// axes rather than X, Y and Z, it is a texel whatever shape the texels are and however the picture is laid.  Far
    /// off, each sample is averaged over the patch the ray covers, and the slope of an average is the average of the
    /// slope, so the picture's bumps are smoothed as its colors are.
    /// </para>
    /// </summary>
    /// <param name="point">The point, in the pattern's own space.</param>
    /// <param name="patch">The patch the ray covers there, in the pattern's own space, if any.</param>
    /// <returns>The gradient there, or <c>null</c> for a point the picture cannot be laid at.</returns>
    public override Vector SlopeAt(Point point, Footprint patch)
    {
        if (RatesAt(point) is not var (across, down))
            return null;

        // The steps that move one texel across the picture and none down it, and one down and none across.
        double uu = across.Dot(across), vv = down.Dot(down), uv = across.Dot(down);
        double determinant = uu * vv - uv * uv;

        if (determinant <= 0)
            return null;

        Vector alongAcross = (across * vv - down * uv) / determinant;
        Vector alongDown = (down * uu - across * uv) / determinant;
        (double reachAcross, double reachDown) = (1, 1);

        if (patch is not null && !patch.IsEmpty)
        {
            Texture texture = Texture;
            ImageLaying laid = Laid();
            ImageMapType? map = laid.Map;
            (double du0, double dv0) = map.AxesFor(point, patch.Across, texture.Width, texture.Height, laid.Ring);
            (double du1, double dv1) = map.AxesFor(point, patch.Along, texture.Width, texture.Height, laid.Ring);

            if (!double.IsNaN(du0 + dv0 + du1 + dv1))
            {
                reachAcross = Math.Max(1, Math.Max(Math.Abs(du0), Math.Abs(du1)) / 2);
                reachDown = Math.Max(1, Math.Max(Math.Abs(dv0), Math.Abs(dv1)) / 2);
            }
        }

        double slopeAcross = (ValueFor(point + alongAcross * reachAcross, patch) -
                              ValueFor(point - alongAcross * reachAcross, patch)) / (2 * reachAcross);
        double slopeDown = (ValueFor(point + alongDown * reachDown, patch) -
                            ValueFor(point - alongDown * reachDown, patch)) / (2 * reachDown);

        return across * slopeAcross + down * slopeDown;
    }

    /// <summary>
    /// This method returns the pattern's value from a color the picture holds, held under one.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The value it stands for.</returns>
    private double ValueOf(Color color) => Math.Min(BelowOne, Channel switch
    {
        ImageChannel.Red => color.Red,
        ImageChannel.Green => color.Green,
        ImageChannel.Blue => color.Blue,
        ImageChannel.Alpha => color.Alpha,
        _ => color.AsGray().Red
    });

    /// <summary>
    /// This method returns whether the given pattern reads the same picture the same way as this one.
    /// </summary>
    /// <param name="pattern">The pattern to match against.</param>
    /// <returns><c>true</c>, if the two match, or <c>false</c>, if not.</returns>
    protected override bool DetailsMatch(Pattern pattern)
    {
        ImagePattern other = (ImagePattern) pattern;

        return base.DetailsMatch(pattern) && ImageReference.Matches(other.ImageReference) && MapType == other.MapType &&
               Once == other.Once && Channel == other.Channel;
    }
}
