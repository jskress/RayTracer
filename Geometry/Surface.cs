using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This is the base class for all pieces of geometry.
/// </summary>
public abstract class Surface : NamedThing
{
    /// <summary>
    /// This property holds a reference to the parent of the surface if there is one.
    /// </summary>
    public Surface Parent { get; set; }

    /// <summary>
    /// This property holds the seed for any randomness to use.
    /// If it is not specified, default randomness will be used where needed.
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>
    /// This holds the material for the surface.
    /// </summary>
    public Material Material { get; set; }

    /// <summary>
    /// This property suppresses shadow detection on this object.
    /// </summary>
    public bool NoShadow { get; set; }

    /// <summary>
    /// This property holds an optional bounding box for the group.
    /// </summary>
    public BoundingBox BoundingBox { get; set; }

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
    /// This property holds a recipe for where this surface stands part way through the shutter's
    /// opening, or null if it holds still.  It is given a fraction, nothing at the moment the
    /// shutter opens and one as it closes, and answers with the motion to lay over the surface's
    /// own transform.
    /// <para>
    /// It is a recipe rather than a matrix because a motion cannot be worked out until it is known
    /// how many instants the shutter will be sampled at, and that is the camera's business, settled
    /// long after the scene is read.
    /// </para>
    /// </summary>
    public Func<double, Matrix> MotionAt { get; set; }

    /// <summary>
    /// This property notes whether this surface moves while the shutter is open.
    /// </summary>
    public bool Moves => _movingTransforms is not null;

    /// <summary>
    /// This property holds the instants the camera looks at, kept while preparing so that a surface
    /// holding others within it may hand them on to its children.
    /// </summary>
    protected double[] SampleTimes { get; private set; }

    /// <summary>
    /// This property provides every place this surface stands while the shutter is open -- just
    /// the one, if it holds still.  A parent gathering its children into a bounding box needs all
    /// of them, since a box drawn around where a thing starts would have the group turn away rays
    /// that should have found it further along its travels.
    /// </summary>
    public IEnumerable<Matrix> TransformsThroughShutter =>
        _movingTransforms ?? [Transform];

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
    private Matrix[] _movingTransforms;
    private Matrix[] _movingInverses;
    private Matrix[] _movingTransposedInverses;

    /// <summary>
    /// This method returns the inverse transform to use for a ray that sees the scene at the given
    /// instant.  A surface that holds still has but the one, whichever instant is asked for.
    /// </summary>
    private Matrix InverseTransformAt(int timeIndex) =>
        _movingInverses is null ? InverseTransform : _movingInverses[timeIndex];

    /// <summary>
    /// This method returns the transposed inverse transform to use for a ray that sees the scene at
    /// the given instant, which is what carries a normal back out to the world.
    /// </summary>
    private Matrix TransposedInverseTransformAt(int timeIndex) =>
        _movingTransposedInverses is null
            ? TransformedInverseTransform
            : _movingTransposedInverses[timeIndex];

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
    public void PrepareForRendering() => PrepareForRendering(null);

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to
    /// perform any expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    /// <param name="sampleTimes">How far through the shutter's opening each of the camera's samples
    /// looks, or null when nothing is moving and there is but the one instant to see.</param>
    public void PrepareForRendering(double[] sampleTimes)
    {
        SampleTimes = sampleTimes;

        PrepareSurfaceForRendering();

        if (MotionAt is not null && sampleTimes is { Length: > 1 })
            BuildMotionTransforms(sampleTimes);

        BoundingBox ??= GetDefaultBoundingBox();

        BoundingBox?.Expand();
    }

