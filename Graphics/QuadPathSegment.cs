using RayTracer.Basics;

namespace RayTracer.Graphics;

/// <summary>
/// This class represents a quadratic segment of a general path.
/// </summary>
public class QuadPathSegment : PathSegment
{
    public QuadPathSegment(TwoDPoint start, TwoDPoint control, TwoDPoint end)
        : base(start, control, end) {}
}
