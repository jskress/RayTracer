using Lex.Clauses;
using RayTracer.Instructions;
using RayTracer.Pigments;

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

        PigmentInstructionSet pigmentInstructionSet = ParsePigmentClause();

        _ = new TopLevelObjectInstruction<Pigment>(_context.InstructionContext, pigmentInstructionSet);
    }
}
