using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;
using RayTracer.Instructions.Transforms;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a top-level loft block.
    /// </summary>
    /// <param name="clause">The clause which starts the loft.</param>
    private void HandleStartLoftClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Loft");

        LoftResolver resolver = ParseLoftClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a loft block.
    /// </summary>
    private LoftResolver ParseLoftClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<LoftResolver>(
                "loftEntryClause", HandleLoftEntryClause),
            "loftEntryClause", HandleLoftEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a loft block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleLoftEntryClause(Clause clause)
    {
        LoftResolver resolver = (LoftResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "section":
                    resolver.SectionResolvers.Add(
                        ParseObjectResolver<LoftSectionResolver>("loftSectionEntryClause", HandleLoftSectionEntryClause));
                    break;
                case "spline" or "discontinuous":
                {
                    // A spline written out, or the name of one, looked up where the loft is made, as a sweep's is.
                    Token last = clause.Tokens[^1];

                    if (BounderToken.OpenBrace.Matches(last))
                    {
                        resolver.SplineResolver = clause.Text() == "discontinuous"
                            ? ParseDiscontinuousSpline()
                            : ParseSplineClause();
                    }
                    else
                        resolver.SplineResolver = new TermResolver<Spline> { Term = new VariableTerm(last) };

                    break;
                }
                case "straight":
                    resolver.StraightResolver = new LiteralResolver<bool> { Value = true };
                    break;
                case "open":
                    resolver.OpenResolver = new LiteralResolver<bool> { Value = true };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "loft");
                    break;
            }
        });
    }

    /// <summary>
    /// This method is used to handle an item clause of a loft's section block: its outline, how far along the loft's
    /// spline it stands, or -- anything else -- its transforms.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleLoftSectionEntryClause(Clause clause)
    {
        LoftSectionResolver resolver = (LoftSectionResolver) _context.CurrentTarget;

        if (clause == null)
        {
            // A transform clause matches nothing quite happily, so it is how many there are that says whether it read one.
            int gathered = resolver.TransformResolver?.TransformCreators.Count ?? 0;
            TransformResolver transforms = ParseTransformClause(resolver.TransformResolver);

            if (transforms == null || transforms.TransformCreators.Count == gathered)
                throw CreateUnexpectedInputException("Expecting a section property here.");

            resolver.TransformResolver = transforms;

            return;
        }

        switch (clause.Text())
        {
            case "path":
                resolver.PathResolver = GetPathResolver(clause);
                break;
            case "at":
                resolver.AtResolver = new TermResolver<double>
                {
                    Term = clause.Term(),
                    Validator = share => share is >= 0 and <= 1
                        ? null
                        : "A section stands at a share of its loft's spline, from 0 at its start to 1 at its end."
                };
                break;
            default:
                throw new Exception($"Internal error: unknown loft section property found: {clause.Text()}.");
        }
    }
}
