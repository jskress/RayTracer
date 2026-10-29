using Lex.Parser;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Terms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve the metal a material is: by name, one of <see cref="Metals"/>, or by the color it
/// mirrors square-on and, if said, an edge color for how each color fares at a slant.
/// </summary>
public class MetalResolver : Resolver<Metal>
{
    /// <summary>
    /// This property holds the term for the metal's name or color.
    /// </summary>
    public Term Term { get; init; }

    /// <summary>
    /// This property holds the term for the edge color of a metal given by its color -- how each color fares at a
    /// slant -- or <c>null</c>.
    /// </summary>
    public Term Edge { get; init; }

    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override Metal Resolve(RenderContext context, Variables variables)
    {
        // A color first: a color can be made a string, and would be taken for a name.
        object value = Term.GetValue(variables, typeof(Color), typeof(string));

        if (value is string name)
        {
            if (!Metals.IsKnown(name))
            {
                throw new TokenException(
                    $"There is no metal named '{name}'; the metals are {string.Join(", ", Metals.Names)}.")
                {
                    Token = Term.ErrorToken
                };
            }

            if (Edge is not null)
            {
                throw new TokenException(
                    "A metal named for what it is turns as it was measured to; only one given by its color may " +
                    "say an edge.")
                {
                    Token = Edge.ErrorToken
                };
            }

            return Metal.Named(name);
        }

        Color edge = Edge is null ? null : Mirrored(Edge.GetValue<Color>(variables), Edge);

        return Metal.FromColor(Mirrored((Color) value, Term), edge);
    }

    /// <summary>
    /// This method makes sure a color a metal mirrors is one a surface can: none to all of each color.
    /// </summary>
    private static Color Mirrored(Color color, Term term)
    {
        if (color.Red is < 0 or > 1 || color.Green is < 0 or > 1 || color.Blue is < 0 or > 1)
        {
            throw new TokenException(
                "A metal mirrors between none and all of each color, so each must lie from 0 to 1.")
            {
                Token = term.ErrorToken
            };
        }

        return color;
    }
}
