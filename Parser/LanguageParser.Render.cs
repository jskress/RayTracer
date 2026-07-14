using Lex.Clauses;
using RayTracer.Instructions;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle a "render" clause, which picks the scene and/or
    /// camera to render when more than one is defined.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleRenderClause(Clause clause)
    {
        ClauseReader reader = clause.Reader();

        reader.NextToken(); // The "render" keyword.

        Term sceneName = null;
        Term cameraName = null;

        if (reader.SkipIfNextTextIs("scene"))
            sceneName = (Term) reader.NextExpression();

        if (reader.SkipIfNextTextIs("with"))
        {
            reader.NextToken(); // The "camera" keyword.

            cameraName = (Term) reader.NextExpression();
        }

        _context.InstructionContext.AddInstruction(new RenderInstruction(sceneName, cameraName));
    }
}
