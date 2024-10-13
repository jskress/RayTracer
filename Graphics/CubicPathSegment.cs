using RayTracer.Basics;

namespace RayTracer.Graphics;

/// <summary>
/// This class represents a cubic segment of a general path.
/// </summary>
public class CubicPathSegment : PathSegment
{
    internal CubicPathSegment(TwoDPoint start, TwoDPoint control1, TwoDPoint control2, TwoDPoint end)
        : base(start, control1, control2, end) {}
}
