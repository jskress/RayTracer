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
    /// This method is used to handle the beginning of a superellipsoid block.
    /// </summary>
    private void HandleStartSuperellipsoidClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Superellipsoid");

        SuperellipsoidResolver resolver = ParseSuperellipsoidClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a superellipsoid block.
    /// </summary>
    private SuperellipsoidResolver ParseSuperellipsoidClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<SuperellipsoidResolver>(
                "superellipsoidEntryClause", HandleSuperellipsoidEntryClause),
            "superellipsoidEntryClause", HandleSuperellipsoidEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a superellipsoid block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleSuperellipsoidEntryClause(Clause clause)
    {
        SuperellipsoidResolver resolver = (SuperellipsoidResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "east":
                    resolver.EastResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                case "north":
                    resolver.NorthResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "superellipsoid");
                    break;
            }
        });
    }
}
