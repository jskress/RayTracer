using Lex.Clauses;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Core;
using RayTracer.Instructions.Patterns;
using RayTracer.Patterns;
using RayTracer.Pigments;

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
    private (IPatternResolver, int) ParsePatternClause(Clause clause, int tokenOffset = 0)
    {
        // A picture read as a pattern is told by its first word, and is the one pattern named by an expression.
        if (clause.Tokens[tokenOffset].Text is "image" or "uncached")
            return (ParseImagePatternClause(clause, tokenOffset), 0);

        Resolver<int?> seedResolver = null;
        string text = string.Join('.', clause.Tokens[tokenOffset..^1]
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
            "crevices" => (new CrevicesPatternResolver(), new CrevicesPattern().DiscretePigmentsNeeded),
            "edges" => (new EdgesPatternResolver(), new EdgesPattern().DiscretePigmentsNeeded),
            "facing" => (new FacingPatternResolver(), new FacingPattern().DiscretePigmentsNeeded),
            "dents" => (new DentsPatternResolver
            {
                SeedResolver = seedResolver
            }, new DentsPattern().DiscretePigmentsNeeded),
            "granite" => (new GranitePatternResolver
            {
                SeedResolver = seedResolver
            }, new GranitePattern().DiscretePigmentsNeeded),
            "flop" => (new FlopPatternResolver(), new FlopPattern().DiscretePigmentsNeeded),
            "hexagon" => (new HexagonPatternResolver(), new HexagonPattern().DiscretePigmentsNeeded),
            "leopard" => (new LeopardPatternResolver(), new LeopardPattern().DiscretePigmentsNeeded),
            "marble" => (new MarblePatternResolver(), new MarblePattern().DiscretePigmentsNeeded),
            "planar" => (new PlanarPatternResolver(), new PlanarPattern().DiscretePigmentsNeeded),
            "radial" => (new RadialPatternResolver(), new RadialPattern().DiscretePigmentsNeeded),
            "ripples" => (new RipplesPatternResolver(), new RipplesPattern().DiscretePigmentsNeeded),
            "square" => (new SquarePatternResolver(), new SquarePattern().DiscretePigmentsNeeded),
            "triangular" => (new TriangularPatternResolver(), new TriangularPattern().DiscretePigmentsNeeded),
            "waves" => (new WavesPatternResolver(), new WavesPattern().DiscretePigmentsNeeded),
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

        ReadBrickSizesInto(resolver);

        return resolver;
    }

    /// <summary>
    /// This method reads whatever a weathering pattern has to say standing at the current place -- how far a
    /// crevice or an edge is looked for, which way a surface is asked to face -- into the resolver given, and
    /// stops at the first thing that is not one.  Like a brick's sizes, these may stand before the colors or
    /// after them, and are read by hand in both places.
    /// </summary>
    /// <param name="resolver">The pattern resolver to read into; any other is left alone.</param>
    private void ReadWeatheringEntriesInto(IPatternResolver resolver)
    {
        if (resolver is not (CrevicesPatternResolver or EdgesPatternResolver or FacingPatternResolver))
            return;

        Clause clause = LanguageDsl.ParseClause(CurrentParser, "weatheringEntryClause");

        while (clause != null)
        {
            Token said = clause.Tokens[0];

            switch (resolver, clause.Text())
            {
                case (CrevicesPatternResolver crevices, "radius"):
                    crevices.RadiusResolver = Reach(clause.Term());
                    break;
                case (EdgesPatternResolver edges, "radius"):
                    edges.RadiusResolver = Reach(clause.Term());
                    break;
                case (FacingPatternResolver facing, "toward"):
                    facing.TowardResolver = clause.Tokens.Count > 1
                        ? new LiteralResolver<Vector>
                        {
                            Value = clause.Text(1) switch
                            {
                                "X" => new Vector(1, 0, 0),
                                "Y" => new Vector(0, 1, 0),
                                _ => new Vector(0, 0, 1)
                            }
                        }
                        : new TermResolver<Vector>
                        {
                            Term = clause.Term(),
                            Validator = value => value.Magnitude > 0
                                ? null
                                : "The way a surface is asked to face must be some way, not nothing."
                        };
                    break;
                default:
                    throw new TokenException(clause.Text() == "radius"
                        ? "Only crevices and edges look about a place, so only they have a radius."
                        : "Only facing asks which way a surface faces, so only it goes toward a direction.")
                    {
                        Token = said
                    };
            }

            clause = LanguageDsl.ParseClause(CurrentParser, "weatheringEntryClause");
        }

        static Resolver<double> Reach(Terms.Term term) => new TermResolver<double>
        {
            Term = term,
            Validator = value => value > 0 ? null : "How far a weathering pattern looks must be more than nothing."
        };
    }

    /// <summary>
    /// This method reads any brick and mortar sizes standing at the current place into the resolver
    /// given, and stops at the first thing that is not one.
    /// <para>
    /// **It is called on both sides of the colors**, which is why it is a method of its own.  The
    /// sizes are read by hand rather than being alternatives in the pigment's entry clause, so they
    /// are only seen where something asks for them; asking in one place only meant that writing them
    /// after the colors -- which is where anyone would write them -- failed with "Expecting a close
    /// brace here", an error that says nothing about `brick size` existing or where it belongs.
    /// </para>
    /// </summary>
    /// <param name="resolver">The brick resolver to read sizes into.</param>
    private void ReadBrickSizesInto(BrickPatternResolver resolver)
    {
        Clause clause = LanguageDsl.ParseClause(CurrentParser, "brickSizeClause");

        while (clause != null)
        {
            if (clause.Text() is "brick")
                resolver.BrickSizeResolver = new TermResolver<Vector> { Term = clause.Term() };
            else
                resolver.MortarSizeResolver = new TermResolver<double> { Term = clause.Term() };

            clause = LanguageDsl.ParseClause(CurrentParser, "brickSizeClause");
        }
    }

    /// <summary>
    /// This method reads whatever the scene has to say about how a pattern's value should be
    /// shaped once the pattern has produced it.  Any of the properties may appear, in any order,
    /// and any of them may be left out.
    /// <para>
    /// Like turbulence, this is offered to every pattern rather than to a chosen few, because none
    /// of it belongs to any pattern in particular: a pattern says how far through its range a point
    /// lies, and this decides what that number does on its way to the color map.
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

    /// <summary>
    /// This method creates the resolver for a picture read as a pattern, or as a normal map: the picture named by the
    /// clause's first expression, laid by whatever map it names, and read for whatever channel it names.
    /// </summary>
    /// <param name="clause">The clause naming the picture.</param>
    /// <param name="tokenOffset">How many words open the clause before the picture's own.</param>
    /// <returns>The resolver for the pattern.</returns>
    private ImagePatternResolver ParseImagePatternClause(Clause clause, int tokenOffset = 0)
    {
        List<string> texts = clause.Tokens[tokenOffset..].Select(token => token.Text).ToList();
        int channel = texts.IndexOf("channel");

        return new ImagePatternResolver
        {
            ImageReferenceResolver = new ImageReferenceResolver
            {
                ImageNameResolver = new TermResolver<string> { Term = clause.Term() },
                SourceDirectoryResolver = new LiteralResolver<string> { Value = CurrentDirectory },
                AlwaysLoadResolver = new LiteralResolver<bool> { Value = texts[0] == "uncached" }
            },
            MapType = ImageMapTypeIn(texts),
            Once = texts.Contains("once"),
            Channel = channel < 0
                ? ImageChannel.Gray
                : texts[channel + 1] switch
                {
                    "red" => ImageChannel.Red,
                    "green" => ImageChannel.Green,
                    "blue" => ImageChannel.Blue,
                    _ => ImageChannel.Alpha
                }
        };
    }

    /// <summary>
    /// This method returns the map an image clause names, if it names one.
    /// </summary>
    /// <param name="texts">The words of the clause.</param>
    /// <returns>The map named, or <c>null</c>.</returns>
    private static ImageMapType? ImageMapTypeIn(List<string> texts)
    {
        if (texts.Contains("planar"))
            return ImageMapType.Planar;

        if (texts.Contains("spherical"))
            return ImageMapType.Spherical;

        if (texts.Contains("cylindrical"))
            return ImageMapType.Cylindrical;

        return texts.Contains("toroidal") ? ImageMapType.Toroidal : null;
    }
}
