using Lex.Clauses;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.General;
using RayTracer.Instructions.Core;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method reads a run of keys, the open brace already read: each a time, a colon, a value, and how the
    /// value comes to it from the key before -- `linear` unless said, `ease in`, `ease out`, `ease` or `ease in
    /// out`, or `hold` -- up to the close brace.
    /// </summary>
    /// <param name="clause">The clause that starts the keys.</param>
    /// <returns>The resolver for the value the keys make.</returns>
    private KeysResolver ParseKeysClause(Clause clause)
    {
        bool smooth = clause.Tokens.Any(token => token.Text == "smooth");
        bool loops = clause.Tokens.Any(token => token.Text == "loop");
        List<(Term Time, Term Value, Keyframes.Way Way)> keys = [];

        while (!CurrentParser.IsNext(BounderToken.CloseBrace))
        {
            Term time = (Term) LanguageDsl.ParseExpression(CurrentParser);

            CurrentParser.MatchToken(true, () => "Expecting a colon to follow the key's time here.", OperatorToken.Colon);

            Term value = (Term) LanguageDsl.ParseExpression(CurrentParser);
            Token said = CurrentParser.PeekNextToken();
            Keyframes.Way way = Keyframes.Way.Linear;

            switch (said?.Text)
            {
                case "linear":
                    CurrentParser.GetNextToken();
                    break;
                case "hold":
                    CurrentParser.GetNextToken();
                    way = Keyframes.Way.Hold;
                    break;
                case "ease":
                    CurrentParser.GetNextToken();
                    way = EaseOf();
                    break;
            }

            if (smooth && way != Keyframes.Way.Linear)
            {
                throw new TokenException(
                    "Smooth keys are passed through at a pace of their own, so a key among them cannot ease or hold.")
                {
                    Token = said
                };
            }

            keys.Add((time, value, way));
        }

        Token close = CurrentParser.GetNextToken();

        if (keys.Count == 0)
            throw new TokenException("Keys need at least one key.") { Token = close };

        return new KeysResolver { Keys = keys, Smooth = smooth, Loops = loops };
    }

    // What follows `ease`: `in`, `out`, `in out`, or nothing, which is in and out both.
    private Keyframes.Way EaseOf()
    {
        if (CurrentParser.PeekNextToken() is { Text: "out" })
        {
            CurrentParser.GetNextToken();

            return Keyframes.Way.EaseOut;
        }

        if (CurrentParser.PeekNextToken() is not { Text: "in" })
            return Keyframes.Way.EaseInOut;

        CurrentParser.GetNextToken();

        if (CurrentParser.PeekNextToken() is not { Text: "out" })
            return Keyframes.Way.EaseIn;

        CurrentParser.GetNextToken();

        return Keyframes.Way.EaseInOut;
    }
}
