using System.Diagnostics.CodeAnalysis;

namespace RayTracer.Geometry;

/// <summary>
/// This class provides the common base class for surfaces that are extruded, like cylinders,
/// cones and generalized extrusions.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
public abstract class ExtrudedSurface : Surface
{
    /// <summary>
    /// This property holds the minimum value of Y for the extent of the extrusion.  This
    /// is in object space.
    /// </summary>
    public double MinimumY { get; set; } = -1;

    /// <summary>
    /// This property holds the maximum value of Y for the extent of the extrusion.  This
    ///  is in object space.
    /// </summary>
    public double MaximumY { get; set; } = 1;

    /// <summary>
    /// This property notes whether the extruded surface has end caps. It is relevant only
    /// when either or both <c>MinimumY</c> and <c>MaximumY</c> are not infinite.
    /// </summary>
    public bool Closed { get; set; } = true;

    /// <summary>
    /// This property reports whether the surface is left open at an end it has: a tube or a shade with nothing at its
    /// end, whose inner face is as much outside as its outer one, so that it has no inside to be in.  One that runs on
    /// for ever both ways encloses what is round its axis whatever is said of its ends.  It is not a sheet as far as
    /// light goes -- a cup lit as a sheet is, from either side, lit its inside through its own wall -- but nothing is in
    /// it: taken for a solid, the Ringworld's floor -- an open band round its star -- was met from within as a ray
    /// leaving its insides, and the air over the far side was never charged (until 2026-11-05).
    /// </summary>
    public bool IsOpen => !Closed && !(double.IsInfinity(MinimumY) && double.IsInfinity(MaximumY));
}
