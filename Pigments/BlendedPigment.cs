using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This class provides a pigment that returns a color that is a blend of other colors.
/// </summary>
public class BlendedPigment : Pigment
{
    /// <summary>
    /// This property holds the list of pigments to blend or layer.
    /// </summary>
    public List<Pigment> Pigments { get; init; } = [];

    /// <summary>
    /// This property indicates that we will blend by layering (combining based on alpha)
    /// as opposed to averaging.
    /// </summary>
    public bool Layer { get; init; }

    /// <summary>
    /// This method is used to push any random number generator seeds throughout the pigment
    /// tree.
    /// </summary>
    /// <param name="seed">The seed value to set.</param>
    public override void SetSeed(int seed)
    {
        foreach (Pigment pigment in Pigments.Where(pigment => !pigment.Seed.HasValue))
            pigment.SetSeed(seed);
    }

    /// <summary>
    /// This method passes the chance to get ready along to each pigment we blend.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="surface">The surface that this pigment is set on.</param>
    protected override void PrepareForRendering(RenderContext context, Surface surface)
    {
        foreach (Pigment pigment in Pigments)
            pigment.RenderingIsAboutToStart(context, surface);
    }

    /// <summary>
    /// This method accepts a point and produces a color for that point.  The color we
    /// return is based on a blend of colors from our child pigments at the given point.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
    /// <summary>
    /// A blend lets light through if any of the pigments it blends does.
    /// </summary>
    public override bool MayTransmit => Pigments.Any(pigment => pigment.MayTransmit);

    public override Color GetColorFor(Point point)
    {
        List<Color> colors = Pigments
            .Select(p => p.GetTransformedColorFor(point))
            .ToList();

        return Layer ? Colors.Layer(colors) : Colors.Average(colors);
    }

    /// <summary>
    /// This method is the same, carrying the patch down to every pigment in the blend, so that a
    /// pattern layered inside one is filtered like any other.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <param name="footprint">The patch around it, in this pigment's own space.</param>
    /// <returns>The appropriate color.</returns>
    public override Color GetColorFor(Point point, Footprint footprint)
    {
        if (footprint is null || footprint.IsEmpty)
            return GetColorFor(point);

        List<Color> colors = Pigments
            .Select(p => p.GetTransformedColorFor(point, footprint))
            .ToList();

        return Layer ? Colors.Layer(colors) : Colors.Average(colors);
    }

    /// <summary>
    /// This method returns whether the given pigmentation matches this one.
    /// </summary>
    /// <param name="other">The pigmentation to compare to.</param>
    /// <returns><c>true</c>, if the two pigmentations match, or <c>false</c>, if not.</returns>
    /// <summary>
    /// This property reports whether any of the blended pigments depends on the directions a surface is
    /// seen and lit from.
    /// </summary>
    public override bool SeesTheViewer => Pigments.Any(pigment => pigment.SeesTheViewer);

    /// <summary>
    /// This property reports whether any of the pigments this one blends sees what stands round it.
    /// </summary>
    public override bool SeesTheSurroundings => Pigments.Any(pigment => pigment.SeesTheSurroundings);

    /// <summary>
    /// This method blends the pigments, each told the directions the surface is being shaded between.
    /// </summary>
    /// <param name="point">The point, in the pigment's own space.</param>
    /// <param name="footprint">How much of the surface the ray covers there.</param>
    /// <param name="viewing">The directions to the eye and the light, and the way the surface faces.</param>
    /// <returns>The color there.</returns>
    public override Color GetColorFor(Point point, Footprint footprint, Viewing viewing)
    {
        if (viewing.IsEmpty)
            return base.GetColorFor(point, footprint, viewing);

        List<Color> colors = Pigments
            .Select(p => p.GetTransformedColorFor(point, footprint, viewing))
            .ToList();

        return Layer ? Colors.Layer(colors) : Colors.Average(colors);
    }

    public override bool Matches(Pigment other)
    {
        if (other is not BlendedPigment blendedPigment ||
            Pigments.Count != blendedPigment.Pigments.Count ||
            Layer != blendedPigment.Layer)
            return false;

        return !Pigments.Where((pigment, index) => !pigment.Matches(blendedPigment.Pigments[index]))
            .Any();
    }
}
