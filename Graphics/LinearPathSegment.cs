using RayTracer.Basics;
using RayTracer.Extensions;

namespace RayTracer.Graphics;

/// <summary>
/// This class represents a linear segment of a general path.
/// </summary>
public class LinearPathSegment : PathSegment
{
    internal LinearPathSegment(TwoDPoint start, TwoDPoint end)
        : base(start, end) {}
}
