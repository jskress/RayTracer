using System.Linq.Expressions;
using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This class represents the work of adding a new value to a list property on an object.
/// This class represents the work of adding a new value to a list property on an object
/// where the type of the list is a parent type of the child being added.
/// </summary>
public class AddChildInstruction<TContainingObject, TParentObject, TChildObject>
    : AffectObjectPropertyInstruction<TContainingObject, List<TParentObject>>
    where TContainingObject : class
    where TParentObject : class
    where TChildObject : class, TParentObject
{
    private readonly InstructionSet<TChildObject> _objectInstructionSet;

    public AddChildInstruction(
        InstructionSet<TChildObject> objectInstructionSet,
        Expression<Func<TContainingObject, List<TParentObject>>> propertyLambda)
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
        List<TParentObject> list = (List<TParentObject>) Getter
            .Invoke(Target, null);

        list?.Add(value);
    }
}

/// <summary>
/// This class represents the work of adding a new value to a list property on an object
/// where the type of the list is the same as the child being added.
/// </summary>
public class AddChildInstruction<TContainingObject, TChildObject>
    : AddChildInstruction<TContainingObject, TChildObject, TChildObject>
    where TContainingObject : class
    where TChildObject : class
{
    public AddChildInstruction(
        InstructionSet<TChildObject> objectInstructionSet,
        Expression<Func<TContainingObject, List<TChildObject>>> propertyLambda)
        : base(objectInstructionSet, propertyLambda) {}
}