    /// <summary>
    /// This method works out where this surface stands at each instant the camera looks, and the
    /// matrices for carrying rays into its space and normals back out at each of them.
    /// <para>
    /// They are worked out once, here, rather than for every ray, which is the whole reason a ray
    /// carries which instant it sees the scene at rather than the instant itself: there are only
    /// ever as many places to stand as there are samples, so the inverting -- much the dearest part
    /// -- is done a handful of times for the whole render instead of millions.
    /// </para>
    /// </summary>
    private void BuildMotionTransforms(double[] sampleTimes)
    {
        _movingTransforms = sampleTimes
            .Select(fraction => MotionAt(fraction) * Transform)
            .ToArray();
        _movingInverses = _movingTransforms
            .Select(matrix => matrix.Invert())
            .ToArray();
        _movingTransposedInverses = _movingInverses
            .Select(matrix => matrix.Transpose())
            .ToArray();
    }

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to
    /// perform any expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    protected virtual void PrepareSurfaceForRendering() {}

    /// <summary>
    /// This method may be overridden to produce a default bounding box for this
    /// shape.
    /// If the user specified one, it will not be replaced and this method
    /// will not be called.
    /// </summary>
    /// <returns>A default bounding box, if any, for the surface.</returns>
    protected virtual BoundingBox GetDefaultBoundingBox()
    {
        return null;
    }

    /// <summary>
    /// This method must be provided by subclasses to determine whether the given
    /// ray intersects the geometry and, if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public void Intersect(Ray ray, List<Intersection> intersections)
    {
        // A moving surface is carried into its own space by where it stands at the instant this
        // ray sees the scene, so the ray finds it where it was then.  The bounding box is tested
        // after that, in the surface's own space, where a motion does not reach -- the box a
        // moving thing needs to be judged against from outside is its parent's, which is drawn
        // wide enough to cover everywhere it goes.
        ray = InverseTransformAt(ray.TimeIndex).Transform(ray);

        if (BoundingBox == null || BoundingBox.IsHitBy(ray))
            AddIntersections(ray, intersections);
    }

    /// <summary>
    /// This method must be provided by subclasses to determine whether the given
    /// ray intersects the geometry and, if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public abstract void AddIntersections(Ray ray, List<Intersection> intersections);

    /// <summary>
    /// This method calculates the normal for the surface at the specified point.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public Vector NormaAt(Point point, Intersection intersection)
    {
        // Asked without an intersection to go on -- as a caller testing the geometry alone may do
        // -- there is no particular instant meant, so the surface is taken where it starts.
        int timeIndex = intersection?.TimeIndex ?? 0;
        Point surfacePoint = WorldToSurface(point, timeIndex);
        Vector normal = SurfaceNormaAt(surfacePoint, intersection);

        // Any roughening happens here, in surface space, which is the same footing the pigment is
        // evaluated on.  Doing it before the normal is carried out to the world means a surface
        // that has been scaled or turned takes its bumps along with it, exactly as it takes its
        // colouring along with it.
        if (Material?.SurfaceNormal is not null)
            normal = Material.SurfaceNormal.PerturbAt(normal, surfacePoint);

        return NormalToWorld(normal, timeIndex);
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
    /// <param name="timeIndex">Which instant of the shutter's opening to place the surface at.</param>
    /// <returns>The converted point.</returns>
    public Point WorldToSurface(Point point, int timeIndex = 0)
    {
        if (Parent != null)
            point = Parent.WorldToSurface(point, timeIndex);

        return InverseTransformAt(timeIndex) * point;
    }

    /// <summary>
    /// This method handles converting the given normal from the surface's coordinate system
    /// to the world's
    /// </summary>
    /// <param name="normal">The normal to convert.</param>
    /// <param name="timeIndex">Which instant of the shutter's opening to place the surface at.</param>
    /// <returns>The converted normal.</returns>
    public Vector NormalToWorld(Vector normal, int timeIndex = 0)
    {
        normal = TransposedInverseTransformAt(timeIndex) * normal;
        normal = normal.Clean().Unit;

        if (Parent != null)
            normal = Parent.NormalToWorld(normal, timeIndex);

        return normal;
    }
}
