using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a fracture: a solid broken into pieces.
/// </summary>
public class FractureResolver : SurfaceResolver<Fracture>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the solid that is broken: a group written out in the block, a named
    /// surface or a call of a primitive.
    /// </summary>
    public ISurfaceResolver SolidResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many pieces the solid breaks into.
    /// </summary>
    public Resolver<int> PiecesResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how wide the cracks are.
    /// </summary>
    public Resolver<double> GapResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far the pieces' edges are rounded.
    /// </summary>
    public Resolver<double> RoundedResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for whether the solid is broken only across X and Z.
    /// </summary>
    public Resolver<bool> FlatResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how much longer the pieces come out along each axis.
    /// </summary>
    public Resolver<Vector> StretchedResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the point the pieces grow smaller toward.
    /// </summary>
    public Resolver<Point> TowardResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far round that point the pieces are gone.
    /// </summary>
    public Resolver<double> HoleResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far the pieces are moved off.
    /// </summary>
    public Resolver<double> ApartResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the most each piece is turned.
    /// </summary>
    public Resolver<double> TumbleResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for whether the pieces are loose stones, each its own in its pattern.
    /// </summary>
    public Resolver<bool> LooseResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for whether the pieces are whole, kept rather than cut by the solid.
    /// </summary>
    public Resolver<bool> WholeResolver { get; set; }

    /// <summary>
    /// This property holds the resolvers for the materials the pieces take one of at random.
    /// </summary>
    public List<Resolver<Material>> MaterialResolvers { get; set; } = [];

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a fracture.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Fracture value)
    {
        PiecesResolver.AssignTo(value, target => target.Pieces, context, variables);
        GapResolver.AssignTo(value, target => target.Gap, context, variables);
        RoundedResolver.AssignTo(value, target => target.Rounded, context, variables);
        FlatResolver.AssignTo(value, target => target.Flat, context, variables);
        StretchedResolver.AssignTo(value, target => target.Stretched, context, variables);
        TowardResolver.AssignTo(value, target => target.Toward, context, variables);
        HoleResolver.AssignTo(value, target => target.Hole, context, variables);
        ApartResolver.AssignTo(value, target => target.Apart, context, variables);
        TumbleResolver.AssignTo(value, target => target.Tumble, context, variables);
        LooseResolver.AssignTo(value, target => target.Loose, context, variables);
        WholeResolver.AssignTo(value, target => target.Whole, context, variables);

        // Only a resolver that says something replaces what is there: the block on a call of a primitive giving
        // back a fracture is read as another, holding a transform and no solid, and must not wipe what the
        // primitive just worked out.
        // A piece's skin is the solid's own surface, which every piece stands as an instance, and a material handed
        // into a shape shared so goes to whichever piece hands it first: every skin of a broken boulder came out the
        // first of its materials.  So with more than one, each has a solid of its own, nothing in it shared.
        if (SolidResolver is not null)
        {
            if (MaterialResolvers.Count > 1)
            {
                value.Solids = MaterialResolvers
                    .Select(_ => context.Unshared(() => SolidResolver.ResolveToSurface(context, variables)))
                    .ToList();
                value.Solid = value.Solids[0];
            }
            else
            {
                value.Solid = SolidResolver.ResolveToSurface(context, variables);
                value.Solids = null;
            }
        }

        if (MaterialResolvers.Count > 0)
            value.PieceMaterials = MaterialResolvers.Select(material => material.Resolve(context, variables)).ToList();

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method creates a copy of this resolver, its list of materials its own.
    /// </summary>
    /// <returns>The copy.</returns>
    public override object Clone()
    {
        FractureResolver resolver = (FractureResolver) base.Clone();

        resolver.MaterialResolvers = [..resolver.MaterialResolvers];

        return resolver;
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        return SolidResolver is null ? "A fracture needs a solid: the thing that is broken." : null;
    }
}
