using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Instructions.Surfaces;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to create the run of surfaces a loop written at the top of a file describes.
/// <para>
/// It is the twin of <see cref="TopLevelObjectCreator"/> and differs in the one way a loop always
/// differs from a thing: how many it makes is not known when the file is read, the range being an
/// expression, so it makes them when the instruction is carried out and adds each as it comes.
/// </para>
/// </summary>
public class TopLevelLoopCreator : Instruction
{
    /// <summary>
    /// This property holds the context to add what the loop makes to.
    /// </summary>
    public InstructionContext Context { get; init; }

    /// <summary>
    /// This property holds the loop.
    /// </summary>
    public SurfaceLoop Loop { get; init; }

    /// <summary>
    /// This method is used to execute the instruction.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        Loop.AddSurfacesTo(context, variables, AddOne);

        return;

        void AddOne(Surface surface) => Context.AddTopLevelObject(surface);
    }
}
