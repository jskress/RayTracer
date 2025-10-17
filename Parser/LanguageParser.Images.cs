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
    /// This method is used to parse an image reference.
    /// </summary>
    /// <param name="clause">The clause to parse.</param>
    /// <returns>An appropriate image reference resolver.</returns>
    private ImageReferenceResolver ParseImageReference(Clause clause)
    {
        bool alwaysLoad = clause.Text() == "uncached";

        return new ImageReferenceResolver
        {
            ImageNameResolver = new TermResolver<string> { Term = clause.Term() },
            SourceDirectoryResolver = new LiteralResolver<string> { Value = CurrentDirectory },
            AlwaysLoadResolver = new LiteralResolver<bool> { Value = alwaysLoad }
        };
    }
}
