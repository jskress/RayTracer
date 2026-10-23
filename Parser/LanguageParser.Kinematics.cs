using Lex.Clauses;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Core;
using RayTracer.Instructions.Surfaces;
using RayTracer.Instructions.Transforms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a joint block at the top of a scene.
    /// </summary>
    /// <param name="clause">The clause that starts the joint.</param>
    private void HandleStartJointClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Joint");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseJointClause(clause)
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a joint block.
    /// <para>
    /// The joint's turn is put first among its transforms once the whole block is read, so that it turns the joint
    /// in its own space before they place it, wherever in the block the transforms were written -- and a
    /// `transform` by name, which replaces what was gathered, cannot drop it.
    /// </para>
    /// </summary>
    /// <param name="clause">The clause that starts the joint.</param>
    private JointResolver ParseJointClause(Clause clause)
    {
        JointResolver resolver = GetSurfaceResolver(
            clause, () => ParseObjectResolver<JointResolver>("groupEntryClause", HandleJointEntryClause),
            "groupEntryClause", HandleJointEntryClause);

        resolver.TransformResolver ??= new TransformResolver();

        if (!resolver.TransformResolver.TransformCreators.Contains(resolver.Turn))
            resolver.TransformResolver.TransformCreators.Insert(0, resolver.Turn);

        return resolver;
    }

    /// <summary>
    /// This method is used to handle an item clause of a joint block: one of the joint's own, or anything a group
    /// may hold.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleJointEntryClause(Clause clause)
    {
        JointResolver resolver = (JointResolver) _context.CurrentTarget;

        if (clause?.Tag != "jointProperty")
        {
            HandleGroupEntryClause(clause);

            return;
        }

        switch (clause.Text())
        {
            case "pivot":
                resolver.Turn.PivotResolver = new TermResolver<Point> { Term = clause.Term() };
                break;
            case "angle":
                resolver.Turn.AngleResolver = new AngleResolver { Term = clause.Term() };
                break;
            case "facing":
                resolver.FacingResolver = new TermResolver<Vector>
                {
                    Term = clause.Term(),
                    Validator = value => value.Magnitude > 0 ? null : "A joint must face some way, not nothing."
                };
                break;
            case "axis":
                resolver.Turn.AxisResolver = clause.Tokens.Count > 1
                    ? new LiteralResolver<Vector>
                    {
                        Value = clause.Text(1) switch
                        {
                            "X" => new Vector(1, 0, 0),
                            "Y" => new Vector(0, 1, 0),
                            _ => new Vector(0, 0, 1)
                        }
                    }
                    : new TermResolver<Vector>
                    {
                        Term = clause.Term(),
                        Validator = value => value.Magnitude > 0 ? null : "A joint must turn about some way, not nothing."
                    };
                break;
            case "limits":
                resolver.Turn.LowestResolver = new AngleResolver { Term = clause.Term() };
                resolver.Turn.HighestResolver = new AngleResolver { Term = clause.Term(1) };
                break;
            case "aim":
                resolver.AimTerm = clause.Term();
                break;
            case "reach":
                resolver.ReachTerm = clause.Term();
                resolver.EffectorTerm = clause.Term(1);
                break;
        }
    }

    /// <summary>
    /// This method is used to create a mark from its clause: a name, and where it stands.
    /// </summary>
    /// <param name="clause">The mark's clause.</param>
    /// <returns>The mark's resolver.</returns>
    private static MarkResolver ParseMarkClause(Clause clause) => new ()
    {
        NameTerm = clause.Term(),
        AtResolver = new TermResolver<Point> { Term = clause.Term(1) }
    };

    /// <summary>
    /// This method is used to create a span from its clause: the names of the two places, and whether it stretches.
    /// </summary>
    /// <param name="clause">The span's clause.</param>
    /// <returns>The span's resolver.</returns>
    private static SpanResolver ParseSpanClause(Clause clause) => new ()
    {
        FromTerm = clause.Term(),
        ToTerm = clause.Term(1),
        Stretched = clause.Tokens.Any(token => token.Text == "stretched")
    };
}
