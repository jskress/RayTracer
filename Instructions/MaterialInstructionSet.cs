using RayTracer.Core;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to create materials.
/// </summary>
public class MaterialInstructionSet : CopyableObjectInstructionSet<Material>
{
    /// <summary>
    /// This method creates a copy of this instruction set,
    /// </summary>
    /// <returns>The copy of this instruction set.</returns>
    public override object Copy()
    {
        return CopyInto(new MaterialInstructionSet());
    }
}
