using RayTracer.Geometry;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to create cylinders.
/// </summary>
public class CylinderInstructionSet : SurfaceInstructionSet<Cylinder>
{
    /// <summary>
    /// This method creates a copy of this instruction set,
    /// </summary>
    /// <returns>The copy of this instruction set.</returns>
    public override object Copy()
    {
        return CopyInto(new CylinderInstructionSet());
    }
}
