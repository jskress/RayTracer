using Lex.Clauses;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL: here the escape-time and folding fractals that
/// stand beside the Julia set -- the Mandelbulb, the Mandelbox and the Menger sponge.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a Mandelbulb block.
    /// </summary>
    private void HandleStartMandelbulbClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Mandelbulb");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseMandelbulbClause(clause)
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a Mandelbulb block.
    /// </summary>
    private MandelbulbResolver ParseMandelbulbClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<MandelbulbResolver>(
                "mandelbulbEntryClause", HandleMandelbulbEntryClause),
            "mandelbulbEntryClause", HandleMandelbulbEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a Mandelbulb block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleMandelbulbEntryClause(Clause clause)
    {
        MandelbulbResolver resolver = (MandelbulbResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "power":
                    resolver.PowerResolver = new TermResolver<double>
                    {
                        Term = clause.Term(),
                        Validator = power => power >= 2
                            ? null
                            : "A Mandelbulb's power must be at least two; below it the point never runs away."
                    };
                    break;
                case "iterations":
                    resolver.IterationsResolver = Rounds(clause, "A Mandelbulb");
                    break;
                case "detail":
                    resolver.DetailResolver = Positive(clause, "A Mandelbulb's detail");
                    break;
                case "accuracy":
                    resolver.AccuracyResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "mandelbulb");
                    break;
            }
        });
    }

    /// <summary>
    /// This method is used to handle the beginning of a Mandelbox block.
    /// </summary>
    private void HandleStartMandelboxClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Mandelbox");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseMandelboxClause(clause)
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a Mandelbox block.
    /// </summary>
    private MandelboxResolver ParseMandelboxClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<MandelboxResolver>(
                "mandelboxEntryClause", HandleMandelboxEntryClause),
            "mandelboxEntryClause", HandleMandelboxEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a Mandelbox block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleMandelboxEntryClause(Clause clause)
    {
        MandelboxResolver resolver = (MandelboxResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "factor":
                    resolver.FactorResolver = new TermResolver<double>
                    {
                        Term = clause.Term(),
                        Validator = factor => Math.Abs(factor) > 1
                            ? null
                            : "A Mandelbox's factor must be more than one, or less than minus one; one that " +
                              "magnifies nothing makes no fractal."
                    };
                    break;
                case "inner":
                    resolver.InnerRadiusResolver = Positive(clause, "A Mandelbox's inner radius");
                    break;
                case "outer":
                    resolver.OuterRadiusResolver = Positive(clause, "A Mandelbox's outer radius");
                    break;
                case "fold":
                    resolver.FoldResolver = Positive(clause, "A Mandelbox's fold");
                    break;
                case "iterations":
                    resolver.IterationsResolver = Rounds(clause, "A Mandelbox");
                    break;
                case "detail":
                    resolver.DetailResolver = Positive(clause, "A Mandelbox's detail");
                    break;
                case "accuracy":
                    resolver.AccuracyResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "mandelbox");
                    break;
            }
        });
    }

    /// <summary>
    /// This method is used to handle the beginning of a Menger sponge block.
    /// </summary>
    private void HandleStartMengerClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Menger");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseMengerClause(clause)
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a Menger sponge block.
    /// </summary>
    private MengerSpongeResolver ParseMengerClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<MengerSpongeResolver>(
                "mengerEntryClause", HandleMengerEntryClause),
            "mengerEntryClause", HandleMengerEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a Menger sponge block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleMengerEntryClause(Clause clause)
    {
        MengerSpongeResolver resolver = (MengerSpongeResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "iterations":
                    resolver.IterationsResolver = new TermResolver<int>
                    {
                        Term = clause.Term(),
                        Validator = count => count >= 0
                            ? null
                            : "A Menger sponge cannot be cut fewer than no times."
                    };
                    break;
                case "accuracy":
                    resolver.AccuracyResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "menger");
                    break;
            }
        });
    }

    /// <summary>
    /// This method returns the resolver for a fractal's count of rounds, which must be at least one.
    /// </summary>
    private static TermResolver<int> Rounds(Clause clause, string what)
    {
        return new TermResolver<int>
        {
            Term = clause.Term(),
            Validator = count => count >= 1 ? null : $"{what} must be iterated at least once."
        };
    }
}
