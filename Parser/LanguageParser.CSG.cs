using Lex.Clauses;
using RayTracer.Geometry;
using RayTracer.Instructions;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a CSG block.
    /// </summary>
    private void HandleStartCsgClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, clause.Tokens[0].Text);

        CsgSurfaceInstructionSet instructionSet = ParseCsgClause(clause);

        _ = new TopLevelObjectInstruction<CsgSurface>(_context.InstructionContext, instructionSet);
    }

    /// <summary>
    /// This method is used to create the instruction set from a csg block.
    /// </summary>
    /// <param name="clause">The clause that started the CSG.</param>
    private CsgSurfaceInstructionSet ParseCsgClause(Clause clause)
    {
        string text = clause.Tokens[0].Text;

        if (text == "csg")
        {
            return DetermineProperInstructionSet<CsgSurfaceInstructionSet>(
                clause, null, ParseCsgClause);
        }

        CsgOperation operation = text switch
        {
            "union" => CsgOperation.Union,
            "difference" => CsgOperation.Difference,
            "intersection" => CsgOperation.Intersection,
            _ => throw new Exception($"Internal error: unknown CSG type: {text}.")
        };

        CsgSurfaceInstructionSet instructionSet = new (operation, clause.Tokens[0]);

        ParseCsgClause(instructionSet);

        return instructionSet;
    }

    /// <summary>
    /// This method is used to create the instruction set from a csg block.
    /// </summary>
    /// <param name="instructionSet">The instruction set to use.</param>
    private void ParseCsgClause(CsgSurfaceInstructionSet instructionSet)
    {
        _context.PushInstructionSet(instructionSet);

        ParseBlock("csgEntryClause", HandleCsgEntryClause);

        _context.PopInstructionSet();
    }

    /// <summary>
    /// This method is used to handle an item clause of a csg block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleCsgEntryClause(Clause clause)
    {
        HandleCsgEntryClause(clause, null);
    }

    /// <summary>
    /// This method is used to handle an item clause of a csg block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    /// <param name="tag">The tag to use, if we need to override the one in the clause.</param>
    private void HandleCsgEntryClause(Clause clause, string tag)
    {
        CsgSurfaceInstructionSet instructionSet = (CsgSurfaceInstructionSet) _context.CurrentSet;

        if (clause == null)
        {
            HandleSurfaceTransform(instructionSet);

            return;            
        }

        switch (tag ?? clause.Tag)
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
            case "torus":
                instructionSet.AddInstruction(ParseTorusClause(clause));
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
                ParseObjectClause(clause, csgSurfaceInstructionSet: instructionSet);
                break;
            case "csg":
                instructionSet.AddInstruction(ParseCsgClause(clause));
                break;
            case "group":
                instructionSet.AddInstruction(ParseGroupClause(clause));
                break;
            case "surface":
                HandleSurfaceClause(clause, instructionSet, "csg");
                break;
        }
    }
}
