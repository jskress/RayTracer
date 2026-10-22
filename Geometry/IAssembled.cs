namespace RayTracer.Geometry;

/// <summary>
/// This interface marks a surface assembled from parts of its own -- a <see cref="Tube"/> from its segments, a
/// rounded <see cref="Extrusion"/> from its faces, tubes and walls -- held as one surface, the union of them,
/// once it is prepared.  What walks a scene's surfaces walks into it, for what it asks of every surface the
/// rays meet: materials, shadows, weather, what turns light, and which side of a CSG a crossing is on.
/// </summary>
public interface IAssembled
{
    /// <summary>
    /// This property holds the union of the surface's parts, once it is prepared; <c>null</c> before, or for a
    /// surface that is not assembled after all.
    /// </summary>
    Surface Root { get; }
}
