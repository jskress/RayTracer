using RayTracer.General;
using RayTracer.Pigments;

namespace RayTracer.Instructions.Pigments;

/// <summary>
/// This class holds what was said of how a picture laid over a whole plane is laid, besides its map: how wide a
/// triplanar picture's turns blend, and whether and how it is shuffled so that it does not repeat.  An image read as a
/// pigment, as a pattern and as a normal map all say it alike.
/// </summary>
public class ImageLayingResolver
{
    /// <summary>
    /// This property holds the resolver for how wide a triplanar picture's turns blend, if it was said.
    /// </summary>
    public Resolver<double> BlendResolver { get; init; }

    /// <summary>
    /// This property notes whether the picture is shuffled.
    /// </summary>
    public bool Shuffled { get; init; }

    /// <summary>
    /// This property holds the resolver for how far apart a shuffled picture's tiles stand, if it was said.
    /// </summary>
    public Resolver<double> SizeResolver { get; init; }

    /// <summary>
    /// This property holds the resolver for how far a shuffled picture's tiles may be turned, if it was said.
    /// </summary>
    public Resolver<double> TurnResolver { get; init; }

    /// <summary>
    /// This method returns how the picture is laid.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>How wide a triplanar picture's turns blend, and how it is shuffled, or <c>null</c>.</returns>
    public (double Blend, Shuffle Shuffle) Resolve(RenderContext context, Variables variables)
    {
        double blend = BlendResolver?.Resolve(context, variables) ?? ImageTaps.DefaultBlend;

        if (!Shuffled)
            return (blend, null);

        return (blend, new Shuffle(
            SizeResolver?.Resolve(context, variables) ?? Shuffle.DefaultSize,
            TurnResolver?.Resolve(context, variables) ?? 0));
    }
}
