using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Instructions.Surfaces.Extrusions;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a field value.
/// </summary>
public class FieldResolver : SurfaceResolver<Field>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the outline the field fills.
    /// </summary>
    /// <para>
    /// It is a plain resolver rather than a <see cref="GeneralPathResolver"/> so that the outline may
    /// equally be *named*: an outline written out here is known as the scene is read, but one handed
    /// to a primitive as an argument is not known until the primitive is called, and a term looked up
    /// then serves both.
    /// </para>
    public Resolver<GeneralPath> OutlineResolver { get; set; }

    /// <summary>
    /// This property holds the resolvers for the things the field is filled with, each with how often it
    /// is picked against the others: unsaid, as often as any.
    /// </summary>
    public List<(ISurfaceResolver Surface, Resolver<double> Weight)> ChoiceResolvers { get; set; } = [];

    /// <summary>
    /// This property holds the resolver for whether the things to stand are taken in turn.
    /// </summary>
    public Resolver<bool> CycleResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far each copy may be turned about the up axis.
    /// </summary>
    public Resolver<double> TurnResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the smallest size a copy may be stood at.
    /// </summary>
    public Resolver<double> SmallestSizeResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the largest size a copy may be stood at.
    /// </summary>
    public Resolver<double> LargestSizeResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far apart the copies stand.
    /// </summary>
    public Resolver<double> SpacingResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far a copy may stray from the grid.
    /// </summary>
    public Resolver<double> JitterResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for whether each place gets its own surface.
    /// </summary>
    public Resolver<bool> CopiesResolver { get; set; }

    /// <summary>
    /// This property holds the name the scene gave to a copy's number, written as `index in
    /// &lt;name&gt;`, or <c>null</c> if it named none.  It is what makes copies worth asking for: the
    /// number is set for each of them in turn, so anything the copy is built from may differ by it.
    /// </summary>
    public string CopyNumberName { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a field.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Field value)
    {
        OutlineResolver.AssignTo(value, target => target.Outline, context, variables);
        SpacingResolver.AssignTo(value, target => target.Spacing, context, variables);
        JitterResolver.AssignTo(value, target => target.Jitter, context, variables);
        CopiesResolver.AssignTo(value, target => target.Copies, context, variables);
        CycleResolver.AssignTo(value, target => target.Cycle, context, variables);
        TurnResolver.AssignTo(value, target => target.Turn, context, variables);
        SmallestSizeResolver.AssignTo(value, target => target.SmallestSize, context, variables);
        LargestSizeResolver.AssignTo(value, target => target.LargestSize, context, variables);

        // Handed over as factories rather than as surfaces, so that a field told to make copies can ask
        // for a fresh one at every place -- the same arrangement an L-system's leaf uses.  Only a
        // resolver with things of its own replaces the field's: the block on a call of a primitive
        // giving back a field is read as another field, holding a transform and nothing to fill with,
        // and it must not wipe what the primitive just worked out.
        if (ChoiceResolvers.Count > 0)
        {
            value.Choices.Clear();

            foreach ((ISurfaceResolver surface, Resolver<double> weight) in ChoiceResolvers)
            {
                value.Choices.Add(new FieldChoice(
                    number => Make(surface, context, variables, number),
                    weight?.Resolve(context, variables) ?? 1));
            }
        }

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method creates a copy of this resolver.
    /// </summary>
    /// <returns>A clone of this resolver.</returns>
    public override object Clone()
    {
        FieldResolver resolver = (FieldResolver) base.Clone();

        // Force the list to be physically different, but with the same content.
        resolver.ChoiceResolvers = [..resolver.ChoiceResolvers];

        return resolver;
    }

    /// <summary>
    /// This method builds one of the field's copies.
    /// <para>
    /// Where the scene named a copy's number, the number is set in a scope of its own before the copy
    /// is built -- a scope, and not the field's own names, so that a name the field was written among
    /// is not quietly overwritten and does not outlive the field.  This is what a loop's counter does,
    /// and it is the whole of what makes `index` worth more than an instance.
    /// </para>
    /// </summary>
    /// <param name="surface">The resolver for the thing to build.</param>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The names the field was written among.</param>
    /// <param name="number">Which copy this is.</param>
    /// <returns>The surface to stand at this place.</returns>
    private Surface Make(ISurfaceResolver surface, RenderContext context, Variables variables, int number)
    {
        if (CopyNumberName is null)
            return surface.ResolveToSurface(context, variables);

        Variables scope = new Variables(variables);

        scope.SetValue(CopyNumberName, (double) number);

        return surface.ResolveToSurface(context, scope);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        if (OutlineResolver is null)
            return "The \"within\" property is required.";

        return ChoiceResolvers.Count == 0 ? "The \"of\" property is required." : null;
    }
}
