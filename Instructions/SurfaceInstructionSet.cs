using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions;

/// <summary>
/// This class is the base class for all instruction sets that create surfaces.
/// </summary>
public abstract class SurfaceInstructionSet<TObject> : CopyableObjectInstructionSet<TObject>
    where TObject : Surface, new()
{
    /// <summary>
    /// This method may be used by subclasses to perform any initialization on our created
    /// object that is needed.
    /// </summary>
    /// <param name="context">The current render context.</param>
    protected override void InitObject(RenderContext context)
    {
        CreatedObject.NoShadow = context.SuppressAllShadows;
    }

    /// <summary>
    /// This is a helper method for running an instruction set for the sake of creating a
    /// surface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="instructionSet">The instruction set that will create the surface.</param>
    /// <returns>The created surface.</returns>
    protected static Surface CreateSurface(
        RenderContext context, Variables variables, IInstructionSet instructionSet)
    {
        instructionSet.Execute(context, variables);

        return instructionSet switch
        {
            PlaneInstructionSet planeInstructionSet => planeInstructionSet.CreatedObject,
            SphereInstructionSet sphereInstructionSet => sphereInstructionSet.CreatedObject,
            CubeInstructionSet cubeInstructionSet => cubeInstructionSet.CreatedObject,
            CylinderInstructionSet cylinderInstructionSet => cylinderInstructionSet.CreatedObject,
            ConicInstructionSet conicInstructionSet => conicInstructionSet.CreatedObject,
            TriangleInstructionSet triangleInstructionSet => triangleInstructionSet.CreatedObject,
            SmoothTriangleInstructionSet smoothTriangleInstructionSet => smoothTriangleInstructionSet.CreatedObject,
            CsgSurfaceInstructionSet csgSurfaceInstructionSet => csgSurfaceInstructionSet.CreatedObject,
            ObjectFileInstructionSet objectFileInstructionSet => objectFileInstructionSet.CreatedObject,
            GroupInstructionSet groupInstructionSet => groupInstructionSet.CreatedObject,
            _ => throw new Exception($"Internal error: unknown surface type: {instructionSet.GetType().Name}.")
        };
    }
}
