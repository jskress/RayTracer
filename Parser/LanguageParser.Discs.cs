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
    /// This method is used to handle the beginning of a disc block.
    /// </summary>
    private void HandleStartDiscClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Disc");

        DiscResolver resolver = ParseDiscClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a disc block.
    /// </summary>
    private DiscResolver ParseDiscClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<DiscResolver>(
                "discEntryClause", HandleDiscEntryClause),
            "discEntryClause", HandleDiscEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a disc block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleDiscEntryClause(Clause clause)
    {
        DiscResolver resolver = (DiscResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "center":
                    resolver.CenterResolver = new TermResolver<Point> { Term = clause.Term() };
                    break;
                case "normal":
                    resolver.NormalResolver = new TermResolver<Vector> { Term = clause.Term() };
                    break;
                case "radius":
                    resolver.RadiusResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                case "inner":
                    resolver.InnerRadiusResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "disc");
                    break;
            }
        });
    }
}
