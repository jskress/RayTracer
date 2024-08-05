using RayTracer.Geometry;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to create spheres.
/// </summary>
public class SphereInstructionSet : SurfaceInstructionSet<Sphere>
{
    /// <summary>
    /// This method creates a copy of this instruction set,
    /// </summary>
    /// <returns>The copy of this instruction set.</returns>
    public override object Copy()
    {
        return CopyInto(new SphereInstructionSet());
    }
}
