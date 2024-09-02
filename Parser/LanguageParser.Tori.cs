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
    /// This method is used to handle the beginning of a torus block.
    /// </summary>
    private void HandleStartTorusClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Torus");

        TorusResolver resolver = ParseTorusClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a torus block.
    /// </summary>
    private TorusResolver ParseTorusClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<TorusResolver>(
                "torusEntryClause", HandleTorusEntryClause),
            "torusEntryClause", HandleTorusEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a torus block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleTorusEntryClause(Clause clause)
    {
        TorusResolver resolver = (TorusResolver) _context.CurrentTarget;

        if (clause == null) // We must have hit a transform property...
            resolver.TransformResolver = ParseTransformClause();
        else
        {
            switch (clause.Text())
            {
                case "radii":
                    resolver.MajorRadiusResolver = new TermResolver<double> { Term = clause.Term() };
                    resolver.MinorRadiusResolver = new TermResolver<double> { Term = clause.Term(1) };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "torus");
                    break;
            }
        }
    }
}
