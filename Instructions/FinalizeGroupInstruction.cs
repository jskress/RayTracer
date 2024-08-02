using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to finalize a new group.  This includes pushing the group's material
/// to any child surfaces that want to inherit it.
/// </summary>
public class FinalizeGroupInstruction : ObjectInstruction<Group>
{
    /// <summary>
    /// This method is used to execute the instruction.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        // Push our material to interested children, if we have one.  If not, this will
        // be filled in by our parent, and so on...
        if (Target.Material != null)
            SetMaterial(Target.Surfaces, Target.Material);
        
        // Now, roll up our bounding box.
        SetBoundingBox();
    }

    /// <summary>
    /// This is a helper method that will push the given material down to all descendents
    /// who want it.  It will recurse as necessary.
    /// </summary>
    /// <param name="surfaces">The list of surfaces to apply the material to.</param>
    /// <param name="material">The material to apply.</param>
    private static void SetMaterial(List<Surface> surfaces, Material material)
    {
        foreach (Surface surface in surfaces)
            surface.SetMaterial(material);
    }

    /// <summary>
    /// This is a helper method that will set the bounding box as needed, base on the
    /// group's children.
    /// </summary>
    private void SetBoundingBox()
    {
        BoundingBox boundingBox = null;

        foreach (Surface surface in Target.Surfaces)
        {
            switch (surface)
            {
                case Group group when boundingBox == null:
                    boundingBox = group.BoundingBox;
                    break;
                case Group group:
                    boundingBox.Add(group.BoundingBox);
                    break;
                case Triangle triangle:
                {
                    if (boundingBox == null)
                        boundingBox = new BoundingBox(triangle.Point1, triangle.Point2);
                    else
                    {
                        boundingBox.Add(triangle.Point1);
                        boundingBox.Add(triangle.Point2);
                    }

                    boundingBox.Add(triangle.Point3);
                    break;
                }
            }
        }

        boundingBox?.Adjust();

        Target.BoundingBox = boundingBox;
    }
}
