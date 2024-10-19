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
        return GetSurfaces(_surfaces);
    }

    /// <summary>
    /// This method produces an enumerator over the given collection of surfaces, in
    /// depth-first order.
    /// </summary>
    /// <param name="surfaces">The collection of surfaces to iterate over.</param>
    /// <returns>An enumerator over our surfaces, in a top-down order.</returns>
    private static IEnumerable<Surface> GetSurfaces(IEnumerable<Surface> surfaces)
    {
        foreach (Surface surface in surfaces)
        {
            switch (surface)
            {
                case Group group:
                    foreach (Surface child in GetSurfaces(group.Surfaces))
                        yield return child;
                    break;
                case CsgSurface csgSurface:
                    foreach (Surface child in GetSurfaces([csgSurface.Left, csgSurface.Right]))
                        yield return child;
                    break;
            }

            yield return surface;
        }
    }
}
