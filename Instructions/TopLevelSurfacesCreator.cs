using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Instructions.Surfaces;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to create the run of surfaces that a loop or a choice written at the top of a
/// file describes.
/// <para>
/// It is the twin of <see cref="TopLevelObjectCreator"/> and differs in the one way these always
/// differ from a thing: how many they make is not known when the file is read, the range or the
/// condition being an expression, so they are made when the instruction is carried out and each is
/// added as it comes.
/// </para>
/// </summary>
public class TopLevelSurfacesCreator : Instruction
{
    /// <summary>
    /// This property holds the context to add what the loop makes to.
    /// </summary>
    public InstructionContext Context { get; init; }

    /// <summary>
    /// This property holds the loop or choice that was written.
    /// </summary>
    public SurfaceListEntry Entry { get; init; }

    /// <summary>
    /// This method is used to execute the instruction.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        Entry.AddSurfacesTo(context, variables, AddOne);

        return;

        void AddOne(Surface surface) => Context.AddTopLevelObject(surface);
    }
}
