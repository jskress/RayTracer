using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This is the base class for all pieces of geometry.
/// </summary>
public abstract class Surface : NamedThing
{
    private static long _surfacesMade;

    /// <summary>
    /// This property carries the order in which this surface came into being, and exists to settle
    /// which of two surfaces is shaded when a ray meets both at exactly the same distance.
    /// <para>
    /// Such a tie is commoner than it sounds -- two solids meeting exactly at a face, a cube
    /// subtracted from another so that their sides are coplanar -- and something has to decide it.
    /// What decided it before was the order the crossings happened to be handed to the sorter, which
    /// is to say nothing decided it: <c>List.Sort</c> is not a stable sort, so equal keys come out in
    /// whatever order the sort's own bookkeeping left them.  Change how the geometry is walked and the
    /// answer changes.  That was measurable -- reversing the walk over a group's children moved two
    /// pixels of one gallery scene, and introducing the tree of boxes moved two others.
    /// </para>
    /// <para>
    /// Counting surfaces as they are built gives an order that is the order they are written in, since
    /// a scene file is read once, start to finish, on one thread.  So the tie goes to whichever surface
    /// the author mentioned first, which is at least a reason, and it is the same reason on every run
    /// however the geometry is arranged for searching.
    /// </para>
    /// </summary>
    internal long Ordinal { get; } = Interlocked.Increment(ref _surfacesMade);

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
    /// This property holds how many places the stuff inside this surface is looked at from when
    /// it lights the scene, or <c>null</c> when it does not light the scene at all.
    /// <para>
    /// A glowing medium is otherwise seen and not felt: it adds its light to rays that pass
    /// through it, so a flame is bright to look at, and nothing carries that light out to the
    /// ground.  Saying this turns the stuff inside into a light as well.
    /// </para>
    /// </summary>
    public int? GivesLightSamples { get; set; }

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
    /// This method returns the box a child of ours occupies in <i>our</i> space, or <c>null</c> when
    /// the child cannot say where it is and so nothing may be ruled out on its behalf.
    /// <para>
    /// A child that moves is taken in every place it stands while the shutter is open, not merely
    /// where it starts.  A box drawn around its first position alone would turn away rays that ought
    /// to have found it further along its travels, and the thing would be cut off part way through
    /// its own blur.  Since a ray only ever sees one of the instants sampled, gathering exactly those
    /// is no approximation of the path swept -- it is the whole of what any ray can find.
    /// </para>
    /// </summary>
    /// <param name="surface">The child to place.</param>
    /// <returns>The box it occupies here, or <c>null</c> if it has none.</returns>
    protected static BoundingBox BoxAround(Surface surface)
    {
        BoundingBox box = new ();

        foreach (Matrix transform in surface.TransformsThroughShutter)
        {
            if (surface.BoundingBox != null)
                box.Add(surface.BoundingBox.TransformedBy(transform));
            else if (surface is Triangle triangle)
            {
                box.Add(transform * triangle.Point1);
                box.Add(transform * triangle.Point2);
                box.Add(transform * triangle.Point3);
            }
            else
                return null;
        }

        // Empty is returned as empty rather than as nothing, for the same reason a group returns an
        // empty box for having nothing in it: a child that occupies no region can be hit by no ray,
        // where a child with no box at all has to be treated as though it could be hit by any of
        // them.  Returning null here put the second meaning on the first case, and since a group is
        // unbounded the moment any child is, that one wrong answer travelled all the way up.
        return box;
    }

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
    /// <summary>
    /// This method finds where a ray crosses this surface, but only within a stretch of the ray, and
    /// is for asking whether anything stands between a point and a light.
    /// <para>
    /// A shadow query throws away every crossing behind the point it started from and every crossing
    /// past the light, so a box lying wholly in either of those stretches holds nothing the query
    /// could use, and the surfaces inside it need never be asked.  On the teapot scene that is about
    /// two crossings in five.
    /// </para>
    /// <para>
    /// <b>The bound is not passed to the surface itself, only used to rule its box out.</b>  That is
    /// what keeps this from breaking a CSG surface: a CSG works out what is solid by walking every
    /// crossing of both its halves in order, and needs the ones behind the ray's origin to know
    /// whether it started inside.  Ruling out a CSG whose whole box is behind the point is fine --
    /// every crossing it could report would have been discarded anyway -- but truncating the list
    /// *inside* one would change what it thinks is solid.  So the bound travels only as far as the
    /// next box, and a surface asked to intersect is always asked in full.
    /// </para>
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    /// <param name="maxDistance">How far along the ray to care about; anything at or past this, or
    /// behind the origin, may be ignored.  Infinite for a light with no near side.</param>
    public void IntersectWithin(Ray ray, List<Intersection> intersections, double maxDistance)
    {
        ray = InverseTransformAt(ray.TimeIndex).Transform(ray);

        if (BoundingBox is not null)
        {
            (double from, double to) = BoundingBox.GetIntersections(ray);

            // A miss, or a box lying wholly behind the point, or wholly past the light.  Reaching
            // this method is itself what licenses the last two: only a shadow query asks this way,
            // and a shadow query discards exactly those crossings.  An ordinary intersection goes
            // through Intersect instead, and keeps them -- CSG counts the ones behind the origin to
            // work out what is solid, and refraction needs them to tell leaving from entering.
            //
            // Note that the behind test does not depend on the far bound at all.  A sky light has no
            // near side, so its distance is infinite and nothing can ever be past it; tying the two
            // together, as this first did, quietly bought nothing on every scene lit by one.
            if (from > to || to < 0 || from > maxDistance)
                return;
        }

        AddIntersectionsWithin(ray, intersections, maxDistance);
    }

    /// <summary>
    /// This method hands a bounded query on to the surface.  Only a group has anything to do with the
    /// bound -- it passes it down to its own children -- and every other surface simply answers in
    /// full, which is what makes the bound safe to give to anything at all.
    /// </summary>
    protected virtual void AddIntersectionsWithin(
        Ray ray, List<Intersection> intersections, double maxDistance)
    {
        AddIntersections(ray, intersections);
    }

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
        // coloring along with it.
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
    /// This method handles converting the given point from the surface's coordinate system to the
    /// world's, which is <see cref="WorldToSurface"/> walked the other way.
    /// <para>
    /// It exists for the things that have something to say about a place *inside* a surface and must
    /// say it out in the world: a light made of the stuff filling a surface has to tell a point being
    /// shaded which way to look and how far, and both of those are the world's business.
    /// </para>
    /// </summary>
    /// <param name="point">The point to convert.</param>
    /// <param name="timeIndex">Which instant to convert at.</param>
    /// <returns>The point, in the world's coordinate system.</returns>
    public Point SurfaceToWorld(Point point, int timeIndex = 0)
    {
        Matrix forward = _movingTransforms is null ? Transform : _movingTransforms[timeIndex];

        point = forward * point;

        return Parent is null ? point : Parent.SurfaceToWorld(point, timeIndex);
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
