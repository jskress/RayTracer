using Lex.Clauses;
using RayTracer.Instructions;
using RayTracer.Instructions.Pigments;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the background property.
    /// </summary>
    private void HandleBackgroundClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Background");

        IPigmentResolver resolver = ParsePigmentClause();

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }
}
