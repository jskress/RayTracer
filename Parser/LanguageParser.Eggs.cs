using Lex.Clauses;
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
    /// This method is used to handle the beginning of an egg block.
    /// </summary>
    private void HandleStartEggClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Egg");

        EggResolver resolver = ParseEggClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from an egg block.
    /// </summary>
    private EggResolver ParseEggClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<EggResolver>(
                "eggEntryClause", HandleEggEntryClause),
            "eggEntryClause", HandleEggEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of an egg block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleEggEntryClause(Clause clause)
    {
        EggResolver resolver = (EggResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "radii":
                    resolver.BottomRadiusResolver = new TermResolver<double> { Term = clause.Term() };
                    resolver.TopRadiusResolver = new TermResolver<double> { Term = clause.Term(1) };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "egg");
                    break;
            }
        });
    }
}
