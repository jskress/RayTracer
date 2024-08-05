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
        string text = string.Join('.', clause.Tokens[1..]
            .Select(t => t is IdToken || (t is KeywordToken && t.Text != "by")
                ? "<id>" : t.Text));

        if (text == "<id>" || text == "<id>." + BounderToken.OpenBrace.Text)
        {
            return DetermineProperInstructionSet<GroupInstructionSet>(
                clause, null, ParseGroupClause);
        }

        Token token = clause.Tokens[1];
        string variableName = null;
        Term startTerm = null;
        Term endTerm = null;
        Term stepTerm = null;
        bool startIsOpen = false;
        bool endIsOpen = false;

        if (text.StartsWith("<id>"))
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

        GroupInstructionSet instructionSet = new GroupInstructionSet(
            variableName, startTerm, endTerm, stepTerm, startIsOpen, endIsOpen);

        ParseGroupClause(instructionSet);

        return instructionSet;
    }

    /// <summary>
    /// This method is used to create the instruction set from a group block.
    /// </summary>
    /// <param name="instructionSet">The instruction set for the group.</param>
    private void ParseGroupClause(GroupInstructionSet instructionSet)
    {
        _context.PushInstructionSet(instructionSet);

        ParseBlock("groupEntryClause", HandleGroupEntryClause);

        _context.PopInstructionSet();

        instructionSet.AddInstruction(new FinalizeGroupInstruction());
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
                instructionSet.AddInstruction(ParsePlaneClause(clause));
                break;
            case "sphere":
                instructionSet.AddInstruction(ParseSphereClause(clause));
                break;
            case "cube":
                instructionSet.AddInstruction(ParseCubeClause(clause));
                break;
            case "circularSurface":
                if (clause.Tokens[0].Text == "cylinder" || clause.Tokens[1].Text == "cylinder")
                    instructionSet.AddInstruction(ParseCylinderClause(clause));
                else if (clause.Tokens[0].Text == "conic" || clause.Tokens[1].Text == "conic")
                    instructionSet.AddInstruction(ParseConicClause(clause));
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
            case "object":
                ParseObjectClause(clause, groupInstructionSet: instructionSet);
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
