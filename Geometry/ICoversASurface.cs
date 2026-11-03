using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This interface is what a thing that fills itself over the whole of another surface says of where on it it may
/// stand -- a field covering a rock with barnacles, ivy climbing a wall.  Both find their places by
/// <see cref="SurfaceSpread"/> and keep or leave each by the same rules, so a tide line or a patch of moss means the
/// same to either.
/// </summary>
internal interface ICoversASurface
{
    /// <summary>
    /// This property holds the transform carrying the thing's own space, where the places are found, into the one
    /// it shares with the surface.
    /// </summary>
    Matrix Transform { get; }

    /// <summary>
    /// This property holds the lowest a place may be, in the space shared with the surface, or <c>null</c> for no
    /// limit.
    /// </summary>
    double? Above { get; }

    /// <summary>
    /// This property holds the highest a place may be, in the space shared with the surface, or <c>null</c> for no
    /// limit.
    /// </summary>
    double? Below { get; }

    /// <summary>
    /// This property holds the steepest a place may be, as an angle from level, in radians, or <c>null</c> for no
    /// limit.
    /// </summary>
    double? Steepest { get; }

    /// <summary>
    /// This property holds the way the surface must face at a place for it to be kept, or <c>null</c> for any.
    /// </summary>
    Vector Facing { get; }

    /// <summary>
    /// This property holds how strongly places favor the surface facing squarely the way they must.
    /// </summary>
    double FacingPower { get; }

    /// <summary>
    /// This property holds the pattern whose value at a place is the chance it is kept, or <c>null</c> for every
    /// place.
    /// </summary>
    DensityShape Patches { get; }

    /// <summary>
    /// This property holds the patterned value below which a place is left.
    /// </summary>
    double PatchesFrom { get; }

    /// <summary>
    /// This property holds the patterned value at and above which a place is always kept.
    /// </summary>
    double PatchesTo { get; }
}
