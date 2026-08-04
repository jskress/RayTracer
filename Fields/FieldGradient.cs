using RayTracer.Basics;

namespace RayTracer.Fields;

/// <summary>
/// This class holds the three slopes of a field function -- how it changes along X, along Y and along
/// Z -- each differentiated symbolically and then compiled, exactly as the function itself was.
/// <para>
/// Taken together they point straight out of the surface the field describes, which is what a renderer
/// needs of it: where a field crosses the value that makes a surface, the direction it climbs fastest
/// is the normal there.  A marcher uses them twice over, since the slope along a ray is also what
/// turns a crossing that has been narrowed down into one pinned to the last few digits.
/// </para>
/// </summary>
public class FieldGradient
{
    private readonly FieldFunction _alongX;
    private readonly FieldFunction _alongY;
    private readonly FieldFunction _alongZ;

    private FieldGradient(FieldFunction alongX, FieldFunction alongY, FieldFunction alongZ)
    {
        _alongX = alongX;
        _alongY = alongY;
        _alongZ = alongZ;
    }

    /// <summary>
    /// This method is used to differentiate and compile the gradient of the given expression.  A
    /// function holding something there is no slope for says so here, against the text that wrote it,
    /// rather than part way through a render.
    /// </summary>
    /// <param name="expression">The expression to take the gradient of.</param>
    /// <returns>The compiled gradient.</returns>
    public static FieldGradient Of(FieldExpression expression)
    {
        return new FieldGradient(
            FieldFunction.Compile(expression.Differentiate(FieldAxis.X)),
            FieldFunction.Compile(expression.Differentiate(FieldAxis.Y)),
            FieldFunction.Compile(expression.Differentiate(FieldAxis.Z)));
    }

    /// <summary>
    /// This method returns the gradient of the field at the given point, as a vector.  It is not made
    /// a unit vector here: a caller wanting a normal will want that, but a caller solving for where a
    /// crossing lies wants the size of the slope as well as its direction.
    /// </summary>
    /// <param name="point">The point to take the gradient at.</param>
    /// <returns>The gradient there.</returns>
    public Vector At(Point point)
    {
        return new Vector(
            _alongX.Evaluate(point.X, point.Y, point.Z),
            _alongY.Evaluate(point.X, point.Y, point.Z),
            _alongZ.Evaluate(point.X, point.Y, point.Z));
    }

    /// <summary>
    /// This method returns the slope of the field along one axis at the given point.
    /// </summary>
    /// <param name="axis">The axis wanted.</param>
    /// <param name="x">The X of the point.</param>
    /// <param name="y">Its Y.</param>
    /// <param name="z">Its Z.</param>
    /// <returns>The slope along that axis.</returns>
    public double Along(FieldAxis axis, double x, double y, double z)
    {
        return axis switch
        {
            FieldAxis.X => _alongX.Evaluate(x, y, z),
            FieldAxis.Y => _alongY.Evaluate(x, y, z),
            FieldAxis.Z => _alongZ.Evaluate(x, y, z),
            _ => throw new ArgumentOutOfRangeException(nameof(axis))
        };
    }

    public override string ToString()
    {
        return $"({_alongX}, {_alongY}, {_alongZ})";
    }
}
