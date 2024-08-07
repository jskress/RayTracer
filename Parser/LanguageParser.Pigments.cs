using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Terms;

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
        string text = clause.Text();
        bool bouncing = false;
        
        if (text is "" or "color")
        {
            Term term = (Term) clause.Expressions.RemoveFirst();

            return PigmentInstructionSet.SolidPigmentInstructionSet(term);
        }

        if (text == "bouncing")
        {
            bouncing = true;

            clause.Tokens.RemoveFirst();
        }

        PigmentType type = ToPigmentType(clause);
        InitializeNoisyPigment initializeInstruction = type == PigmentType.Noise
            ? ParseNoisyPigmentInstruction()
            : null;
        List<PigmentInstructionSet> sets = [ParsePigmentClause()];

        if (type != PigmentType.Noise)
        {
            CurrentParser.MatchToken(
                true, () => "Expecting a comma here.", OperatorToken.Comma);

            sets.Add(ParsePigmentClause());
        }

        TransformInstructionSet transformInstructionSet = ParseTransformClause();

        CurrentParser.MatchToken(
            true, () => "Expecting a close brace here.", BounderToken.CloseBrace);

        return PigmentInstructionSet.CompoundPigmentInstructionSet(
            type, initializeInstruction, transformInstructionSet, bouncing, sets.ToArray());
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
            "blend" => PigmentType.Blend,
            "linear" => PigmentType.LinearGradient,
            "radial" => PigmentType.RadialGradient,
            "noisy" => PigmentType.Noise,
            _ => throw new Exception($"Internal error: unknown pigment type: {token.Text}")
        };
    }

    /// <summary>
    /// This method is used to parse the turbulence clause for a moisy pigment, if the
    /// clause is present.
    /// </summary>
    /// <returns>The initialization instruction, or <c>null</c>.</returns>
    private InitializeNoisyPigment ParseNoisyPigmentInstruction()
    {
        Clause clause = LanguageDsl.ParseClause(CurrentParser, "turbulenceClause");

        if (clause == null)
            return null;

        Term depthTerm = clause.Term();
        bool phased = clause.Tokens.Count > 1;
        Term tightnessTerm = null;
        Term scaleTerm = null;

        if (phased)
        {
            tightnessTerm = clause.Term(1);
            
            if (clause.Tokens.Count > 2)
                scaleTerm = clause.Term(2);
        }

        return new InitializeNoisyPigment(depthTerm, phased, tightnessTerm, scaleTerm);
    }
}
