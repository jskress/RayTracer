using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Terms;

namespace RayTracer.Instructions.Transforms;

/// <summary>
/// This class provides the base class for the various concrete transform creators.
/// </summary>
public abstract class TransformCreator : Resolver<Matrix>
{
    /// <summary>
    /// This property holds the collection of terms needed to create a transform.
    /// </summary>
    public Term[] Terms { get; set; }

    /// <summary>
    /// This property note which axes the transform should affect.
    /// </summary>
    public TransformAxis Axis
    {
        get => _axis;
        set
        {
            _axis = value;

            AxisChangedTo(value);
        }
    }

    private TransformAxis _axis;
    private Type[] _expectedTypes;

    /// <summary>
    /// This method is triggered when the <see cref="Axis"/> property is set.
    /// </summary>
    /// <param name="axis"></param>
    private void AxisChangedTo(TransformAxis axis)
    {
        _expectedTypes = axis == TransformAxis.All
            ? [typeof(NumberTuple), typeof(double)]
            : [typeof(double)];
    }

    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override Matrix Resolve(RenderContext context, Variables variables)
    {
        object[] values = Terms
            .Select(term => term.GetValue(variables, _expectedTypes))
            .ToArray();
        double[] doubles = values
            .Where(value => value is double)
            .Cast<double>()
            .ToArray();
        NumberTuple[] tuples = values
            .Where(value => value is NumberTuple)
            .Cast<NumberTuple>()
            .ToArray();

        return CreateTransform(context, doubles, tuples);
    }

    /// <summary>
    /// This method is provided by subclasses to actually create the transform matrix.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="doubles">The array of doubles, if any, our terms resolved to.</param>
    /// <param name="tuples">The array of tuples, if any, our terms resolved to.</param>
    /// <returns>The created matrix.</returns>
    protected abstract Matrix CreateTransform(
        RenderContext context, double[] doubles, NumberTuple[] tuples);
}
