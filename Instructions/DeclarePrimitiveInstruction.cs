using RayTracer.General;
using RayTracer.Terms;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to give a scene a primitive of its own, binding it into the scope it was
/// written in so that its body is later resolved against that scope rather than a caller's.
/// </summary>
public class DeclarePrimitiveInstruction : Instruction
{
    /// <summary>
    /// This property holds the primitive, as yet belonging to no scope.
    /// </summary>
    public UserPrimitive Primitive { get; init; }

    /// <summary>
    /// This method binds the primitive into the scope it was written in.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        variables.SetValue(Primitive.Name, Primitive.BoundTo(variables));
    }
}
