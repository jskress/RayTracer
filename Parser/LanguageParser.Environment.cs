using Lex.Clauses;
using RayTracer.Extensions;
using RayTracer.Instructions;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the environment property, which says what is true of the space
    /// between a scene's objects.
    /// </summary>
    private void HandleEnvironmentClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Environment");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseEnvironmentClause(clause)
        });
    }

    /// <summary>
    /// This method is used to create the resolver for an environment clause.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    /// <returns>The resolver for the clause.</returns>
    private static SceneEnvironmentResolver ParseEnvironmentClause(Clause clause)
    {
        return new SceneEnvironmentResolver
        {
            IndexOfRefractionResolver = new TermResolver<double>
            {
                Term = clause.Term(),
                Validator = index => index > 0
                    ? null
                    : "An index of refraction must be greater than zero."
            }
        };
    }
}
