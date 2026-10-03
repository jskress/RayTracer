using RayTracer.General;
using RayTracer.Terms;

namespace RayTracer.Instructions.Surfaces.Extrusions;

/// <summary>
/// This class resolves the font families a text names, in the order it names them: the first that
/// can be had is used, as a CSS font list is read.  See
/// <see cref="Fonts.FontManager.GetFirstAvailableTypeFace(IReadOnlyList{string}, Fonts.FontWeight, bool)"/>.
/// </summary>
public class FontFamiliesResolver : Resolver<string[]>
{
    /// <summary>
    /// This property holds the terms naming the families, in order of preference.  It is set once,
    /// when the clause is read, and never added to, so a resolver copied along with a named text
    /// shares it safely.
    /// </summary>
    public IReadOnlyList<Term> Terms { get; init; } = [];

    /// <summary>
    /// This method resolves each term to the name of a family.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The names of the families, in order of preference.</returns>
    public override string[] Resolve(RenderContext context, Variables variables)
    {
        return Terms
            .Select(term => new TermResolver<string> { Term = term }.Resolve(context, variables))
            .ToArray();
    }
}
