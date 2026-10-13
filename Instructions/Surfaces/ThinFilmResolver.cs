using RayTracer.Core;
using RayTracer.General;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a film on a material: how thick it is, perhaps by a pattern, and its
/// index of refraction.
/// </summary>
public class ThinFilmResolver : ObjectResolver<ThinFilm>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for how thick the film is, or how thin at its thinnest.
    /// </summary>
    public Resolver<double> ThicknessResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how thick the film is at its thickest, where a pattern
    /// sets its thickness.
    /// </summary>
    public Resolver<double> ThickestResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the pattern that sets the film's thickness, if one does.
    /// </summary>
    public DensityShapeResolver PatternResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the film's index of refraction.
    /// </summary>
    public Resolver<double> IndexOfRefractionResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a film.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The film to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, ThinFilm value)
    {
        ThicknessResolver.AssignTo(value, target => target.Thickness, context, variables);
        ThickestResolver.AssignTo(value, target => target.Thickest, context, variables);
        PatternResolver.AssignTo(value, target => target.Pattern, context, variables);
        IndexOfRefractionResolver.AssignTo(value, target => target.IndexOfRefraction, context, variables);
    }

    /// <summary>
    /// This method checks that the film says how thick it is, which nothing could guess for it.
    /// </summary>
    /// <returns>An error message, or <c>null</c>, if all is well.</returns>
    public string Validate()
    {
        return ThicknessResolver is null ? "A film needs a thickness, in nanometers." : null;
    }
}
