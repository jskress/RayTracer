using Lex.Clauses;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Graphics;
using RayTracer.Instructions;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of an environment block, which says what is true
    /// of the space between a scene's objects.
    /// </summary>
    private void HandleStartEnvironmentClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Environment");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseEnvironmentBlock()
        });
    }

    /// <summary>
    /// This method is used to handle the environment property written as a single line, which is the
    /// shorthand the index of refraction arrived as.
    /// </summary>
    private void HandleEnvironmentClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Environment");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseEnvironmentClause(clause)
        });
    }

    /// <summary>
    /// This method is used to create the resolver for an environment block.
    /// </summary>
    /// <returns>The resolver for the block.</returns>
    private SceneEnvironmentResolver ParseEnvironmentBlock()
    {
        return ParseObjectResolver<SceneEnvironmentResolver>(
            "environmentEntryClause", HandleEnvironmentEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of an environment block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleEnvironmentEntryClause(Clause clause)
    {
        if (clause == null)
            throw CreateUnexpectedInputException("Expecting a valid environment property here.");

        SceneEnvironmentResolver resolver = (SceneEnvironmentResolver) _context.CurrentTarget;

        switch (ToCmd(clause))
        {
            case "index":
            case "ior":
                resolver.IndexOfRefractionResolver = CreateIndexOfRefractionResolver(clause);
                break;
            case "medium":
                // The space outside everything has no far side, so a medium filling it must be one
                // that has an answer over an endless span.  One that gives off light where it
                // absorbs none has no such answer: with nothing to settle it, its own light piles up
                // without limit.  Said of something bounded that is a perfectly good description, so
                // it is only refused here.
                resolver.MediumResolver = ParseMediumClause(ForTheSurroundings);
                break;
            default:
                throw new NotSupportedException("Unknown environment property found.");
        }
    }

    /// <summary>
    /// This method checks a medium against what filling the endless surroundings asks of one.  Both
    /// things refused here are refused for the same underlying reason: the surroundings have no far
    /// side, and each would need one.
    /// </summary>
    /// <param name="medium">The medium to check.</param>
    /// <returns>What is wrong with it, or <c>null</c> if nothing is.</returns>
    private static string ForTheSurroundings(Core.Medium medium)
    {
        // Light given off where nothing takes light back out has nothing to settle it, so over an
        // endless span such a medium is infinitely bright.
        if (medium.MustBeBounded)
        {
            return "A medium filling the surroundings must absorb or scatter wherever it emits, " +
                   "since the surroundings have no far side for its light to stop at.  Give it some " +
                   "of either, or give the medium a surface to fill instead.";
        }

        // A crossing with no end can only be sampled at all because there is a distance past which
        // nothing could still reach the eye, and that rests on a floor under how much stuff there is.
        // A shape free to thin toward nothing takes the floor away, and with it any honest stopping
        // point -- so rather than invent one, this asks for a shape to be given an end of its own.
        return medium.HasShape
            ? "A medium whose density varies must fill a surface rather than the surroundings, " +
              "since a crossing with no end has nowhere to stop when the stuff in it may thin away " +
              "to nothing.  Put the medium in a surface -- a large flattened box, for a ground fog."
            : null;
    }

    /// <summary>
    /// This method is used to create the resolver for an environment clause written as a single line.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    /// <returns>The resolver for the clause.</returns>
    private static SceneEnvironmentResolver ParseEnvironmentClause(Clause clause)
    {
        return new SceneEnvironmentResolver
        {
            IndexOfRefractionResolver = CreateIndexOfRefractionResolver(clause)
        };
    }

    /// <summary>
    /// This is a helper method for creating the resolver for an index of refraction, which must be a
    /// real substance's: nothing may have an index of nothing or less.
    /// </summary>
    /// <param name="clause">The clause to pull the index from.</param>
    /// <returns>The resolver for the index.</returns>
    private static TermResolver<double> CreateIndexOfRefractionResolver(Clause clause)
    {
        return new TermResolver<double>
        {
            Term = clause.Term(),
            Validator = index => index > 0
                ? null
                : "An index of refraction must be greater than zero."
        };
    }

    /// <summary>
    /// This method is used to parse a medium block: what fills a piece of space.
    /// </summary>
    /// <param name="validator">A validator to apply to the medium once it is resolved, or
    /// <c>null</c> if anything the block may say is allowed.</param>
    /// <returns>The resolver for the medium.</returns>
    private MediumResolver ParseMediumClause(Func<Core.Medium, string> validator = null)
    {
        MediumResolver resolver = ParseObjectResolver<MediumResolver>(
            "mediumEntryClause", HandleMediumEntryClause);

        resolver.Validator = validator;

        return resolver;
    }

    /// <summary>
    /// This method is used to handle an item clause of a medium block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleMediumEntryClause(Clause clause)
    {
        if (clause == null)
            throw CreateUnexpectedInputException("Expecting a valid medium property here.");

        MediumResolver resolver = (MediumResolver) _context.CurrentTarget;
        Term term = clause.Term();

        switch (clause.Text())
        {
            case "absorption":
                resolver.AbsorptionResolver = new CoefficientResolver
                {
                    Term = term, Validator = ValidateCoefficient("absorb")
                };
                break;
            case "emission":
                resolver.EmissionResolver = new CoefficientResolver
                {
                    Term = term, Validator = ValidateCoefficient("emit")
                };
                break;
            case "scattering":
                resolver.ScatteringResolver = new CoefficientResolver
                {
                    Term = term, Validator = ValidateCoefficient("turn aside")
                };
                break;
            case "anisotropy":
                resolver.AnisotropyResolver = new TermResolver<double>
                {
                    Term = term,
                    // At one exactly, every scrap of light would go straight on and the shape has no
                    // value at all to give in any other direction.
                    Validator = anisotropy => Math.Abs(anisotropy) < 1
                        ? null
                        : "An anisotropy must lie between minus one and one, and reach neither."
                };
                break;
            case "phase":
                resolver.PhaseFunctionResolver = new LiteralResolver<PhaseFunction>
                {
                    Value = PhaseFunction.Rayleigh
                };
                break;
            case "bounces":
                resolver.BouncesResolver = new TermResolver<int>
                {
                    Term = term,
                    Validator = bounces => bounces < 0
                        ? "A path cannot be turned fewer than no times."
                        : null
                };
                break;
            case "samples":
                resolver.SamplesResolver = new TermResolver<int>
                {
                    Term = term,
                    Validator = samples => samples < 1
                        ? "A medium must be sampled in at least one place."
                        : null
                };
                break;
            case "density":
                if (clause.Text(1) == "function")
                {
                    resolver.DensityFieldResolver = new FieldExpressionResolver { Term = term };

                    break;
                }

                resolver.DensityResolver = new TermResolver<double>
                {
                    Term = term,
                    Validator = density => density >= 0
                        ? null
                        : "A density cannot be less than nothing."
                };
                break;
            default:
                throw new NotSupportedException("Unknown medium property found.");
        }
    }

    /// <summary>
    /// This is a helper method for checking that one of a medium's coefficients is a rate at which
    /// something happens, and so cannot run backward.
    /// </summary>
    /// <param name="verb">What the medium would be doing at a negative rate.</param>
    /// <returns>The validator to use.</returns>
    private static Func<Color, string> ValidateCoefficient(string verb)
    {
        return color => color.Red >= 0 && color.Green >= 0 && color.Blue >= 0
            ? null
            : $"A medium cannot {verb} light at less than no rate at all.";
    }
}
