using RayTracer.Basics;

namespace RayTracer.Core;

/// <summary>
/// This class defines some common direction vectors.  All are of unit length.
/// </summary>
public static class Directions
{
    public static readonly Vector Up = new (0, 1, 0);
    public static readonly Vector Down = new (0, -1, 0);
    public static readonly Vector Left = new (-1, 0, 0);
    public static readonly Vector Right = new (1, 0, 0);
    public static readonly Vector In = new (0, 0, 1);
    public static readonly Vector Out = new (0, 0, -1);
}
