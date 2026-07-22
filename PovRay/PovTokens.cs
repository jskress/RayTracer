using Lex;
using Lex.Dsl;
using Lex.Parser;
using Lex.Tokens;

namespace RayTracer.PovRay;

/// <summary>
/// This class turns the text of a POV-Ray file into tokens, and provides the small vocabulary the
/// parser above it uses to ask about them.
/// <para>
/// The tokenizing is left to the Lex library, described the same declarative way the ray tracer's
/// own language is.  POV-Ray needs nothing unusual of it: the numbers, names, strings, comments
/// and operators it is written with are the ones any C-like language uses, and the spec below is
/// the whole of the difference.
/// </para>
/// </summary>
public static class PovTokens
{
    /// <summary>
    /// This is how a POV-Ray file is broken into tokens.
    /// <para>
    /// Two things are worth knowing about what this produces.  A directive arrives as the
    /// <c>#</c> operator followed by a plain name rather than as one token, which is what lets
    /// <c>#   debug</c> read the same as <c>#debug</c>; POV-Ray allows the space and finish.inc
    /// uses it.  And the angle brackets a vector is written in arrive as operators rather than as
    /// bounders, since they are also POV-Ray's comparison operators, so the parser tells a vector
    /// from a comparison by where it is rather than by the token's type.
    /// </para>
    /// </summary>
    private const string PovParserSpec = """
        standard comments
        identifiers
        numbers
        double quoted strings
        bounders
        predefined operators
        whitespace
        """;

    /// <summary>
    /// This method reads the given text into the tokens that carry meaning.  White space and
    /// comments are dropped along the way.
    /// </summary>
    /// <param name="text">The text of the file to read.</param>
    /// <returns>The tokens the file is made of.</returns>
    public static List<Token> Tokenize(string text)
    {
        using LexicalParser parser = LexicalParserFactory.CreateFrom(PovParserSpec);

        parser.SetSource(text.AsReader());

        List<Token> result = [];

        while (!parser.IsAtEnd())
        {
            Token token = parser.GetNextToken();

            if (token is null)
                break;

            if (token is not (WhitespaceToken or CommentToken))
                result.Add(token);
        }

        return result;
    }

    /// <summary>
    /// This method notes whether the given token is the given operator or bounder.  The two are
    /// asked about together because which one a character is depends on the Lex library's own
    /// arrangements rather than on anything POV-Ray cares about: a brace is a bounder and an angle
    /// bracket is an operator, and to the parser they are both simply punctuation.
    /// </summary>
    /// <param name="token">The token to test, which may be <c>null</c> at the end of the file.</param>
    /// <param name="text">The punctuation to test for.</param>
    /// <returns><c>true</c> if the token is that punctuation.</returns>
    public static bool IsPunctuation(this Token token, string text) =>
        token is OperatorToken or BounderToken && token.Text == text;

    /// <summary>
    /// This method notes whether the given token is the given name.  POV-Ray is case-sensitive, so
    /// the match is too.
    /// </summary>
    /// <param name="token">The token to test, which may be <c>null</c> at the end of the file.</param>
    /// <param name="text">The name to test for.</param>
    /// <returns><c>true</c> if the token is that name.</returns>
    public static bool IsIdentifier(this Token token, string text) =>
        token is IdToken && token.Text == text;

    /// <summary>
    /// This method produces a description of the given token suitable for putting in a message to
    /// the user.
    /// </summary>
    /// <param name="token">The token to describe, which may be <c>null</c> at the end of the file.</param>
    /// <returns>The token, described.</returns>
    public static string Describe(this Token token) =>
        token is null ? "the end of the file" : $"\"{token.Text}\"";
}
