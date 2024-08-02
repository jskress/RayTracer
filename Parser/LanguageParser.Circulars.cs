using Lex.Clauses;
using Lex.Parser;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Instructions;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a circular surface block.
    /// </summary>
    private void HandleStartCircularSurfaceClause(Clause clause)
    {
        bool open = clause.Tokens[0].Text == "open";

        if (open)
            clause.Tokens.RemoveFirst();

        string text = clause.Tokens[0].Text;

        VerifyDefaultSceneUsage(clause, $"{char.ToUpper(text[0])}{text[1..]}");

        switch (text)
        {
            case "cylinder":
            {
                CylinderInstructionSet cylinderInstructionSet = ParseCylinderClause(open);
                _ = new TopLevelObjectInstruction<Cylinder>(_context.InstructionContext, cylinderInstructionSet);
                break;
            }
            case "conic":
            {
                ConicInstructionSet conicInstructionSet = ParseConicClause(open);
                _ = new TopLevelObjectInstruction<Conic>(_context.InstructionContext, conicInstructionSet);
                break;
            }
            default:
                throw new TokenException($"Internal error: unknown circular surface type: {text}.")
                {
                    Token = clause.Tokens[0]
                };
        }
    }

    /// <summary>
    /// This method is used to create the instruction set from a cylinder block.
    /// </summary>
    private CylinderInstructionSet ParseCylinderClause(bool open)
    {
        CylinderInstructionSet instructionSet = new ();

        ParseCircularSurfaceClause(instructionSet, open);

        return instructionSet;
    }

    /// <summary>
    /// This method is used to create the instruction set from a conic block.
    /// </summary>
    private ConicInstructionSet ParseConicClause(bool open)
    {
        ConicInstructionSet instructionSet = new ();

        ParseCircularSurfaceClause(instructionSet, open);

        return instructionSet;
    }

    /// <summary>
    /// This method is used to create the instruction set from a circularSurface block.
    /// </summary>
    private void ParseCircularSurfaceClause<TObject>(
        ObjectInstructionSet<TObject> instructionSet, bool open)
        where TObject : CircularSurface, new()
    {
        _context.PushInstructionSet(instructionSet);

        ParseBlock("circularSurfaceEntryClause", HandleCircularSurfaceEntryClause);

        _context.PopInstructionSet();

        if (open)
        {
            instructionSet.AddInstruction(new SetObjectPropertyInstruction<TObject, bool>(
                target => target.Closed, false));
        }
    }

    /// <summary>
    /// This method is used to handle an item clause of a circularSurface block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleCircularSurfaceEntryClause(Clause clause)
    {
        IInstructionSet instructionSet = _context.CurrentSet;

        switch (instructionSet)
        {
            case CylinderInstructionSet cylinderInstructionSet:
                HandleCircularSurfaceEntryClause(cylinderInstructionSet, clause);
                break;
            case ConicInstructionSet conicInstructionSet:
                HandleCircularSurfaceEntryClause(conicInstructionSet, clause);
                break;
            default:
                throw new Exception("Internal error: unknown circular surface instruction set type.");
        }
    }

    /// <summary>
    /// This method is used to handle an item clause of a circularSurface block.
    /// </summary>
    /// <param name="instructionSet">The instruction set to work with.</param>
    /// <param name="clause">The clause to process.</param>
    private void HandleCircularSurfaceEntryClause<TObject>(
        ObjectInstructionSet<TObject> instructionSet, Clause clause)
        where TObject : CircularSurface, new()
    {
        if (clause == null) // We must have hit a transform property...
            HandleSurfaceTransform(instructionSet);
        else
        {
            string text = clause.Tokens[0].Text;

            switch (text)
            {
                case "min":
                    instructionSet.AddInstruction(new SetObjectPropertyInstruction<TObject, double>(
                        target => target.MinimumY, (Term) clause.Expressions[0]));
                    break;
                case "max":
                    instructionSet.AddInstruction(new SetObjectPropertyInstruction<TObject, double>(
                        target => target.MaximumY, (Term) clause.Expressions[0]));
                    break;
                default:
                    HandleSurfaceClause(clause, instructionSet, "circularSurface");
                    break;
            }
        }
    }
}
