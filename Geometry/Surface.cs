using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This is the base class for all pieces of geometry.
/// </summary>
public abstract class Surface : NamedThing
{
    /// <summary>
    /// This property holds a reference to the parent of the surface, if there is one.
    /// </summary>
    public Surface Parent { get; set; }

    /// <summary>
    /// This holds the material for the surface.
    /// </summary>
    public Material Material { get; set; } = new ();

    /// <summary>
    /// This property suppresses shadow detection on this object.
    /// </summary>
    public bool NoShadow { get; set; }

    /// <summary>
    /// This property holds the transform for the surface for converting from world to
    /// surface space.
    /// </summary>
    public Matrix Transform
    {
        get => _transform;
        set
        {
            _transform = value;

            if (_inverseTransform.IsValueCreated)
                _inverseTransform = new Lazy<Matrix>(CreateInverseTransform);
        }
    }

    /// <summary>
    /// This property provides the inverse of the surface's transform.
    /// </summary>
    private Matrix InverseTransform => _inverseTransform.Value;

    /// <summary>
    /// This property provides the transposed inverse of the surface's transform.
    /// </summary>
    private Matrix TransformedInverseTransform => _transposedInverseTransform.Value;

    private Matrix _transform;
    private Lazy<Matrix> _inverseTransform;
    private Lazy<Matrix> _transposedInverseTransform;

    protected Surface()
    {
        _transform = Matrix.Identity;
        _inverseTransform = new Lazy<Matrix>(CreateInverseTransform);
        _transposedInverseTransform = new Lazy<Matrix>(CreateTransposedInverseTransform);
    }

    /// <summary>
    /// This method creates the inverse of our transformation matrix.
    /// </summary>
    /// <returns>The inverse of our transformation matrix.</returns>
    private Matrix CreateInverseTransform()
    {
        if (_transposedInverseTransform.IsValueCreated)
            _transposedInverseTransform = new Lazy<Matrix>(CreateTransposedInverseTransform);

        return _transform.Invert();
    }

    /// <summary>
    /// This method creates the transposed inverse of our transformation matrix.
    /// </summary>
    /// <returns>The transposed inverse of our transformation matrix.</returns>
    private Matrix CreateTransposedInverseTransform()
    {
        return InverseTransform.Transpose();
    }

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to
    /// perform any expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    public virtual void PrepareForRendering() {}

    /// <summary>
    /// This method must be provided by subclasses to determine whether the given
    /// ray intersects the geometry and, if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public void Intersect(Ray ray, List<Intersection> intersections)
    {
        AddIntersections(InverseTransform.Transform(ray), intersections);
    }

    /// <summary>
    /// This method must be provided by subclasses to determine whether the given
    /// ray intersects the geometry and, if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public abstract void AddIntersections(Ray ray, List<Intersection> intersections);

    /// <summary>
    /// This method calculate the normal for the surface at the specified point.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public Vector NormaAt(Point point, Intersection intersection)
    {
        Vector normal = SurfaceNormaAt(WorldToSurface(point), intersection);

        return NormalToWorld(normal);
    }

    /// <summary>
    /// This method should calculate the normal for the surface at the specified point.
    /// The point will have been transformed to surface-space coordinates.  The vector
    /// returned should also be in surface-space coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public abstract Vector SurfaceNormaAt(Point point, Intersection intersection);

    /// <summary>
    /// This method handles converting a given point from the world's coordinate system to
    /// the surface's.
    /// </summary>
    /// <param name="point">The point to convert.</param>
    /// <returns>The converted point.</returns>
    public Point WorldToSurface(Point point)
    {
        if (Parent != null)
            point = Parent.WorldToSurface(point);

        return InverseTransform * point;
    }

    /// <summary>
    /// This method handles converting a given normal from the surface's coordinate system
    /// to the world's
    /// </summary>
    /// <param name="normal">The normal to convert.</param>
    /// <returns>The converted normal.</returns>
    public Vector NormalToWorld(Vector normal)
    {
        normal = TransformedInverseTransform * normal;
        normal = normal.Clean().Unit;

        if (Parent != null)
            normal = Parent.NormalToWorld(normal);

        return normal;
    }

    /// <summary>
    /// This method decides whether the given child surface is, or is contained by, the
    /// given (potential) parent surface.
    /// </summary>
    /// <param name="parent">The surface to check whether it is, or contains, the child.</param>
    /// <param name="child">the child surface to test.</param>
    /// <returns><c>true</c>, if <c>child</c> either is, or is contained by, <c>parent</c>.</returns>
    protected static bool IsOrIncludes(Surface parent, Surface child)
    {
        return parent switch
        {
            Group group => group.Surfaces.Any(surface => IsOrIncludes(surface, child)),
            CsgSurface csgSurface => IsOrIncludes(csgSurface.Left, child) ||
                                     IsOrIncludes(csgSurface.Right, child),
            _ => parent == child
        };
    }

    /// <summary>
    /// This is a helper method that will push the given material down to all descendents
    /// who want it.  It will recurse as necessary.
    /// </summary>
    /// <param name="material">The material to apply.</param>
    public void SetMaterial(Material material)
    {
        Material ??= material;

        if (this is Group group)
        {
            foreach (Surface child in group.Surfaces)
                child.SetMaterial(material);
        }
        else if (this is CsgSurface csgSurface)
        {
            csgSurface.Left.SetMaterial(material);
            csgSurface.Right.SetMaterial(material);
        }
    }
}
