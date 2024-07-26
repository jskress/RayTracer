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
    /// This method is used to parse a clause that defines a pigment.
    /// </summary>
    private PigmentInstructionSet ParsePigmentClause()
    {
        Clause clause = ParseClause("pigmentClause");
        
        if (clause.Tokens.IsNullOrEmpty() || clause.Tokens[0].Text == "color")
        {
            Term term = (Term) clause.Expressions.RemoveFirst();

            return PigmentInstructionSet.SolidPigmentInstructionSet(term);
        }

        PigmentType type = ToPigmentType(clause);
        PigmentInstructionSet first = ParsePigmentClause();

        CurrentParser.MatchToken(
            true, () => "Expecting a comma here.", OperatorToken.Comma);

        PigmentInstructionSet second = ParsePigmentClause();
        TransformInstructionSet transformInstructionSet = ParseTransformClause();

        CurrentParser.MatchToken(
            true, () => "Expecting a close brace here.", BounderToken.CloseBrace);

        return PigmentInstructionSet.CompoundPigmentInstructionSet(
            type, transformInstructionSet, first, second);
    }

    /// <summary>
    /// This is a helper method for converting the next token into a representative
    /// pigment type.
    /// </summary>
    /// <param name="clause">The clause to pull from.</param>
    /// <returns>The representative pigment type.</returns>
    private static PigmentType ToPigmentType(Clause clause)
    {
        Token token = clause.Tokens.RemoveFirst();

        return token.Text switch
        {
            "checker" => PigmentType.Checker,
            "ring" => PigmentType.Ring,
            "stripe" => PigmentType.Stripe,
            "linear" => PigmentType.LinearGradient,
            _ => throw new Exception($"Internal error: unknown pigment type: {token.Text}")
        };
    }
}
