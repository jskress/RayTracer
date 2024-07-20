using System.Linq.Expressions;
using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This class represents the work of adding a new value to a list property on an object.
/// </summary>
public class AddChildInstruction<TParentObject, TChildObject>
    : AffectObjectPropertyInstruction<TParentObject, List<TChildObject>>
    where TParentObject : class, new()
    where TChildObject : class, new()
{
    private readonly InstructionSet<TParentObject> _parentInstructionSet;
    private readonly InstructionSet<TChildObject> _objectInstructionSet;

    public AddChildInstruction(
        InstructionSet<TParentObject> parentInstructionSet,
        InstructionSet<TChildObject> objectInstructionSet,
        Expression<Func<TParentObject, List<TChildObject>>> propertyLambda)
        : base(propertyLambda)
    {
        _parentInstructionSet = parentInstructionSet;
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
        List<TChildObject> list = (List<TChildObject>) Getter
            .Invoke(_parentInstructionSet.CreatedObject, null);

        list?.Add(value);
    }
}
