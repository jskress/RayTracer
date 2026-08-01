using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.Instructions.Surfaces.Extrusions;

/// <summary>
/// This class is used to resolve a general path value.
/// </summary>
public class GeneralPathResolver : ObjectResolver<GeneralPath>
{
    /// <summary>
    /// This property holds the list of commands to apply to a general path when creating
    /// it.
    /// </summary>
    public List<PathCommand> PathCommands { get; } = [];

    /// <summary>
    /// This property holds the 2D transforms, if any, to apply to the finished path as a
    /// whole before it is handed to whatever shape carries it.
    /// </summary>
    public TwoDTransformResolver TransformResolver { get; } = new ();

    /// <summary>
    /// This method should be provided by subclasses to apply their resolvers to the
    /// appropriate properties.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, GeneralPath value)
    {
        foreach (PathCommand command in PathCommands)
            command.Apply(context, variables, value);

        // The outline is built first, then moved, turned or resized as a whole -- the same
        // order in which a surface is shaped and then placed.
        if (!TransformResolver.IsEmpty)
            value.Transform(TransformResolver.Resolve(context, variables));
    }
}
