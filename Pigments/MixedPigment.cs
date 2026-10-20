using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This class provides a pigment that is two others mixed in a given proportion: what a material blended
/// part way between two others shows, its color mixed as its finish is.  See <see cref="Core.MaterialBlend"/>.
/// </summary>
public class MixedPigment : Pigment
{
    /// <summary>
    /// This property holds the pigment there is less of as the mix runs toward one.
    /// </summary>
    public Pigment From { get; init; }

    /// <summary>
    /// This property holds the pigment there is more of as the mix runs toward one.
    /// </summary>
    public Pigment To { get; init; }

    /// <summary>
    /// This property holds how far the mix has run from the one pigment to the other, from nought to one.
    /// </summary>
    public double Share { get; init; }

    /// <summary>
    /// This method pushes a seed down to both pigments, but not to one that has its own.
    /// </summary>
    /// <param name="seed">The seed value to set.</param>
    public override void SetSeed(int seed)
    {
        foreach (Pigment pigment in new[] { From, To }.Where(pigment => !pigment.Seed.HasValue))
            pigment.SetSeed(seed);
    }

    /// <summary>
    /// This method hands both pigments their chance to get ready.
    /// </summary>
    protected override void PrepareForRendering(RenderContext context, Surface surface)
    {
        From.RenderingIsAboutToStart(context, surface);
        To.RenderingIsAboutToStart(context, surface);
    }

    /// <summary>
    /// This property reports whether either pigment lets light through.
    /// </summary>
    public override bool MayTransmit => From.MayTransmit || To.MayTransmit;

    /// <summary>
    /// This property reports whether either pigment depends on the directions a surface is seen and lit from.
    /// </summary>
    public override bool SeesTheViewer => From.SeesTheViewer || To.SeesTheViewer;

    /// <summary>
    /// This property reports whether either pigment sees what stands round the place it is asked about.
    /// </summary>
    public override bool SeesTheSurroundings => From.SeesTheSurroundings || To.SeesTheSurroundings;

    /// <summary>
    /// This method returns the two pigments' colors at a point, mixed.
    /// </summary>
    public override Color GetColorFor(Point point) =>
        Mixed(From.GetTransformedColorFor(point), To.GetTransformedColorFor(point));

    /// <summary>
    /// This method returns the same, told how much of the surface the ray covers there.
    /// </summary>
    public override Color GetColorFor(Point point, Footprint footprint) =>
        Mixed(From.GetTransformedColorFor(point, footprint), To.GetTransformedColorFor(point, footprint));

    /// <summary>
    /// This method returns the same, told the directions the surface is seen and lit from.
    /// </summary>
    public override Color GetColorFor(Point point, Footprint footprint, Viewing viewing) =>
        Mixed(From.GetTransformedColorFor(point, footprint, viewing), To.GetTransformedColorFor(point, footprint, viewing));

    /// <summary>
    /// This method returns whether this pigment matches another: the same two pigments, mixed alike.
    /// </summary>
    public override bool Matches(Pigment other) =>
        other is MixedPigment mixed && From.Matches(mixed.From) && To.Matches(mixed.To) && Share.Near(mixed.Share);

    private Color Mixed(Color from, Color to) => from * (1 - Share) + to * Share;
}
