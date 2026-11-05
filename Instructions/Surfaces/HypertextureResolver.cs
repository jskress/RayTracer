using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Pigments;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a hypertexture: the solid whose skin is broken up, how far and by what, and for a
/// soft one the medium its stuff is made of.
/// </summary>
public class HypertextureResolver : SurfaceResolver<Hypertexture>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the solid: a group written out in the block, a named surface or a call of
    /// a primitive.
    /// </summary>
    public ISurfaceResolver SolidResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far the noise moves the skin.
    /// </summary>
    public Resolver<double> ReachResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the noise the skin is moved by.
    /// </summary>
    public DensityShapeResolver NoiseResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for whether the skin only moves in.
    /// </summary>
    public Resolver<bool> ErodedResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the direction, and how far, the skin is drawn out.
    /// </summary>
    public Resolver<Vector> DrawnResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far either side of the skin a soft hypertexture's stuff thins out.
    /// </summary>
    public Resolver<double> SoftResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the medium a soft hypertexture's stuff is made of.
    /// </summary>
    public Resolver<Medium> MediumResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many cells of the distance field span the longest side.
    /// </summary>
    public Resolver<int> ResolutionResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how closely the skin is found.
    /// </summary>
    public Resolver<double> AccuracyResolver { get; set; }

    /// <summary>
    /// This method applies our resolvers to a hypertexture.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Hypertexture value)
    {
        base.SetProperties(context, variables, value);

        if (SolidResolver is not null)
            value.Solid = SolidResolver.ResolveToSurface(context, variables);

        if (ReachResolver is not null)
            value.Reach = ReachResolver.Resolve(context, variables);

        if (NoiseResolver is not null)
            value.Noise = NoiseResolver.Resolve(context, variables);

        ErodedResolver.AssignTo(value, target => target.Eroded, context, variables);
        DrawnResolver.AssignTo(value, target => target.Drawn, context, variables);
        ResolutionResolver.AssignTo(value, target => target.Resolution, context, variables);

        if (AccuracyResolver is not null)
        {
            value.Accuracy = AccuracyResolver.Resolve(context, variables);
            value.AccuracyIsSaid = true;
        }

        if (SoftResolver is not null)
        {
            value.Soft = SoftResolver.Resolve(context, variables);
            bool was = Instructions.MediumResolver.ShapedByItsHolder;

            Instructions.MediumResolver.ShapedByItsHolder = true;

            try
            {
                value.Material = ShellAround(MediumResolver?.Resolve(context, variables), value);
            }
            finally
            {
                Instructions.MediumResolver.ShapedByItsHolder = was;
            }
        }
    }

    /// <summary>
    /// This method makes the clear shell a soft hypertexture is: seen through entirely, refracting nothing, and holding
    /// its medium, given the hypertexture's own stuff for its shape.
    /// </summary>
    private static Material ShellAround(Medium medium, Hypertexture hypertexture)
    {
        if (medium is null)
            throw new Exception("A soft hypertexture needs a medium: what its stuff is made of.");

        if (medium.DensityVaries)
        {
            throw new Exception(
                "A soft hypertexture's medium takes its shape from the hypertexture, and cannot have a density function or pattern of its own.");
        }

        Medium stuff = medium.Copy();

        stuff.DensityPattern = hypertexture.Density;

        return new Material
        {
            Pigment = SolidPigment.White,
            Ambient = 0,
            Diffuse = 0,
            Specular = 0,
            Transparency = 1,
            Interior = new Interior { IndexOfRefraction = 1, Medium = stuff }
        };
    }

    /// <summary>
    /// This method checks what can be checked before the scene is carried out.
    /// </summary>
    /// <returns>What is wrong, or <c>null</c>.</returns>
    public string Validate()
    {
        if (SolidResolver is null)
            return "A hypertexture needs a solid: the thing whose skin it breaks up.";

        if (SoftResolver is not null && MediumResolver is null)
            return "A soft hypertexture needs a medium: what its stuff is made of.";

        if (SoftResolver is null && MediumResolver is not null)
            return "Only a soft hypertexture holds a medium; say how soft it is, or give it a material instead.";

        if (SoftResolver is not null && MaterialResolver is not null)
            return "A soft hypertexture is a clear shell round its medium, and takes no material.";

        return null;
    }
}
