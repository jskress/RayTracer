using Lex.Clauses;
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
                    transformTerms = terms[..6].ToArray();
                    terms.RemoveRange(0, 6);
                    break;
                case TransformType.Matrix:
                    creator = new MatrixCreator();
                    transformTerms = terms[..16].ToArray();
                    terms.RemoveRange(0, 16);
                    break;
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
        if (type is TransformType.Shear or TransformType.Matrix)
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
