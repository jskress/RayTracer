using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.General;

namespace RayTracer.Instructions.Transforms;

/// <summary>
/// This class is used to resolve a value that is a matrix.
/// </summary>
/// <remarks>
/// Note: This class extends <see cref="ObjectResolver{TValue}"/> for the convenience of
/// playing nice with the variable and extensible items systems; we provide our own
/// <c>Resolve()</c> method.
/// </remarks>
public class TransformResolver : ObjectResolver<Matrix>, ICloneable
{
    /// <summary>
    /// This property holds the list of creators for the collection of transforms that will
    /// be combined for our final value.
    /// </summary>
    public List<TransformCreator> TransformCreators { get; private set; } = [];

    private bool _reversed;

    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override Matrix Resolve(RenderContext context, Variables variables)
    {
        if (!_reversed)
        {
            TransformCreators.Reverse();

            _reversed = true;
        }

        List<Matrix> transforms = TransformCreators
            .Select(creator => creator.Resolve(context, variables))
            .ToList();

        return transforms.IsEmpty()
            ? Matrix.Identity
            : transforms[1..].Aggregate(
                transforms[0],
                (accumulator, next) => accumulator * next);
    }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a named
    /// object.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Matrix value)
    {
        // Since we hijack the Resolve() method, we don't really need to do anything here.
    }

    /// <summary>
    /// This method creates a copy of this resolver.
    /// </summary>
    /// <returns>A clone of this resolver.</returns>
    public object Clone()
    {
        TransformResolver resolver = (TransformResolver) MemberwiseClone();

        // Force the lists to be physically different, but with the same content.
        resolver.TransformCreators = [..resolver.TransformCreators];

        return resolver;
    }
}
