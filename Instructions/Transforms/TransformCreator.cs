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
        get => field;
        set
        {
            field = value;

            AxisChangedTo(value);
        }
    }

    private Type[] _expectedTypes;

    /// <summary>
    /// This method is triggered when the <see cref="Axis"/> property is set.
    /// </summary>
    /// <param name="axis">The new transform axis.</param>
    private void AxisChangedTo(TransformAxis axis)
    {
        _expectedTypes = axis == TransformAxis.All
            ? [typeof(NumberTuple), typeof(double)]
            : [typeof(double)];
    }

    /// <summary>
    /// This property holds the value each of this transform's numbers takes when the transform
    /// does nothing at all.  It is zero for most of them -- moving by nothing, turning by no angle,
    /// leaning by none -- and a scale overrides it, since scaling by nothing means scaling by one.
    /// It is what a transform given only part way is measured from.
    /// </summary>
    protected virtual double IdentityValue => 0;

    /// <summary>
    /// This property notes whether this transform can be given part way.  All of them can but a
    /// bare matrix, whose numbers mean nothing on their own.
    /// </summary>
    protected virtual bool CanBeGivenInPart => true;

    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override Matrix Resolve(RenderContext context, Variables variables)
    {
        return ResolveAt(context, variables, 1);
    }

    /// <summary>
    /// This method builds the transform as it stands some fraction of the way through being
    /// applied, which is what a motion asks for: a thing that slides six units over the time the
    /// shutter is open has slid three of them half way through.
    /// <para>
    /// Each sort of transform is measured from its own idea of doing nothing, so a half-applied
    /// move is half as far, a half-applied turn is half the angle, and a half-applied scale is half
    /// way from one to whatever was asked for -- not half of it, which would flatten the thing to
    /// nothing at the start.
    /// </para>
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="fraction">How much of the transform to apply, from zero to one.</param>
    public Matrix ResolveAt(RenderContext context, Variables variables, double fraction)
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

        // A transform applied in full is left entirely alone -- no arithmetic laid over its
        // numbers -- so that every scene written before motion existed builds exactly the matrix
        // it always did.
        if (fraction != 1)
        {
            if (!CanBeGivenInPart)
            {
                throw new Exception(
                    "A matrix cannot be given part way, so it cannot be used as a motion.");
            }

            doubles = doubles.Select(PartWay).ToArray();
            tuples = tuples
                .Select(tuple => new NumberTuple(
                    PartWay(tuple.X), PartWay(tuple.Y), PartWay(tuple.Z), tuple.W))
                .ToArray();
        }

        return CreateTransform(context, doubles, tuples);

        double PartWay(double value) => IdentityValue + (value - IdentityValue) * fraction;
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
