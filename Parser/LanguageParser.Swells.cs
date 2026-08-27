using Lex.Clauses;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a body of water.
    /// </summary>
    /// <param name="clause">The clause that starts the water.</param>
    private void HandleStartSwellsClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Swells");

        SwellsResolver resolver = ParseSwellsClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a swells block.
    /// </summary>
    /// <param name="clause">The clause that starts the water.</param>
    private SwellsResolver ParseSwellsClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<SwellsResolver>(
                "swellsEntryClause", HandleSwellsEntryClause),
            "swellsEntryClause", HandleSwellsEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a swells block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleSwellsEntryClause(Clause clause)
    {
        SwellsResolver resolver = (SwellsResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "width":
                    resolver.AcrossResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                case "depth":
                    resolver.AlongResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                case "accuracy":
                    resolver.AccuracyResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                case "wave":
                    resolver.TrainResolvers.Add(ParseSwellTrainClause());
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "swells");
                    break;
            }
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a wave train block.
    /// </summary>
    private SwellTrainResolver ParseSwellTrainClause()
    {
        SwellTrainResolver resolver = new ();

        ParseObjectResolver("swellTrainEntryClause", HandleSwellTrainEntryClause, resolver);

        return resolver;
    }

    /// <summary>
    /// This method is used to handle an item clause of a wave train block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleSwellTrainEntryClause(Clause clause)
    {
        SwellTrainResolver resolver = (SwellTrainResolver) _context.CurrentTarget;

        switch (clause.Text())
        {
            case "amplitude":
                resolver.AmplitudeResolver = new TermResolver<double> { Term = clause.Term() };
                break;
            case "steepness":
                resolver.SteepnessResolver = new TermResolver<double> { Term = clause.Term() };
                break;
            case "wavelength":
                resolver.WavelengthResolver = new TermResolver<double> { Term = clause.Term() };
                break;
            case "direction":
                resolver.DirectionResolver = new TermResolver<Vector> { Term = clause.Term() };
                break;
            case "phase":
                resolver.PhaseResolver = new TermResolver<double> { Term = clause.Term() };
                break;
            default:
                throw new Exception($"Internal error: unknown wave train property found: {clause.Text()}.");
        }
    }
}
