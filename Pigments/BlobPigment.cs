using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This class provides the pigment a blob wears when its components carry pigments of their own.  It
/// mixes them in the proportion each component contributes to the field at the point being colored,
/// so the color changes over exactly where the shape does -- a ball melting into a bond takes the
/// bond's color across the same fillet it takes the bond's shape across.
/// <para>
/// It is not built by anyone writing a scene, and there is no word for it in the language.  A blob
/// puts one on once it knows some component wants a color of its own, wrapping whatever pigment the
/// blob's material already had so that components which said nothing keep speaking with the blob's
/// own voice.
/// </para>
/// </summary>
public class BlobPigment : Pigment
{
    /// <summary>
    /// A blend lets light through if anything it might blend does.
    /// </summary>
    public override bool MayTransmit => _fallback.MayTransmit ||
        _pigments.Any(pigment => pigment is not null && pigment.MayTransmit);

    private readonly Blob _blob;
    private readonly Pigment _fallback;
    private readonly Pigment[] _pigments;

    /// <summary>
    /// This constructs the pigment for a blob, over whatever pigment it already wore.
    /// </summary>
    /// <param name="blob">The blob whose components are to be mixed.</param>
    /// <param name="fallback">The pigment to speak with where no component has one.</param>
    public BlobPigment(Blob blob, Pigment fallback)
    {
        _blob = blob;
        _fallback = fallback;
        _pigments = blob.Components
            .Select(component => component.Pigment)
            .ToArray();
    }

    /// <summary>
    /// This method is used to push any random number generator seeds throughout the pigment tree.
    /// </summary>
    /// <param name="seed">The seed value to set.</param>
    public override void SetSeed(int seed)
    {
        foreach (Pigment pigment in Everything().Where(pigment => !pigment.Seed.HasValue))
            pigment.SetSeed(seed);
    }

    /// <summary>
    /// This method passes the chance to get ready along to every pigment we might mix.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="surface">The surface that this pigment is set on.</param>
    protected override void PrepareForRendering(RenderContext context, Surface surface)
    {
        foreach (Pigment pigment in Everything())
            pigment.RenderingIsAboutToStart(context, surface);
    }

    /// <summary>
    /// This method accepts a point and produces the color the components mix to there.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
    public override Color GetColorFor(Point point)
    {
        return GetColorFor(point, null);
    }

    /// <summary>
    /// This method is the same, carrying the patch down to each pigment it mixes, so that a pattern
    /// on one component is filtered like any other.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <param name="footprint">The patch around it, or <c>null</c>, for a point sample.</param>
    /// <returns>The appropriate color for that patch.</returns>
    public override Color GetColorFor(Point point, Footprint footprint)
    {
        Span<double> weights = stackalloc double[_pigments.Length];
        double total = _blob.GetComponentWeights(point, weights);

        // Nothing is adding to the field here, so there is nothing to apportion the color between.
        // A point on the surface is not normally one of these, but a shadow or a stray query may be,
        // and the blob's own pigment is the right thing to answer with.
        if (total <= 0)
            return ColorFrom(_fallback, point, footprint);

        Color color = Colors.Black;
        double alpha = 0;

        for (int index = 0; index < _pigments.Length; index++)
        {
            if (weights[index] <= 0)
                continue;

            double share = weights[index] / total;
            Color part = ColorFrom(_pigments[index] ?? _fallback, point, footprint);

            // The alpha is carried by hand because adding colors does not carry it, and a component
            // wearing a pigment that lets light through must be able to say so through the mix.
            color += part * share;
            alpha += part.Alpha * share;
        }

        return color.WithAlpha(alpha);
    }

    /// <summary>
    /// This method returns whether the given pigment matches this one, which asks that they dress
    /// the very same blob: the mixing is by that blob's own field, so two of these over different
    /// blobs would give different colors from the same components.
    /// </summary>
    /// <param name="other">The pigment to compare to.</param>
    /// <returns><c>true</c>, if the two pigments match, or <c>false</c>, if not.</returns>
    public override bool Matches(Pigment other)
    {
        if (other is not BlobPigment blobPigment ||
            !ReferenceEquals(_blob, blobPigment._blob) ||
            !_fallback.Matches(blobPigment._fallback) ||
            _pigments.Length != blobPigment._pigments.Length)
            return false;

        for (int index = 0; index < _pigments.Length; index++)
        {
            Pigment ours = _pigments[index];
            Pigment theirs = blobPigment._pigments[index];

            if (ours is null != theirs is null)
                return false;

            if (ours is not null && !ours.Matches(theirs))
                return false;
        }

        return true;
    }

    /// <summary>
    /// This method asks one pigment for its color, in its own space, for a point or for a patch.
    /// </summary>
    /// <param name="pigment">The pigment to ask.</param>
    /// <param name="point">The point to ask about.</param>
    /// <param name="footprint">The patch around it, or <c>null</c>, for a point sample.</param>
    /// <returns>The color that pigment gives.</returns>
    private static Color ColorFrom(Pigment pigment, Point point, Footprint footprint)
    {
        return footprint is null || footprint.IsEmpty
            ? pigment.GetTransformedColorFor(point)
            : pigment.GetTransformedColorFor(point, footprint);
    }

    /// <summary>
    /// This method returns every pigment this one might reach, which is each component's and the one
    /// standing behind them all.
    /// </summary>
    /// <returns>The pigments to pass anything along to.</returns>
    private IEnumerable<Pigment> Everything()
    {
        return _pigments
            .Where(pigment => pigment is not null)
            .Append(_fallback);
    }
}
