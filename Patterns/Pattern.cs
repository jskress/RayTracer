using RayTracer.Basics;

namespace RayTracer.Patterns;

/// <summary>
/// This is the base class for all patterns.
/// </summary>
public abstract class Pattern
{
    public abstract double Evaluate(Point point);
}
