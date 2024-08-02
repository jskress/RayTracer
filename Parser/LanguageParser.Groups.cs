using Lex.Clauses;
using Lex.Tokens;
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
    /// This method is used to handle the beginning of a group block.
    /// </summary>
    private void HandleStartGroupClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Group");

        GroupInstructionSet instructionSet = ParseGroupClause(clause);

        _ = new TopLevelObjectInstruction<Group>(_context.InstructionContext, instructionSet);
    }

    /// <summary>
    /// This method is used to create the instruction set from a group block.
    /// </summary>
    /// <param name="clause">The clause that started the group.</param>
    private GroupInstructionSet ParseGroupClause(Clause clause)
    {
        Token token = clause.Tokens[1];
        string variableName = null;
        Term startTerm = null;
        Term endTerm = null;
        Term stepTerm = null;
        bool startIsOpen = false;
        bool endIsOpen = false;

        if (token is IdToken or KeywordToken)
        {
            clause.Tokens.RemoveRange(1, 2);

            variableName = token.Text;
            token = clause.Tokens[1];
        }

        if (BounderToken.LeftParen.Matches(token) ||
            BounderToken.OpenBracket.Matches(token))
        {
            startTerm = (Term) clause.Expressions[0];
            endTerm = (Term) clause.Expressions[1];
            startIsOpen = BounderToken.LeftParen.Matches(token);
            endIsOpen = BounderToken.LeftParen.Matches(clause.Tokens[3]);

            clause.Expressions.RemoveRange(0, 2);
            clause.Tokens.RemoveRange(1, 3);

            if (clause.Tokens[1].Text == "by")
            {
                stepTerm = (Term) clause.Expressions[0];

                clause.Expressions.RemoveFirst();
                clause.Tokens.RemoveAt(1);
            }
        }

        GroupInstructionSet instructionSet = new (
            variableName, startTerm, endTerm, stepTerm, startIsOpen, endIsOpen);

        _context.PushInstructionSet(instructionSet);

        ParseBlock("groupEntryClause", HandleGroupEntryClause);

        _context.PopInstructionSet();

        instructionSet.AddInstruction(new FinalizeGroupInstruction());

        return instructionSet;
    }

    /// <summary>
    /// This method is used to handle an item clause of a group block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleGroupEntryClause(Clause clause)
    {
        GroupInstructionSet instructionSet = (GroupInstructionSet) _context.CurrentSet;

        if (clause == null)
        {
            HandleSurfaceTransform(instructionSet);

            return;            
        }

        switch (clause.Tag)
        {
            case "plane":
                instructionSet.AddInstruction(ParsePlaneClause());
                break;
            case "sphere":
                instructionSet.AddInstruction(ParseSphereClause());
                break;
            case "cube":
                instructionSet.AddInstruction(ParseCubeClause());
                break;
            case "circularSurface":
                bool open = clause.Tokens[0].Text == "open";
                if (clause.Tokens[0].Text == "cylinder" || clause.Tokens[1].Text == "cylinder")
                    instructionSet.AddInstruction(ParseCylinderClause(open));
                else if (clause.Tokens[0].Text == "conic" || clause.Tokens[1].Text == "conic")
                    instructionSet.AddInstruction(ParseConicClause(open));
                else
                    throw new Exception("Internal error: unknown circular surface type.");
                break;
            case "triangle":
                instructionSet.AddInstruction(ParseTriangleClause(clause));
                break;
            case "smooth":
                instructionSet.AddInstruction(ParseSmoothTriangleClause(clause));
                break;
            case "objectFile":
                instructionSet.AddInstruction(ParseObjectFileClause(clause));
                break;
            case "csg":
                instructionSet.AddInstruction(ParseCsgClause(clause));
                break;
            case "group":
                instructionSet.AddInstruction(ParseGroupClause(clause));
                break;
            case "boundingBox":
                instructionSet.AddInstruction(new SetBoundingBoxInstruction(
                    (Term) clause.Expressions[0], (Term) clause.Expressions[1]));
                break;
            case "surface":
                HandleSurfaceClause(clause, instructionSet, "group");
                break;
        }
    }
}
