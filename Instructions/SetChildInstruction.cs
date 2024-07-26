using System.Linq.Expressions;
using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This class represents the work of setting a property on an object to a value created
/// by an instruction set.
/// </summary>
public class SetChildInstruction<TParentObject, TChildObject>
    : AffectObjectPropertyInstruction<TParentObject, TChildObject>
    where TParentObject : class
    where TChildObject : class
{
    private readonly InstructionSet<TChildObject> _objectInstructionSet;

    public SetChildInstruction(
        InstructionSet<TChildObject> objectInstructionSet,
        Expression<Func<TParentObject, TChildObject>> propertyLambda)
        : base(propertyLambda)
    {
        _objectInstructionSet = objectInstructionSet;
    }

    /// <summary>
    /// This method is used to execute the instruction to set a string property.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        _objectInstructionSet.Execute(context, variables);

        TChildObject value = _objectInstructionSet.CreatedObject;

        Setter.Invoke(Target, [value]);
    }
}
