using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.Instructions;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to parse a clause of zero or more transformations.
    /// </summary>
    private TransformInstructionSet ParseTransformClause()
    {
        Clause clause = ParseClause("transformClause");
        
        if (clause == null)
            return null;

        List<Term> terms = clause.Expressions.Cast<Term>().ToList();
        TransformInstructionSet instructions = new ();

        while (clause.Tokens.Count > 0)
        {
            TransformType type = ToTransformType(clause);
            TransformAxis axis = ToTransformAxis(clause, type);
            TransformInstruction instruction;

            switch (type)
            {
                case TransformType.Translate:
                    instruction = TransformInstruction.TranslationInstruction(
                        terms.RemoveFirst(), axis);
                    break;
                case TransformType.Scale:
                    instruction = TransformInstruction.ScaleInstruction(
                        terms.RemoveFirst(), axis);
                    break;
                case TransformType.Rotate:
                    instruction = TransformInstruction.RotationInstruction(
                        terms.RemoveFirst(), axis);
                    break;
                case TransformType.Shear:
                    instruction = TransformInstruction.ShearInstruction(terms[..6].ToArray());
                    terms.RemoveRange(0, 6);
                    break;
                case TransformType.Matrix:
                    instruction = TransformInstruction.ShearInstruction(terms[..16].ToArray());
                    terms.RemoveRange(0, 16);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            instructions.AddInstruction(instruction);
        }

        return instructions;
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
}
