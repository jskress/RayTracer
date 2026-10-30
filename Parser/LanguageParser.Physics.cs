using Lex.Clauses;
using Lex.Parser;
using RayTracer.Basics;
using RayTracer.Extensions;
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
    /// This method is used to handle an item clause of a body block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleBodyEntryClause(Clause clause)
    {
        BodyResolver resolver = (BodyResolver) _context.CurrentTarget;

        switch (clause.Text())
        {
            case "mass":
                resolver.MassResolver = MoreThanNought(clause, "A body's mass");
                break;
            case "bounce":
                resolver.BounceResolver = OneOrLess(clause, "How much of its speed a body keeps");
                break;
            case "friction":
                resolver.FrictionResolver = NoughtOrMore(clause, "A body's friction");
                break;
            case "drag":
                resolver.DragResolver = NoughtOrMore(clause, "A body's drag");
                break;
            case "radius":
                resolver.RadiusResolver = MoreThanNought(clause, "A body's radius");
                break;
            case "velocity":
                resolver.VelocityResolver = new TermResolver<Vector> { Term = clause.Term() };
                break;
            case "spin":
                resolver.SpinTerm = clause.Term();
                break;
            case "shape":
                resolver.IsABox = clause.Text(1) == "box";
                break;
            case "tether":
                resolver.Tethers.Add((clause.Term(), clause.Expressions.Count > 1 ? clause.Term(1) : null));
                break;
            default:
                throw new Exception($"Internal error: unknown body property found: {clause.Text()}.");
        }
    }

    /// <summary>
    /// This method is used to handle the beginning of a swarm block.
    /// </summary>
    /// <param name="clause">The clause that starts the swarm.</param>
    private void HandleStartSwarmClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Swarm");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseSwarmClause(clause)
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a swarm block.
    /// </summary>
    /// <param name="clause">The clause that starts the swarm.</param>
    private SwarmResolver ParseSwarmClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<SwarmResolver>("swarmEntryClause", HandleSwarmEntryClause),
            "swarmEntryClause", HandleSwarmEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a swarm block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleSwarmEntryClause(Clause clause)
    {
        SwarmResolver resolver = (SwarmResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "of":
                    // A named surface, or a call of a primitive, as a field's copies are.
                    resolver.ThingResolver = clause.Tokens.Count > 2
                        ? ParseCall(clause)
                        : GetExtensibleItem<ISurfaceResolver>(clause.Tokens[1], false);
                    break;
                case "within":
                    // Like the thing itself: a named surface, or a call of a primitive.
                    RefuseBoth(resolver.FromResolver, clause);
                    resolver.WithinResolver = clause.Tokens.Count > 2
                        ? ParseCall(clause)
                        : GetExtensibleItem<ISurfaceResolver>(clause.Tokens[1], false);
                    break;
                case "from":
                    RefuseBoth(resolver.WithinResolver, clause);
                    resolver.FromResolver = new TermResolver<Point> { Term = clause.Term() };
                    break;
                case "toward":
                    resolver.TowardResolver = new TermResolver<Vector> { Term = clause.Term() };
                    break;
                case "spread":
                    resolver.SpreadResolver = new AngleResolver { Term = clause.Term() };
                    break;
                case "rate":
                    RefuseFlowAndBurst(resolver.BurstResolver, clause);
                    resolver.RateResolver = MoreThanNought(clause, "How many copies a swarm throws a second");
                    break;
                case "since":
                    RefuseFlowAndBurst(resolver.BurstResolver, clause);
                    resolver.SinceResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                case "burst":
                {
                    RefuseFlowAndBurst(resolver.RateResolver ?? resolver.SinceResolver, clause);
                    resolver.BurstResolver = new TermResolver<double>
                    {
                        Term = clause.Term(),
                        Validator = value => value >= 1 && value == Math.Floor(value)
                            ? null
                            : "A burst throws a whole number of copies, one or more."
                    };

                    // When it comes and how long it takes are each optional, so which expression is which is told by
                    // the words before them.
                    int next = 1;

                    resolver.BurstAtResolver = clause.Tokens.Any(token => token.Text == "at")
                        ? new TermResolver<double> { Term = clause.Term(next++) }
                        : null;
                    resolver.BurstOverResolver = clause.Tokens.Any(token => token.Text == "over")
                        ? NoughtOrMore(clause, "How long a burst takes", next)
                        : null;
                    break;
                }
                case "bounce":
                    resolver.BounceResolver = OneOrLess(clause, "How much of its speed a copy keeps");
                    break;
                case "friction":
                    resolver.FrictionResolver = OneOrLess(clause, "How much of its speed across a copy loses");
                    break;
                case "drag":
                    resolver.DragResolver = NoughtOrMore(clause, "A swarm's drag");
                    break;
                case "radius":
                    resolver.RadiusResolver = MoreThanNought(clause, "A swarm's radius");
                    break;
                case "speed":
                    resolver.SpeedFromResolver = NoughtOrMore(clause, "A swarm's speed");
                    resolver.SpeedToResolver = clause.Expressions.Count > 1 ? NoughtOrMore(clause, "A swarm's speed", 1) : null;
                    break;
                case "life":
                    resolver.LifeFromResolver = MoreThanNought(clause, "A copy's life");
                    resolver.LifeToResolver = clause.Expressions.Count > 1 ? MoreThanNought(clause, "A copy's life", 1) : null;
                    break;
                case "spin":
                    resolver.SpinFromResolver = Spin(clause, 0);
                    resolver.SpinToResolver = clause.Expressions.Count > 1 ? Spin(clause, 1) : null;
                    break;
                case "falls":
                    resolver.FallsFromResolver = MoreThanNought(clause, "How fast a copy falls");
                    resolver.FallsToResolver = clause.Expressions.Count > 1 ? MoreThanNought(clause, "How fast a copy falls", 1) : null;
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "swarm");
                    break;
            }
        });
    }

    // A swarm throws its copies from a place or from within a shape, not both.
    private static void RefuseBoth(object other, Clause clause)
    {
        if (other is not null)
        {
            throw new TokenException("A swarm throws its copies from a place or from within a shape, not both.")
            {
                Token = clause.Tokens[0]
            };
        }
    }

    // A swarm throws a steady flow, from a time on at a rate, or one burst, not both.
    private static void RefuseFlowAndBurst(object other, Clause clause)
    {
        if (other is not null)
        {
            throw new TokenException(
                "A swarm throws a steady flow, with \"rate\" and \"since\", or one burst, not both.")
            {
                Token = clause.Tokens[0]
            };
        }
    }

    // How fast a copy spins, an angle a second.
    private static AngleResolver Spin(Clause clause, int index) => new ()
    {
        Term = clause.Term(index), Validator = value => value >= 0 ? null : "A copy's spin may not be less than nothing."
    };

    private static TermResolver<double> MoreThanNought(Clause clause, string what, int index = 0) => new ()
    {
        Term = clause.Term(index), Validator = value => value > 0 ? null : $"{what} must be more than nothing."
    };

    private static TermResolver<double> NoughtOrMore(Clause clause, string what, int index = 0) => new ()
    {
        Term = clause.Term(index), Validator = value => value >= 0 ? null : $"{what} may not be less than nothing."
    };

    private static TermResolver<double> OneOrLess(Clause clause, string what, int index = 0) => new ()
    {
        Term = clause.Term(index),
        Validator = value => value is >= 0 and <= 1 ? null : $"{what} runs from nothing to one."
    };
}
