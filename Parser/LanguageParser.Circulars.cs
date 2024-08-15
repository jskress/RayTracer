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
        string text = clause.Text();

        if (text == "open")
            text = clause.Text(1);

        VerifyDefaultSceneUsage(clause, $"{char.ToUpper(text[0])}{text[1..]}");

        switch (text)
        {
            case "cylinder":
            {
                CylinderInstructionSet cylinderInstructionSet = ParseCylinderClause(clause);
                _ = new TopLevelObjectInstruction<Cylinder>(
                    _context.InstructionContext, cylinderInstructionSet);
                break;
            }
            case "conic":
            {
                ConicInstructionSet conicInstructionSet = ParseConicClause(clause);
                _ = new TopLevelObjectInstruction<Conic>(
                    _context.InstructionContext, conicInstructionSet);
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
    private CylinderInstructionSet ParseCylinderClause(Clause clause)
    {
        bool open = IsCircularOpen(clause);

        return DetermineProperInstructionSet(
            clause, () => new CylinderInstructionSet(), 
            set => ParseCircularSurfaceClause(set, open));
    }

    /// <summary>
    /// This method is used to create the instruction set from a conic block.
    /// </summary>
    private ConicInstructionSet ParseConicClause(Clause clause)
    {
        bool open = IsCircularOpen(clause);

        return DetermineProperInstructionSet(
            clause, () => new ConicInstructionSet(), 
            set => ParseCircularSurfaceClause(set, open));
    }

    /// <summary>
    /// This is a helper method for checking the given clause to see if it starts with the
    /// "open" token.  If so, it is removed.
    /// </summary>
    /// <param name="clause">The clause to check.</param>
    /// <returns><c>true</c>, if the clause started with an "open" token, or <c>false</c>,
    /// if not.</returns>
    private static bool IsCircularOpen(Clause clause)
    {
        if (clause.Text() == "open")
        {
            clause.Tokens.RemoveFirst();

            return true;
        }

        return false;
    }

    /// <summary>
    /// This method is used to create the instruction set from a circularSurface block.
    /// </summary>
    private void ParseCircularSurfaceClause<TObject>(
        ObjectInstructionSet<TObject> instructionSet, bool open)
        where TObject : ExtrudedSurface, new()
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
        where TObject : ExtrudedSurface, new()
    {
        if (clause == null) // We must have hit a transform property...
            HandleSurfaceTransform(instructionSet);
        else
        {
            string text = clause.Text();

            switch (text)
            {
                case "min":
                    instructionSet.AddInstruction(new SetObjectPropertyInstruction<TObject, double>(
                        target => target.MinimumY, clause.Term()));
                    break;
                case "max":
                    instructionSet.AddInstruction(new SetObjectPropertyInstruction<TObject, double>(
                        target => target.MaximumY, clause.Term()));
                    break;
                default:
                    HandleSurfaceClause(clause, instructionSet, "circularSurface");
                    break;
            }
        }
    }
}
