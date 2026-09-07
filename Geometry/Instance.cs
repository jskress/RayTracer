using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class stands in a scene for a shape that is built once and used in many places.
/// <para>
/// **The saving is in the building, not in the testing.**  A ray still has to be tried against the
/// shape wherever it stands, so an instance costs what any surface costs to intersect.  What it does
/// not cost is being *made*: one elm's geometry takes about half a second to build and is then kept
/// whole in memory, and a wood of two hundred of them was two hundred of each.  A leaf is the same
/// blade ten thousand times over.
/// </para>
/// <para>
/// **The one hard part is that a surface has exactly one parent.**  Everything that carries a point
/// or a normal between the world and a surface -- <see cref="Surface.WorldToSurface(Point,int,
/// Surface)"/> and its kin -- walks up the parent chain, and a shape reachable from a dozen places
/// cannot say which of them it is standing in.  So the *hit* carries the answer instead: every
/// crossing found through this instance is stamped with it, and where the parent chain runs out the
/// walk carries on through the stamp.  See <see cref="Intersection.Portal"/>.
/// </para>
/// </summary>
public class Instance : Surface
{
    /// <summary>
    /// This property holds the shape this instance stands for, which it shares with every other
    /// instance of it.
    /// </summary>
    public Surface Prototype { get; init; }

    /// <summary>
    /// This method gets the shared shape ready, which it may already be: it is reached once by every
    /// instance pointing at it, and its box would be padded again each time.  The flag lives on the
    /// shape rather than here, since it is the shape that must only be readied once however many
    /// instances point at it.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        if (!Prototype.SharedAndReady)
        {
            Prototype.SharedAndReady = true;

            Prototype.PrepareForRendering(SampleTimes);
        }

        // An instance inside a shared shape would need the hit to carry *both* of them, in order, so
        // that the walk could come out through each in turn.  That is a chain and an allocation on
        // the hottest path in the renderer, and nothing yet asks for it -- a wood varies its trees by
        // their variant, which is an argument, so the trees do not share and only their leaves do.
        // It is refused here rather than quietly getting the second one wrong.
        if (HoldsAnInstance(Prototype))
        {
            throw new Exception(
                "A shape that is shared cannot itself hold a shared shape.  Both would have to be " +
                "unwound to say where a point on it lies, and only the nearer one is remembered.");
        }
    }

    /// <summary>
    /// This method reports whether a shape may stand in more than one place.
    /// <para>
    /// Each of the things that says no would be *wrong* rather than merely unshared, and each is
    /// checked on the shape itself rather than guessed at from whoever is asking.  A caller that is
    /// told no must build the shape again for each place it stands.
    /// </para>
    /// </summary>
    /// <param name="surface">The shape to consider.</param>
    /// <returns><c>true</c>, if it may be shared.</returns>
    public static bool MayBeShared(Surface surface) => surface switch
    {
        null => false,

        // A shape reachable from two places has no one place to be, and a light made of the stuff
        // inside it has to be somewhere.
        _ when surface.GivesLightSamples is not null => false,

        // The medium inside a thing is looked up by carrying a point into the *containing* surface's
        // space, and that lookup has no hit to take a portal from.
        _ when surface.Material?.Interior?.Medium is not null => false,

        // Where a thing stands changes while the shutter is open, and an instance would have to carry
        // its own motion as well as the shape's.
        //
        // **Both callers ask before the shape has been prepared**, having only just built it, and
        // `Moves` is not set until preparation works out where the shape stands at each instant.  So
        // the question is put to `MotionAt`, which is there from the moment the shape is made; asking
        // `Moves` alone was a guard that could never fire.
        _ when surface.Moves || surface.MotionAt is not null => false,

        // Both of these would need a hit to remember two instances rather than one, which is what
        // PrepareSurfaceForRendering refuses outright.  Answering here lets a caller build its own
        // copies quietly instead of ending the render with a complaint no scene can act on.
        Instance => false,

        Group group => group.Surfaces.All(MayBeShared),
        CsgSurface csg => MayBeShared(csg.Left) && MayBeShared(csg.Right),
        _ => true
    };

    /// <summary>
    /// This method reports whether a shape holds an instance anywhere inside it.
    /// </summary>
    /// <param name="surface">The shape to look through.</param>
    /// <returns><c>true</c>, if there is an instance somewhere within it.</returns>
    private static bool HoldsAnInstance(Surface surface) => surface switch
    {
        Instance => true,
        Group group => group.Surfaces.Any(HoldsAnInstance),
        CsgSurface csg => HoldsAnInstance(csg.Left) || HoldsAnInstance(csg.Right),
        _ => false
    };

    /// <summary>
    /// This method returns the box the shared shape occupies, which is the box this instance occupies
    /// too -- the instance's own transform is applied to it from outside, as it is for any surface.
    /// </summary>
    /// <returns>The region the shared shape covers.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        return BoxAround(Prototype);
    }

    /// <summary>
    /// This method returns the region the shared shape really covers, padding taken off.
    /// </summary>
    /// <returns>The region the shared shape covers.</returns>
    internal override BoundingBox TrueBoundingBox()
    {
        return BoxAround(Prototype, true);
    }

    /// <summary>
    /// This method hands the ray on to the shared shape and stamps whatever it finds.
    /// <para>
    /// The ray has already been carried into this instance's own space by
    /// <see cref="Surface.Intersect"/>, so what the shape is given is the ray as it would look to a
    /// shape standing here -- which is the whole trick.  Only the crossings this call added are
    /// stamped; the list arrives with other surfaces' crossings already in it.
    /// </para>
    /// </summary>
    /// <param name="ray">The ray to try, in this instance's own space.</param>
    /// <param name="intersections">The list to add any crossings to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        int before = intersections.Count;

        Prototype.Intersect(ray, intersections);

        for (int index = before; index < intersections.Count; index++)
            intersections[index].Portal = this;
    }

    /// <summary>
    /// This method answers a shadow query the same way, keeping the shape's own right to throw away
    /// crossings it knows a shadow ray cannot want.
    /// </summary>
    /// <param name="ray">The ray to try, in this instance's own space.</param>
    /// <param name="intersections">The list to add any crossings to.</param>
    /// <param name="maxDistance">How far along the ray anything can matter.</param>
    protected override void AddIntersectionsWithin(
        Ray ray, List<Intersection> intersections, double maxDistance)
    {
        int before = intersections.Count;

        Prototype.IntersectWithin(ray, intersections, maxDistance);

        for (int index = before; index < intersections.Count; index++)
            intersections[index].Portal = this;
    }

    /// <summary>
    /// This method is never called: a crossing found through an instance names the shape's own
    /// surface, not the instance, so it is that surface which is asked for the normal.
    /// </summary>
    /// <param name="point">The point to find the normal at.</param>
    /// <param name="intersection">The intersection being shaded.</param>
    /// <returns>Nothing; this always throws.</returns>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        throw new NotSupportedException(
            "An instance is never itself the surface a ray met; the shape it stands for is.");
    }
}
