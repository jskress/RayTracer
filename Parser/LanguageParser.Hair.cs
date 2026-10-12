using Lex.Clauses;
using RayTracer.Extensions;
using RayTracer.Basics;
using RayTracer.Instructions;
using RayTracer.Instructions.Core;
using RayTracer.Instructions.Surfaces;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method reads the body of a hair block, the opening of which has been read already.
    /// </summary>
    /// <returns>The resolver for the hair.</returns>
    private HairGrowthResolver ParseHairClause()
    {
        return ParseObjectResolver<HairGrowthResolver>("hairEntryClause", HandleHairEntryClause);
    }

    /// <summary>
    /// This method is used to handle one property of a hair block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleHairEntryClause(Clause clause)
    {
        if (clause == null)
            throw CreateUnexpectedInputException("Expecting a valid hair property here.");

        HairGrowthResolver resolver = (HairGrowthResolver) _context.CurrentTarget;
        Term term = clause.Term();

        switch (ToCmd(clause))
        {
            case "density":
                resolver.DensityResolver = new TermResolver<double?>
                {
                    Term = term, Validator = value => value > 0 ? null : "A hair's density must be greater than nought."
                };
                break;
            case "count":
                resolver.CountResolver = new TermResolver<int?>
                {
                    Term = term, Validator = value => value >= 0 ? null : "A hair's count may not be less than nought."
                };
                break;
            case "length":
                resolver.LengthResolver = Positive(term, "length");
                break;
            case "vary":
                resolver.VaryResolver = new TermResolver<double>
                {
                    Term = term,
                    Validator = value => value is >= 0 and < 1
                        ? null
                        : "How much a hair's length varies must be at least nought and less than one."
                };
                break;
            case "thickness":
                resolver.ThicknessResolver = new TermResolver<double?>
                {
                    Term = term, Validator = value => value > 0 ? null : "A hair's thickness must be greater than nought."
                };
                break;
            case "lean":
                // In the scene's own unit of angle, as a rotation is; more than a right angle would grow
                // the strand back into its surface.
                resolver.LeanResolver = new AngleResolver
                {
                    Term = term,
                    Validator = value => value is >= 0 and <= Math.PI / 2 + 1e-9
                        ? null
                        : "A hair's lean must be from nought to a right angle."
                };
                break;
            case "comb":
                resolver.CombResolver = new TermResolver<Vector> { Term = term };
                break;
            case "curl":
                resolver.CurlResolver = new TermResolver<double> { Term = term };

                if (clause.Expressions.Count > 1)
                {
                    resolver.CurlRadiusResolver = new TermResolver<double?>
                    {
                        Term = clause.Term(1),
                        Validator = value => value > 0 ? null : "A curl's radius must be greater than nought."
                    };
                }
                break;
            case "frizz":
                resolver.FrizzResolver = NotNegative(term, "frizz");
                break;
            case "clump":
                resolver.ClumpResolver = new TermResolver<double>
                {
                    Term = term,
                    Validator = value => value is >= 0 and <= 1 ? null : "A hair's clump must be from nought to one."
                };

                if (clause.Expressions.Count > 1)
                {
                    resolver.ClumpSizeResolver = new TermResolver<int>
                    {
                        Term = clause.Term(1),
                        Validator = value => value >= 1 ? null : "A lock of hair must hold at least one strand."
                    };
                }
                break;
            case "droop":
                resolver.DroopResolver = NotNegative(term, "droop");
                break;
            case "material":
                resolver.MaterialResolver = GetMaterialResolverForUse(clause);
                break;
            case "with.seed":
                resolver.SeedResolver = new TermResolver<int?> { Term = term };
                break;
            case "patches":
                resolver.PatchesResolver = ParseDensityShapeClause(clause);
                break;
            case "facing":
                resolver.FacingResolver = new TermResolver<Vector> { Term = term };

                if (clause.Expressions.Count > 1)
                {
                    resolver.FacingPowerResolver = new TermResolver<double>
                    {
                        Term = clause.Term(1),
                        Validator = value => value > 0 ? null : "How strongly hair favors a facing must be more than nought."
                    };
                }
                break;
            default:
                throw new Exception($"Internal error: unknown hair property found: {ToCmd(clause)}.");
        }

        return;

        static TermResolver<double> Positive(Term term, string what) => new ()
        {
            Term = term, Validator = value => value > 0 ? null : $"A hair's {what} must be greater than nought."
        };

        static TermResolver<double> NotNegative(Term term, string what) => new ()
        {
            Term = term, Validator = value => value >= 0 ? null : $"A hair's {what} may not be less than nought."
        };
    }
}
