using Lex.Clauses;
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
        return clause.Tokens[0].Text switch
        {
            "distant" => ParseObjectResolver<DistantLightResolver>(
                "distantLightEntryClause", HandleDistantLightEntryClause),
            "spot" => ParseObjectResolver<SpotlightResolver>(
                "spotLightEntryClause", HandleSpotlightEntryClause),
            "area" => ParseObjectResolver<AreaLightResolver>(
                "areaLightEntryClause", HandleAreaLightEntryClause),
            _ => ParseObjectResolver<PointLightResolver>(
                "pointLightEntryClause", HandlePointLightEntryClause)
        };
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
            default:
                throw new Exception($"Internal error: unknown light property found: {clause.Text()}.");
        }
    }
}
