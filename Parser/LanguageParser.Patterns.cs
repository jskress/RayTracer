using Lex.Clauses;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Core;
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
    /// necessary, further clauses are parsed.  We also return the number of discrete
    /// pigments that the pattern needs.
    /// </summary>
    /// <param name="clause">The clause to interpret.</param>
    /// <returns>A tuple carrying the pattern resolver and the number of discrete pigments
    /// needed.</returns>
    private (IPatternResolver, int) ParsePatternClause(Clause clause)
    {
        Resolver<int?> seedResolver = null;
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

        if (text.Contains(".with.seed"))
        {
            seedResolver = new TermResolver<int?> { Term = clause.Term() };
            text = text.Replace(".with.seed", "");
        }

        (IPatternResolver Resolver, int Count) result = text switch
        {
            // Simple ones.
            "agate" => (new AgatePatternResolver(), new AgatePattern().DiscretePigmentsNeeded),
            "boxed" => (new BoxedPatternResolver(), new BoxedPattern().DiscretePigmentsNeeded),
            "bozo" => (new BozoPatternResolver
            {
                SeedResolver = seedResolver
            }, new BozoPattern().DiscretePigmentsNeeded),
            "brick" => (ParseBrickPattern(), new BrickPattern().DiscretePigmentsNeeded),
            "checker" => (new CheckerPatternResolver(), new CheckerPattern().DiscretePigmentsNeeded),
            "crackle" => (new CracklePatternResolver
            {
                SeedResolver = seedResolver
            }, new CracklePattern().DiscretePigmentsNeeded),
            "cubic" => (new CubicPatternResolver(), new CubicPattern().DiscretePigmentsNeeded),
            "dents" => (new DentsPatternResolver
            {
                SeedResolver = seedResolver
            }, new DentsPattern().DiscretePigmentsNeeded),
            "granite" => (new GranitePatternResolver
            {
                SeedResolver = seedResolver
            }, new GranitePattern().DiscretePigmentsNeeded),
            "hexagon" => (new HexagonPatternResolver(), new HexagonPattern().DiscretePigmentsNeeded),
            "leopard" => (new LeopardPatternResolver(), new LeopardPattern().DiscretePigmentsNeeded),
            "marble" => (new MarblePatternResolver(), new MarblePattern().DiscretePigmentsNeeded),
            "planar" => (new PlanarPatternResolver(), new PlanarPattern().DiscretePigmentsNeeded),
            "radial" => (new RadialPatternResolver(), new RadialPattern().DiscretePigmentsNeeded),
            "square" => (new SquarePatternResolver(), new SquarePattern().DiscretePigmentsNeeded),
            "triangular" => (new TriangularPatternResolver(), new TriangularPattern().DiscretePigmentsNeeded),
            "wood" => (new WoodPatternResolver(), new WoodPattern().DiscretePigmentsNeeded),
            "wrinkles" => (new WrinklesPatternResolver
            {
                SeedResolver = seedResolver
            }, new WrinklesPattern().DiscretePigmentsNeeded),
            // The clipped falloffs.  Note these are distinct from the "spherical gradient" and
            // "cylindrical gradient" below: those repeat, being the fractional part of the
            // distance, while these fall off once and stop.
            "spherical" => (new SphericalPatternResolver(), new SphericalPattern().DiscretePigmentsNeeded),
            "cylindrical" => (new CylindricalPatternResolver(), new CylindricalPattern().DiscretePigmentsNeeded),
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

        // Turbulence is offered to every pattern, not to the handful that once knew how to ask for
        // it.  It belongs to no pattern in particular: it stirs the points a pattern is asked
        // about, and a pattern goes on computing exactly what it always did.  Parsing it here
        // rather than in each case above is what makes that true of all of them at once.
        result.Resolver.TurbulenceResolver = ParseOptionalTurbulence();

        ParsePatternShaping(result.Resolver);

        return result;
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

    /// <summary>
    /// This method reads whatever the scene has to say about how a pattern's value should be
    /// shaped once the pattern has produced it.  Any of the properties may appear, in any order,
    /// and any of them may be left out.
    /// <para>
    /// Like turbulence, this is offered to every pattern rather than to a chosen few, because none
    /// of it belongs to any pattern in particular: a pattern says how far through its range a point
    /// lies, and this decides what that number does on its way to the colour map.
    /// </para>
    /// </summary>
    /// <param name="resolver">The pattern resolver to attach the shaping to.</param>
    private void ParsePatternShaping(IPatternResolver resolver)
    {
        while (true)
        {
            Clause clause = LanguageDsl.ParseClause(CurrentParser, "patternShapingClause");

            if (clause is null)
                return;

            switch (clause.Text())
            {
                case "frequency":
                    resolver.FrequencyResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                case "phase":
                    resolver.PhaseResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                default:
                    // Everything else is a wave, named by the token that opens the clause.
                    WaveType wave = clause.Text() switch
                    {
                        "ramp" => WaveType.Ramp,
                        "sine" => WaveType.Sine,
                        "triangle" => WaveType.Triangle,
                        "scallop" => WaveType.Scallop,
                        "cubic" => WaveType.Cubic,
                        "poly" => WaveType.Poly,
                        _ => throw new TokenException($"Unknown wave type, {clause.Text()}, found.")
                        {
                            Token = clause.Tokens[0]
                        }
                    };

                    resolver.WaveResolver = new LiteralResolver<WaveType> { Value = wave };

                    if (wave == WaveType.Poly)
                        resolver.ExponentResolver = new TermResolver<double> { Term = clause.Term() };

                    break;
            }
        }
    }
}
