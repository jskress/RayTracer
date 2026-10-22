using Lex.Clauses;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.Instructions.Transforms;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method reads the body of a motion block: the transforms that carry a surface from
    /// where it starts to where it ends up, which are the same transforms it could be placed with
    /// in the first place.
    /// </summary>
    /// <returns>The resolver for the motion.</returns>
    private TransformResolver ParseMotionClause()
    {
        TransformResolver resolver = new ();

        while (!CurrentParser.IsNext(BounderToken.CloseBrace))
        {
            int before = resolver.TransformCreators.Count;

            ParseTransformClause(resolver);

            // A zero-or-more clause reports success having read nothing at all, so without this
            // anything that is not a transform would spin here rather than be complained about.
            if (resolver.TransformCreators.Count == before)
                throw CreateUnexpectedInputException("Expecting a transform here.");
        }

        // Make sure we eat the closing brace.
        CurrentParser.GetNextToken();

        if (resolver.TransformCreators.Count == 0)
            throw CreateUnexpectedInputException("A motion needs at least one transform.");

        // A motion is a share of each of its transforms, and a place along a spline has no share; refused here, as
        // it is read, since a motion is only ever worked out when a shutter is open.
        if (resolver.TransformCreators.OfType<AlongCreator>().FirstOrDefault() is { } along)
        {
            throw new TokenException(
                "A motion is a share of a transform, and a place along a spline has no share: move a thing along " +
                "a spline in terms of the time instead.")
            {
                Token = along.SplineTerm.ErrorToken
            };
        }

        return resolver;
    }

    /// <summary>
    /// This method is used to parse a clause of zero or more transformations.
    /// </summary>
    private TransformResolver ParseTransformClause(TransformResolver resolver = null)
    {
        Clause clause = ParseClause("transformClause");
        
        if (clause == null)
            return null;

        List<Term> terms = clause.Expressions.Cast<Term>().ToList();

        resolver ??= new TransformResolver();

        while (clause.Tokens.Count > 0)
        {
            TransformType type = ToTransformType(clause);
            TransformAxis axis = ToTransformAxis(clause, type);
            TransformCreator creator;
            Term[] transformTerms;

            switch (type)
            {
                case TransformType.Translate:
                    creator = new TranslationCreator();
                    transformTerms = [terms.RemoveFirst()];
                    break;
                case TransformType.Scale:
                    creator = new ScaleCreator();
                    transformTerms = [terms.RemoveFirst()];
                    break;
                case TransformType.Rotate:
                    creator = new RotationCreator();
                    transformTerms = [terms.RemoveFirst()];
                    break;
                case TransformType.Shear:
                    creator = new ShearCreator();
                    transformTerms = TakeBracketedTerms(clause, terms, 6);
                    break;
                case TransformType.Matrix:
                    creator = new MatrixCreator();
                    transformTerms = TakeBracketedTerms(clause, terms, 16);
                    break;
                case TransformType.Along:
                {
                    // `along <spline> at|by <amount> [upright|banked] [wheelbase <w>]`: the words between and
                    // after the values are left in the clause's tokens, and taken here.
                    bool byDistance = clause.Tokens.RemoveFirst().Text == "by";
                    bool banked = TakeWord(clause, "upright", "banked") == "banked";
                    bool wheelbase = TakeWord(clause, "wheelbase") is not null;

                    resolver.TransformCreators.Add(new AlongCreator
                    {
                        SplineTerm = terms.RemoveFirst(), AmountTerm = terms.RemoveFirst(), ByDistance = byDistance,
                        Banked = banked, WheelbaseTerm = wheelbase ? terms.RemoveFirst() : null
                    });

                    continue;
                }
                default:
                    throw new Exception("Unknown transform type");
            }

            creator.Terms = transformTerms;
            creator.Axis = axis;

            resolver.TransformCreators.Add(creator);
        }

        return resolver;
    }

    /// <summary>
    /// This is a helper method for pulling the terms of a bracketed, comma-separated list
    /// of values, such as the one a shear or matrix carries, out of the given list of
    /// terms.  The grammar leaves the brackets and separating commas in the clause's list
    /// of tokens, so we discard those here as well; if we didn't, the next trip around the
    /// parsing loop would try to read one of them as the name of a transform.
    /// </summary>
    /// <param name="clause">The clause to remove the punctuation tokens from.</param>
    /// <param name="terms">The list of terms to pull from.</param>
    /// <param name="count">The number of values in the list.</param>
    /// <returns>The terms for the values in the list.</returns>
    private static Term[] TakeBracketedTerms(Clause clause, List<Term> terms, int count)
    {
        Term[] result = terms[..count].ToArray();

        terms.RemoveRange(0, count);

        // The open bracket, the commas that separate the values, and the close bracket.
        clause.Tokens.RemoveRange(0, count + 1);

        return result;
    }

    // The clause's next word, taken from it, if it is one of those given; otherwise null and the word left.
    private static string TakeWord(Clause clause, params string[] words)
    {
        if (clause.Tokens.FirstOrDefault() is not { } token || !words.Contains(token.Text))
            return null;

        clause.Tokens.RemoveFirst();

        return token.Text;
    }

    /// <summary>
    /// This is a helper method for converting the next token into a representative
    /// transform type.
    /// </summary>
    /// <param name="clause">The clause to pull from.</param>
    /// <returns>The representative transform type.</returns>
    private static TransformType ToTransformType(Clause clause)
    {
        Token token = clause.Tokens.RemoveFirst();

        return token.Text switch
        {
            "translate" => TransformType.Translate,
            "scale" => TransformType.Scale,
            "rotate" => TransformType.Rotate,
            "shear" => TransformType.Shear,
            "matrix" => TransformType.Matrix,
            "along" => TransformType.Along,
            _ => throw new Exception($"Internal error: unknown transfer type: {token.Text}")
        };
    }

    /// <summary>
    /// This is a helper method for converting a token into a representative transform
    /// axis.
    /// </summary>
    /// <param name="clause">The clause to pull from.</param>
    /// <param name="type">The type of transform involved.</param>
    /// <returns>The representative transform axis.</returns>
    private static TransformAxis ToTransformAxis(Clause clause, TransformType type)
    {
        if (type is TransformType.Shear or TransformType.Matrix or TransformType.Along)
            return TransformAxis.None;

        Token token = clause.Tokens.FirstOrDefault();
        TransformAxis axis = token switch
        {
            KeywordToken when token.Text == "X" => TransformAxis.X,
            KeywordToken when token.Text == "Y" => TransformAxis.Y,
            KeywordToken when token.Text == "Z" => TransformAxis.Z,
            _ => TransformAxis.All
        };

        if (axis is not TransformAxis.All)
            clause.Tokens.RemoveFirst();

        return axis;
    }

    /// <summary>
    /// This is a helper method for creating the right resolver, either by parsing an
    /// in-place definition or a variable reference.
    /// </summary>
    /// <param name="clause">The clause that tells us how to get the transform resolver.</param>
    /// <returns>The proper resolver.</returns>
    private TransformResolver GetTransformResolver(Clause clause)
    {
        Token token = clause.Tokens[1];
        bool extending = clause.Tokens.Count > 2;
        bool expectCloseBrace;
        TransformResolver resolver;

        if (BounderToken.OpenBrace.Matches(token))
        {
            resolver = ParseTransformClause();
            expectCloseBrace = true;
        }
        else
        {
            resolver = GetExtensibleItem<TransformResolver>(token, extending);
            expectCloseBrace = extending;
        }

        if (clause.Tokens.Count > 2)
            ParseTransformClause(resolver);

        if (expectCloseBrace)
        {
            CurrentParser.MatchToken(
                true, () => "Expecting a close brace here.",
                BounderToken.CloseBrace);
        }

        return resolver;
    }
}
