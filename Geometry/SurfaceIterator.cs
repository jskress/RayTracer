namespace RayTracer.Geometry;

/// <summary>
/// This class provides an enumerator over a collection of surfaces.  The surfaces are
/// produced in a top-down, then left-to-right order.
/// </summary>
public class SurfaceIterator
{
    /// <summary>
    /// This property creates a new iteration over our set of surfaces and all their
    /// descendents.
    /// </summary>
    public IEnumerable<Surface> Surfaces => GetSurfaces();

    private readonly Surface[] _surfaces;

    public SurfaceIterator(Surface surface) : this([surface]) {}

    public SurfaceIterator(IEnumerable<Surface> surfaces)
    {
        _surfaces = surfaces.ToArray();
    }

    /// <summary>
    /// This method produces an enumerator over our collection of surfaces, in depth-first
    /// order.
    /// </summary>
    /// <returns>An enumerator over our surfaces, in a top-down order.</returns>
    private IEnumerable<Surface> GetSurfaces()
    {
        return GetSurfaces(_surfaces, []);
    }

    /// <summary>
    /// This method produces an enumerator over the given collection of surfaces, in
    /// depth-first order.
    /// </summary>
    /// <param name="surfaces">The collection of surfaces to iterate over.</param>
    /// <returns>An enumerator over our surfaces, in a top-down order.</returns>
    private static IEnumerable<Surface> GetSurfaces(
        IEnumerable<Surface> surfaces, HashSet<Surface> shared)
    {
        foreach (Surface surface in surfaces)
        {
            switch (surface)
            {
                case Group group:
                    foreach (Surface child in GetSurfaces(group.Surfaces, shared))
                        yield return child;
                    break;
                case CsgSurface csgSurface:
                    foreach (Surface child in GetSurfaces([csgSurface.Left, csgSurface.Right], shared))
                        yield return child;
                    break;
                case Tube { Root: not null } tube:
                    foreach (Surface child in GetSurfaces([tube.Root], shared))
                        yield return child;
                    break;

                // **A shared shape has to be walked, and walked exactly once.**  Everything that
                // settles a surface before rendering -- its material, the scene's ambient scaling, the
                // seed its pigment is sown with -- is done by walking the scene this way, and a shape
                // standing behind an instance was reached by none of it.  Its surfaces came out
                // unseeded and undressed, and the picture was quietly a little different.
                //
                // Once, because the shape is one shape however many instances point at it, and
                // handing it out again per instance would settle it over and over.
                case Instance instance when shared.Add(instance.Prototype):
                    foreach (Surface child in GetSurfaces([instance.Prototype], shared))
                        yield return child;
                    break;
            }

            yield return surface;
        }
    }
}
