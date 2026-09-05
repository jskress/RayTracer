using Lex.Clauses;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a signed distance surface block.
    /// </summary>
    private void HandleStartSdfClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Sdf");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseSdfClause(clause)
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a signed distance surface block.
    /// </summary>
    private SignedDistanceSurfaceResolver ParseSdfClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<SignedDistanceSurfaceResolver>(
                "sdfEntryClause", HandleSdfEntryClause),
            "sdfEntryClause", HandleSdfEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a signed distance surface block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleSdfEntryClause(Clause clause)
    {
        SignedDistanceSurfaceResolver resolver =
            (SignedDistanceSurfaceResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "function":
                    resolver.FunctionResolver = new FieldExpressionResolver { Term = clause.Term() };
                    break;
                case "accuracy":
                    resolver.AccuracyResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "sdf");
                    break;
            }
        });
    }

    /// <summary>
    /// This method is used to handle the beginning of a Julia fractal block.
    /// </summary>
    private void HandleStartJuliaClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Julia");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseJuliaClause(clause)
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a Julia fractal block.
    /// </summary>
    private JuliaFractalResolver ParseJuliaClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<JuliaFractalResolver>(
                "juliaEntryClause", HandleJuliaEntryClause),
            "juliaEntryClause", HandleJuliaEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a Julia fractal block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleJuliaEntryClause(Clause clause)
    {
        JuliaFractalResolver resolver = (JuliaFractalResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "c":
                    resolver.CResolver = new TermResolver<NumberTuple> { Term = clause.Term() };
                    break;
                case "iterations":
                    resolver.IterationsResolver = new TermResolver<int> { Term = clause.Term() };
                    break;
                case "accuracy":
                    resolver.AccuracyResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "julia");
                    break;
            }
        });
    }
}
