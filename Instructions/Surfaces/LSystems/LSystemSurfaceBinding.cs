using System.Text;

namespace RayTracer.Instructions.Surfaces.LSystems;

/// <summary>
/// This class ties one character to the surface an L-system should stamp when its production
/// names that character after a <c>~</c> -- so <c>~L</c> may grow a leaf where <c>~K</c> grows a
/// fruit.  It holds a resolver rather than a surface because each occurrence needs its own copy,
/// resolved afresh, and because resolving a named surface needs a render context that is only in
/// hand at resolve time.
/// </summary>
public class LSystemSurfaceBinding
{
    /// <summary>
    /// This property holds the character that names the surface in a production.
    /// </summary>
    public Rune Character { get; init; }

    /// <summary>
    /// This property holds the resolver for the surface that character names.
    /// </summary>
    public ISurfaceResolver Resolver { get; init; }
}
