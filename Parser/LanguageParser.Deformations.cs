using Lex.Clauses;
using Lex.Parser;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Core;
using RayTracer.Instructions.Surfaces;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method parses a `deformed { }` block: the spline a surface is bent along, and the line in its own space
    /// laid along it.  Both must be said.
    /// </summary>
    /// <param name="clause">The clause that opened the block.</param>
    /// <returns>The resolver for the deformation.</returns>
    private DeformationResolver ParseDeformedClause(Clause clause)
    {
        DeformationResolver resolver = ParseObjectResolver<DeformationResolver>(
            "deformedEntryClause", HandleDeformedEntryClause);

        if (resolver.SplineTerm is null)
        {
            throw new TokenException("A deformation must say the spline to bend along, with \"along\".")
            {
                Token = clause.Tokens[0]
            };
        }

        if (resolver.TowardResolver is null)
        {
            throw new TokenException(
                "A deformation must say the line to bend, with \"centerline toward\" the way it runs.")
            {
                Token = clause.Tokens[0]
            };
        }

        return resolver;
    }

    /// <summary>
    /// This method is used to handle an item clause of a deformation block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleDeformedEntryClause(Clause clause)
    {
        DeformationResolver resolver = (DeformationResolver) _context.CurrentTarget;

        switch (clause.Text())
        {
            case "along":
                resolver.SplineTerm = clause.Term();
                break;
            case "centerline":
            {
                bool from = clause.Tokens.Any(token => token.Text == "from");
                string axis = clause.Tokens[^1].Text;

                resolver.FromResolver = from ? new TermResolver<Point> { Term = clause.Term() } : null;
                resolver.TowardResolver = axis switch
                {
                    "X" => new LiteralResolver<Vector> { Value = new Vector(1, 0, 0) },
                    "Y" => new LiteralResolver<Vector> { Value = new Vector(0, 1, 0) },
                    "Z" => new LiteralResolver<Vector> { Value = new Vector(0, 0, 1) },
                    _ => new TermResolver<Vector>
                    {
                        Term = clause.Term(from ? 1 : 0),
                        Validator = value => value.Magnitude > 0
                            ? null
                            : "A centerline must run some way, not nothing."
                    }
                };
                break;
            }
            case "twist":
                resolver.TwistResolver = new AngleResolver { Term = clause.Term() };
                break;
            case "stretched":
                resolver.Stretched = true;
                break;
            default:
                throw new Exception($"Internal error: unknown deformation property found: {clause.Text()}.");
        }
    }
}
