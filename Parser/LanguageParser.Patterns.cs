using Lex.Clauses;
using Lex.Parser;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Patterns;
using RayTracer.Patterns;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method creates a pattern resolver represented by the given clause.  As
    /// necessary, further clauses are parsed.  The number of discrete pigments that the
    /// pattern needs is also returned.
    /// </summary>
    /// <param name="clause">The clause to interpret.</param>
    /// <returns>A tuple carrying the pattern resolver and the number of discrete pigments
    /// needed.</returns>
    private (IPatternResolver, int) ParsePatternClause(Clause clause)
    {
        string text = string.Join('.', clause.Tokens[..^1]
            .Select(token => token.Text));
        bool bouncing = false;

        if (text.EndsWith(".{"))
            text = text[..^2];

        if (text.Contains(".bouncing."))
        {
            text = text.Replace(".bouncing.", ".");
            bouncing = true;
        }

        return text switch
        {
            // Simple ones.
            "agate" => (new AgatePatternResolver
            {
                TurbulenceResolver = ParseTurbulenceClause()
            }, new AgatePattern().DiscretePigmentsNeeded),
            "boxed" => (new BoxedPatternResolver(), new BoxedPattern().DiscretePigmentsNeeded),
            "brick" => (ParseBrickPattern(), new BrickPattern().DiscretePigmentsNeeded),
            "checker" => (new CheckerPatternResolver(), new CheckerPattern().DiscretePigmentsNeeded),
            "cubic" => (new CubicPatternResolver(), new CubicPattern().DiscretePigmentsNeeded),
            "dents" => (new DentsPatternResolver(), new DentsPattern().DiscretePigmentsNeeded),
            "granite" => (new GranitePatternResolver(), new GranitePattern().DiscretePigmentsNeeded),
            "hexagon" => (new HexagonPatternResolver(), new HexagonPattern().DiscretePigmentsNeeded),
            "leopard" => (new LeopardPatternResolver(), new LeopardPattern().DiscretePigmentsNeeded),
            "planar" => (new PlanarPatternResolver(), new PlanarPattern().DiscretePigmentsNeeded),
            "square" => (new SquarePatternResolver(), new SquarePattern().DiscretePigmentsNeeded),
            "triangular" => (new TriangularPatternResolver(), new TriangularPattern().DiscretePigmentsNeeded),
            "wrinkles" => (new WrinklesPatternResolver(), new WrinklesPattern().DiscretePigmentsNeeded),
            // Stripes
            "linear.stripes" => (new StripedPatternResolver
            {
                BandTypeResolver = new LiteralResolver<BandType> { Value = BandType.LinearX }
            }, new StripedPattern().DiscretePigmentsNeeded),
            "linear.X.stripes" => (new StripedPatternResolver
            {
                BandTypeResolver = new LiteralResolver<BandType> { Value = BandType.LinearX }
            }, new StripedPattern().DiscretePigmentsNeeded),
            "linear.Y.stripes" => (new StripedPatternResolver
            {
                BandTypeResolver = new LiteralResolver<BandType> { Value = BandType.LinearY }
            }, new StripedPattern().DiscretePigmentsNeeded),
            "linear.Z.stripes" => (new StripedPatternResolver
            {
                BandTypeResolver = new LiteralResolver<BandType> { Value = BandType.LinearZ }
            }, new StripedPattern().DiscretePigmentsNeeded),
            "cylindrical.stripes" => (new StripedPatternResolver
            {
                BandTypeResolver = new LiteralResolver<BandType> { Value = BandType.Cylindrical }
            }, new StripedPattern().DiscretePigmentsNeeded),
            "spherical.stripes" => (new StripedPatternResolver
            {
                BandTypeResolver = new LiteralResolver<BandType> { Value = BandType.Spherical }
            }, new StripedPattern().DiscretePigmentsNeeded),
            // Gradients
            "linear.gradient" => (new GradientPatternResolver
            {
                BandTypeResolver = new LiteralResolver<BandType> { Value = BandType.LinearX },
                BouncingResolver = new LiteralResolver<bool> { Value = bouncing }
            }, new GradientPattern().DiscretePigmentsNeeded),
            "linear.X.gradient" => (new GradientPatternResolver
            {
                BandTypeResolver = new LiteralResolver<BandType> { Value = BandType.LinearX },
                BouncingResolver = new LiteralResolver<bool> { Value = bouncing }
            }, new GradientPattern().DiscretePigmentsNeeded),
            "linear.Y.gradient" => (new GradientPatternResolver
            {
                BandTypeResolver = new LiteralResolver<BandType> { Value = BandType.LinearY },
                BouncingResolver = new LiteralResolver<bool> { Value = bouncing }
            }, new GradientPattern().DiscretePigmentsNeeded),
            "linear.Z.gradient" => (new GradientPatternResolver
            {
                BandTypeResolver = new LiteralResolver<BandType> { Value = BandType.LinearZ },
                BouncingResolver = new LiteralResolver<bool> { Value = bouncing }
            }, new GradientPattern().DiscretePigmentsNeeded),
            "cylindrical.gradient" => (new GradientPatternResolver
            {
                BandTypeResolver = new LiteralResolver<BandType> { Value = BandType.Cylindrical },
                BouncingResolver = new LiteralResolver<bool> { Value = bouncing }
            }, new GradientPattern().DiscretePigmentsNeeded),
            "spherical.gradient" => (new GradientPatternResolver
            {
                BandTypeResolver = new LiteralResolver<BandType> { Value = BandType.Spherical },
                BouncingResolver = new LiteralResolver<bool> { Value = bouncing }
            }, new GradientPattern().DiscretePigmentsNeeded),
            _ => throw new TokenException($"Unsupported {text} pattern found.")
            {
                Token = clause.Tokens[0]
            }
        };
    }

    /// <summary>
    /// This method is used to parse out a brick pattern.
    /// </summary>
    /// <returns>The parsed brick pattern.</returns>
    private BrickPatternResolver ParseBrickPattern()
    {
        BrickPatternResolver resolver = new ();
        Clause clause = LanguageDsl.ParseClause(CurrentParser, "brickSizeClause");

        while (clause != null)
        {
            if (clause.Text() is "brick")
                resolver.BrickSizeResolver = new TermResolver<Vector> { Term = clause.Term() };
            else
                resolver.MortarSizeResolver = new TermResolver<double> { Term = clause.Term() };

            clause = LanguageDsl.ParseClause(CurrentParser, "brickSizeClause");
        }

        return resolver;
    }
}
