using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Graphics;
using RayTracer.Instructions;
using RayTracer.Instructions.Core;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a light block, of whatever sort.
    /// </summary>
    /// <param name="clause">The clause that opened the block.</param>
    private void HandleStartLightClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Light");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseLightClause(clause)
        });
    }

    /// <summary>
    /// This method reads a light block and returns the resolver for whichever sort it turned out
    /// to be.  Which sort is decided by the word before "light": "distant" for the sun, "spot" for
    /// a cone, and nothing or "point" for a plain lamp.
    /// </summary>
    /// <param name="clause">The clause that opened the block.</param>
    /// <returns>The resolver for the light.</returns>
    private IObjectResolver ParseLightClause(Clause clause)
    {
        // The word before "light" names the sort, so everything after it sits one token later when
        // there is one.
        int offset = clause.Tokens[0].Text == "light" ? 0 : 1;

        // A name where a block would be means a light already described.  Which sort it is comes from
        // what was stored rather than from the words written here, exactly as "object <name>" works
        // for a surface of any kind.
        if (!BounderToken.OpenBrace.Matches(clause.Tokens[offset + 1]))
        {
            return GetExtensibleItem<ILightResolver>(clause.Tokens[offset + 1], false) switch
            {
                DistantLightResolver => NamedLight<DistantLightResolver>(
                    clause, "distantLightEntryClause", HandleDistantLightEntryClause, offset),
                SpotlightResolver => NamedLight<SpotlightResolver>(
                    clause, "spotLightEntryClause", HandleSpotlightEntryClause, offset),
                AreaLightResolver => NamedLight<AreaLightResolver>(
                    clause, "areaLightEntryClause", HandleAreaLightEntryClause, offset),
                SkyLightResolver => NamedLight<SkyLightResolver>(
                    clause, "skyLightEntryClause", HandleSkyLightEntryClause, offset),
                _ => NamedLight<PointLightResolver>(
                    clause, "pointLightEntryClause", HandlePointLightEntryClause, offset)
            };
        }

        return clause.Tokens[0].Text switch
        {
            "distant" => ParseObjectResolver<DistantLightResolver>(
                "distantLightEntryClause", HandleDistantLightEntryClause),
            "spot" => ParseObjectResolver<SpotlightResolver>(
                "spotLightEntryClause", HandleSpotlightEntryClause),
            "area" => ParseObjectResolver<AreaLightResolver>(
                "areaLightEntryClause", HandleAreaLightEntryClause),
            "sky" => ParseObjectResolver<SkyLightResolver>(
                "skyLightEntryClause", HandleSkyLightEntryClause),
            _ => ParseObjectResolver<PointLightResolver>(
                "pointLightEntryClause", HandlePointLightEntryClause)
        };
    }

    /// <summary>
    /// This method finds a light that was given a name and, if the scene added a block of its own,
    /// lays that over a copy of it.
    /// </summary>
    /// <param name="clause">The clause naming the light.</param>
    /// <param name="entryBlockName">The clause name for reading that sort of light's properties.</param>
    /// <param name="handler">What handles one of those properties.</param>
    /// <param name="offset">How many words stand before the name beyond the first.</param>
    /// <returns>The resolver for the light.</returns>
    private TResolver NamedLight<TResolver>(
        Clause clause, string entryBlockName, Action<Clause> handler, int offset)
        where TResolver : class, ICloneable, IObjectResolver, new()
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<TResolver>(entryBlockName, handler),
            entryBlockName, handler, offset);
    }

    /// <summary>
    /// This method is used to handle an item clause of a sky light block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleSkyLightEntryClause(Clause clause)
    {
        SkyLightResolver resolver = (SkyLightResolver) _context.CurrentTarget;

        switch (clause.Text())
        {
            case "named":
                resolver.NameResolver = new TermResolver<string> { Term = clause.Term() };
                break;
            case "color":
                resolver.ColorResolver = new TermResolver<Color> { Term = clause.Term() };
                break;
            case "pigment":
                resolver.PigmentResolver = ParsePigmentClause();
                break;
            case "samples":
                resolver.SamplesResolver = new TermResolver<int>
                {
                    Term = clause.Term(),
                    Validator = samples => samples < 1
                        ? "A sky must be looked at from at least one direction."
                        : null
                };
                break;
            default:
                throw new NotSupportedException("Unknown sky light property found.");
        }
    }

    /// <summary>
    /// This method is used to handle an item clause of a point light block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandlePointLightEntryClause(Clause clause)
    {
        PointLightResolver resolver = (PointLightResolver) _context.CurrentTarget;
        Term term = clause.Term();

        switch (clause.Text())
        {
            case "named":
                resolver.NameResolver = new TermResolver<string> { Term = term };
                break;
            case "location":
                resolver.LocationResolver = new TermResolver<Point> { Term = term };
                break;
            case "color":
                resolver.ColorResolver = new TermResolver<Color> { Term = term };
                break;
            case "fade":
                if (clause.Text(1) == "distance")
                {
                    resolver.FadeDistanceResolver = new TermResolver<double>
                    {
                        Term = term,
                        Validator = distance => distance > 0
                            ? null
                            : "A light must be worth what it says at some real distance, so a " +
                              "fading distance of nothing or less has no meaning."
                    };
                }
                else
                {
                    resolver.FadePowerResolver = new TermResolver<double>
                    {
                        Term = term,
                        Validator = power => power >= 0
                            ? null
                            : "A light cannot grow brighter with distance, so a fading power below " +
                              "nothing has no meaning."
                    };
                }

                break;
            default:
                throw new Exception($"Internal error: unknown light property found: {clause.Text()}.");
        }
    }

    /// <summary>
    /// This method is used to handle an item clause of a distant light block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleDistantLightEntryClause(Clause clause)
    {
        DistantLightResolver resolver = (DistantLightResolver) _context.CurrentTarget;
        Term term = clause.Term();

        switch (clause.Text())
        {
            case "named":
                resolver.NameResolver = new TermResolver<string> { Term = term };
                break;
            case "direction":
                resolver.DirectionResolver = new TermResolver<Vector> { Term = term };
                break;
            case "color":
                resolver.ColorResolver = new TermResolver<Color> { Term = term };
                break;
            default:
                throw new Exception($"Internal error: unknown light property found: {clause.Text()}.");
        }
    }

    /// <summary>
    /// This method is used to handle an item clause of a spotlight block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleSpotlightEntryClause(Clause clause)
    {
        SpotlightResolver resolver = (SpotlightResolver) _context.CurrentTarget;
        Term term = clause.Term();

        switch (clause.Text())
        {
            case "named":
                resolver.NameResolver = new TermResolver<string> { Term = term };
                break;
            case "location":
                resolver.LocationResolver = new TermResolver<Point> { Term = term };
                break;
            // The "point at" clause opens with "point", so that is the tag it comes in under.
            case "point":
                resolver.PointAtResolver = new TermResolver<Point> { Term = term };
                break;
            case "radius":
                resolver.RadiusResolver = new TermResolver<double> { Term = term };
                break;
            case "falloff":
                resolver.FalloffResolver = new TermResolver<double> { Term = term };
                break;
            case "tightness":
                resolver.TightnessResolver = new TermResolver<double> { Term = term };
                break;
            case "color":
                resolver.ColorResolver = new TermResolver<Color> { Term = term };
                break;
            case "fade":
                if (clause.Text(1) == "distance")
                {
                    resolver.FadeDistanceResolver = new TermResolver<double>
                    {
                        Term = term,
                        Validator = distance => distance > 0
                            ? null
                            : "A light must be worth what it says at some real distance, so a " +
                              "fading distance of nothing or less has no meaning."
                    };
                }
                else
                {
                    resolver.FadePowerResolver = new TermResolver<double>
                    {
                        Term = term,
                        Validator = power => power >= 0
                            ? null
                            : "A light cannot grow brighter with distance, so a fading power below " +
                              "nothing has no meaning."
                    };
                }

                break;
            default:
                throw new Exception($"Internal error: unknown light property found: {clause.Text()}.");
        }
    }

    /// <summary>
    /// This method is used to handle an item clause of an area light block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleAreaLightEntryClause(Clause clause)
    {
        AreaLightResolver resolver = (AreaLightResolver) _context.CurrentTarget;
        Term term = clause.Term();

        // "no jitter" comes in as two words joined with a dot, the way "no shadow" does on a
        // surface; everything else here is a single word, which ToCmd leaves alone.
        switch (ToCmd(clause))
        {
            case "named":
                resolver.NameResolver = new TermResolver<string> { Term = term };
                break;
            case "location":
                resolver.LocationResolver = new TermResolver<Point> { Term = term };
                break;
            case "axisU":
                resolver.Axis1Resolver = new TermResolver<Vector> { Term = term };
                break;
            case "axisV":
                resolver.Axis2Resolver = new TermResolver<Vector> { Term = term };
                break;
            // "steps" sets both directions at once, which is what a square grid wants.
            case "steps":
                resolver.UStepsResolver = new TermResolver<int> { Term = term };
                resolver.VStepsResolver = new TermResolver<int> { Term = term };
                break;
            case "uSteps":
                resolver.UStepsResolver = new TermResolver<int> { Term = term };
                break;
            case "vSteps":
                resolver.VStepsResolver = new TermResolver<int> { Term = term };
                break;
            case "seed":
                resolver.SeedResolver = new TermResolver<int?> { Term = term };
                break;
            // "no jitter" arrives as a two-word tag, and turns the jitter off.
            case "no.jitter":
                resolver.Jitter = false;
                break;
            case "color":
                resolver.ColorResolver = new TermResolver<Color> { Term = term };
                break;
            case "fade":
                if (clause.Text(1) == "distance")
                {
                    resolver.FadeDistanceResolver = new TermResolver<double>
                    {
                        Term = term,
                        Validator = distance => distance > 0
                            ? null
                            : "A light must be worth what it says at some real distance, so a " +
                              "fading distance of nothing or less has no meaning."
                    };
                }
                else
                {
                    resolver.FadePowerResolver = new TermResolver<double>
                    {
                        Term = term,
                        Validator = power => power >= 0
                            ? null
                            : "A light cannot grow brighter with distance, so a fading power below " +
                              "nothing has no meaning."
                    };
                }

                break;
            default:
                throw new Exception($"Internal error: unknown light property found: {clause.Text()}.");
        }
    }
}
