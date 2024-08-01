using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to set the material on a surface to the material from its parent
/// surface.
/// </summary>
public class MarkMaterialForInheritanceInstruction<TObject> : ObjectInstruction<TObject>
    where TObject : Surface, new()
{
    /// <summary>
    /// This method is used to execute the instruction.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        Target.Material = null;
    }
}
