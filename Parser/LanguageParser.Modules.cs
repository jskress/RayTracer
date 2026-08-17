using Lex;
using Lex.Parser;
using RayTracer.Extensions;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This file carries the one thing an L-system word needs from the language: a way to turn a piece
/// of argument text into a term.  The reading of the word itself lives with the L-system code, in
/// <see cref="RayTracer.Geometry.LSystems.ModuleWord"/>, since knowing that a letter may be
/// followed by a balanced parenthesis is L-system knowledge rather than parser knowledge.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method compiles one piece of expression text to a term, by handing it to the same
    /// expression parser the rest of the language uses.  Nothing about an L-system's arithmetic is
    /// special, so nothing about it is written twice: a module's argument may use scene variables,
    /// the built-in functions, and anything else an expression may hold.
    /// </summary>
    /// <param name="text">The expression text to compile.</param>
    /// <returns>The term that text describes.</returns>
    public static Term CompileModuleArgument(string text)
    {
        LexicalParser parser = LanguageDsl.CreateLexicalParser();

        parser.SetSource(text.AsReader());

        Term term = (Term) LanguageDsl.ParseExpression(parser);

        parser.Close();

        return term;
    }
}
