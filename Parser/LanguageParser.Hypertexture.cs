using Lex.Clauses;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Core;
using RayTracer.Instructions.Surfaces;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL: here a hypertexture, a solid's skin broken up by
/// noise, hard or soft.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a hypertexture.
    /// </summary>
    private void HandleStartHypertextureClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Hypertexture");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseHypertextureClause(clause)
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a hypertexture block.
    /// </summary>
    private HypertextureResolver ParseHypertextureClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<HypertextureResolver>(
                "hypertextureEntryClause", HandleHypertextureEntryClause),
            "hypertextureEntryClause", HandleHypertextureEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a hypertexture block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleHypertextureEntryClause(Clause clause)
    {
        HypertextureResolver resolver = (HypertextureResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "solid":
                    resolver.SolidResolver = ParseSurfaceAfterAWord(clause);
                    break;
                case "reach":
                    resolver.ReachResolver = new TermResolver<double>
                    {
                        Term = clause.Term(),
                        Validator = value => value >= 0 ? null : "A hypertexture's reach may not be less than nought."
                    };
                    break;
                case "noise":
                    resolver.NoiseResolver = ParseDensityShapeClause(clause);
                    break;
                case "eroded":
                    resolver.ErodedResolver = new LiteralResolver<bool> { Value = true };
                    break;
                case "drawn":
                    resolver.DrawnResolver = new TermResolver<Vector> { Term = clause.Term() };
                    break;
                case "soft":
                    resolver.SoftResolver = new TermResolver<double>
                    {
                        Term = clause.Term(),
                        Validator = value => value > 0 ? null : "A hypertexture's soft width must be more than nought."
                    };
                    break;
                case "medium":
                    resolver.MediumResolver = GetMediumResolver(clause);
                    break;
                case "resolution":
                    resolver.ResolutionResolver = new TermResolver<int>
                    {
                        Term = clause.Term(),
                        Validator = value => value >= 8
                            ? null
                            : "A hypertexture's resolution must be at least 8 cells along its longest side."
                    };
                    break;
                case "accuracy":
                    resolver.AccuracyResolver = new TermResolver<double>
                    {
                        Term = clause.Term(),
                        Validator = value => value > 0 ? null : "A hypertexture's accuracy must be more than nought."
                    };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "hypertexture");
                    break;
            }
        });
    }
}
