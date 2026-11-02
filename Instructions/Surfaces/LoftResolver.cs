using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Instructions.Transforms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve one section of a loft: its outline, its transforms, and how far along a spline it
/// stands.
/// </summary>
public class LoftSectionResolver : ObjectResolver<LoftSection>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the section's outline.
    /// </summary>
    public Resolver<GeneralPath> PathResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for where the section stands, from its own plane.
    /// </summary>
    public TransformResolver TransformResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far along a loft's spline the section stands, as a share of its length.
    /// </summary>
    public Resolver<double> AtResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a section.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, LoftSection value)
    {
        value.Outline = PathResolver.Resolve(context, variables);

        if (TransformResolver?.Resolve(context, variables) is { } transform)
            value.Transform = transform;

        if (AtResolver is not null)
            value.At = AtResolver.Resolve(context, variables);
    }

    /// <summary>
    /// This method makes sure a section says what its outline is.
    /// </summary>
    /// <returns>The text of an error message, or <c>null</c>, if all is well.</returns>
    public string Validate()
    {
        return PathResolver is null ? "A loft's section needs an outline, a path." : null;
    }
}

/// <summary>
/// This class is used to resolve a loft.
/// </summary>
public class LoftResolver : SurfaceResolver<Loft>, IValidatable
{
    /// <summary>
    /// This property holds the resolvers for the loft's sections, in order.
    /// </summary>
    public List<LoftSectionResolver> SectionResolvers { get; private set; } = [];

    /// <summary>
    /// This property holds the resolver for the spline the sections stand along, if they stand along one.
    /// </summary>
    public Resolver<Spline> SplineResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for whether the skin runs straight from section to section.
    /// </summary>
    public Resolver<bool> StraightResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for whether the loft's ends are left open.
    /// </summary>
    public Resolver<bool> OpenResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a loft.  The sections are added to
    /// what the loft has rather than put in place of it: a block laid over a loft a primitive made holds none.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Loft value)
    {
        value.Sections.AddRange(SectionResolvers.Select(resolver => resolver.Resolve(context, variables)));

        if (SplineResolver is not null)
            value.Spline = SplineResolver.Resolve(context, variables);

        StraightResolver.AssignTo(value, target => target.Straight, context, variables);
        OpenResolver.AssignTo(value, target => target.Open, context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method makes sure a loft has sections enough to stretch a skin between.
    /// </summary>
    /// <returns>The text of an error message, or <c>null</c>, if all is well.</returns>
    public string Validate()
    {
        return SectionResolvers.Count < 2 ? "A loft needs at least two sections to stretch its skin between." : null;
    }

    /// <summary>
    /// This method creates a copy of this resolver, with a list of sections of its own.
    /// </summary>
    /// <returns>A clone of this resolver.</returns>
    public override object Clone()
    {
        LoftResolver resolver = (LoftResolver) base.Clone();

        // Force the list to be physically different, but with the same content.
        resolver.SectionResolvers = [..resolver.SectionResolvers];

        return resolver;
    }
}
