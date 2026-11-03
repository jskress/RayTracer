using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method reads the spline a clause names or begins: written out after an open brace, a catenary hung in
    /// place, or the name of one, looked up where the thing that goes along it is made, so it may be a primitive's
    /// parameter.  A catenary written in place is how a primitive hangs one, its body holding only what it gives back.
    /// </summary>
    /// <param name="clause">The clause holding "spline" and what followed it.</param>
    /// <returns>The resolver for the spline.</returns>
    internal Resolver<Spline> SplineNamedBy(Clause clause)
    {
        Token last = clause.Tokens[^1];

        if (!BounderToken.OpenBrace.Matches(last))
            return new TermResolver<Spline> { Term = new VariableTerm(last) };

        if (clause.Tokens[^2].Text == "catenary")
            return ParseCatenaryClause();

        return clause.Text() == "discontinuous" ? ParseDiscontinuousSpline() : ParseSplineClause();
    }

    /// <summary>
    /// This method is used to create the resolver for a catenary, a spline hung between two places; the caller has
    /// already matched "catenary" and the open brace after it.
    /// </summary>
    private CatenaryResolver ParseCatenaryClause()
    {
        return ParseObjectResolver<CatenaryResolver>("catenaryEntryClause", HandleCatenaryEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a catenary block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleCatenaryEntryClause(Clause clause)
    {
        if (clause == null)
            throw CreateUnexpectedInputException("Expecting a valid catenary property here.");

        CatenaryResolver resolver = (CatenaryResolver) _context.CurrentTarget;

        switch (clause.Text())
        {
            case "from":
                resolver.FromResolver = new TermResolver<Basics.Point> { Term = clause.Term() };
                break;
            case "to":
                resolver.ToResolver = new TermResolver<Basics.Point> { Term = clause.Term() };
                break;
            case "sag":
                resolver.SagResolver = new TermResolver<double>
                {
                    Term = clause.Term(),
                    Validator = value => value > 0 ? null : "A catenary's sag must be more than nought."
                };
                break;
            case "length":
                resolver.LengthResolver = new TermResolver<double>
                {
                    Term = clause.Term(),
                    Validator = value => value > 0 ? null : "A catenary's length must be more than nought."
                };
                break;
            default:
                throw new Exception($"Internal error: unknown catenary property found: {clause.Text()}.");
        }
    }

    /// <summary>
    /// This method is used to handle the beginning of a top-level rope block.
    /// </summary>
    /// <param name="clause">The clause which starts the rope.</param>
    private void HandleStartRopeClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Rope");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseRopeClause(clause)
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a rope block.
    /// </summary>
    private RopeResolver ParseRopeClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<RopeResolver>("ropeEntryClause", HandleRopeEntryClause),
            "ropeEntryClause", HandleRopeEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a rope block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleRopeEntryClause(Clause clause)
    {
        RopeResolver resolver = (RopeResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "spline" or "discontinuous":
                    resolver.SplineResolver = SplineNamedBy(clause);
                    break;
                case "radius":
                    resolver.RadiusResolver = new TermResolver<double>
                    {
                        Term = clause.Term(),
                        Validator = value => value > 0 ? null : "A rope's radius must be more than nought."
                    };
                    break;
                case "strands":
                    resolver.StrandsResolver = new TermResolver<int>
                    {
                        Term = clause.Term(),
                        Validator = value => value >= 2
                            ? null
                            : "A rope is laid from at least two strands; a single one is a tube."
                    };
                    break;
                case "lay":
                    resolver.LayResolver = new TermResolver<double>
                    {
                        Term = clause.Term(),
                        Validator = value => value != 0
                            ? null
                            : "A rope's lay must not be nought: its strands would never get anywhere."
                    };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "rope");
                    break;
            }
        });
    }

    /// <summary>
    /// This method is used to handle the beginning of a top-level chain block.
    /// </summary>
    /// <param name="clause">The clause which starts the chain.</param>
    private void HandleStartChainClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Chain");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseChainClause(clause)
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a chain block.
    /// </summary>
    private ChainResolver ParseChainClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<ChainResolver>("chainEntryClause", HandleChainEntryClause),
            "chainEntryClause", HandleChainEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a chain block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleChainEntryClause(Clause clause)
    {
        ChainResolver resolver = (ChainResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "spline" or "discontinuous":
                    resolver.SplineResolver = SplineNamedBy(clause);
                    break;
                case "link":
                    resolver.LinkResolver = Positive(clause, "A chain's link");
                    break;
                case "wire":
                    resolver.WireResolver = Positive(clause, "A chain's wire");
                    break;
                case "width":
                    resolver.WidthResolver = Positive(clause, "A chain's width");
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "chain");
                    break;
            }
        });
    }

    /// <summary>
    /// This method returns the resolver for a size that must be more than nought.
    /// </summary>
    private static TermResolver<double> Positive(Clause clause, string what) => new ()
    {
        Term = clause.Term(),
        Validator = value => value > 0 ? null : $"{what} must be more than nought."
    };
}
