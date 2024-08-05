using Lex.Clauses;
using Lex.Parser;
using RayTracer.Core;
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
    /// This method is used to handle the beginning of an object file block.
    /// </summary>
    private void HandleStartObjectFileClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Object file");

        ObjectFileInstructionSet instructionSet = ParseObjectFileClause(clause);

        _ = new TopLevelObjectInstruction<Group>(_context.InstructionContext, instructionSet);
    }

    /// <summary>
    /// This method is used to create the instruction set from an object file block.
    /// </summary>
    private ObjectFileInstructionSet ParseObjectFileClause(Clause clause)
    {
        Term fileName = (Term) clause.Expressions[0];

        ObjectFileInstructionSet instructionSet = new (CurrentDirectory, fileName);

        _context.PushInstructionSet(instructionSet);

        ParseBlock("surfaceEntryClause", HandleObjectFileEntryClause);

        _context.PopInstructionSet();

        instructionSet.AddInstruction(new FinalizeGroupInstruction());

        return instructionSet;
    }

    /// <summary>
    /// This method is used to handle an item clause of an object file block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleObjectFileEntryClause(Clause clause)
    {
        ObjectFileInstructionSet instructionSet = (ObjectFileInstructionSet) _context.CurrentSet;

        if (clause == null) // We must have hit a transform property...
            HandleSurfaceTransform(instructionSet);
        else
            HandleSurfaceClause(clause, instructionSet, "object file");
    }

    /// <summary>
    /// This method is used to handle the beginning of an object block.
    /// </summary>
    private void HandleStartObjectClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Object");
        ParseObjectClause(clause);
    }

    /// <summary>
    /// This method is used to parse an object reference clause.
    /// </summary>
    /// <param name="clause">The clause to parse.</param>
    /// <param name="sceneInstructionSet">If given, add the copy here.</param>
    /// <param name="groupInstructionSet">If given, add the copy here.</param>
    /// <param name="csgSurfaceInstructionSet">If given, add the copy here.</param>
    private void ParseObjectClause(
        Clause clause, SceneInstructionSet sceneInstructionSet = null,
        GroupInstructionSet groupInstructionSet = null,
        CsgSurfaceInstructionSet csgSurfaceInstructionSet = null)
    {
        string variableName = clause.Tokens[1].Text;
        bool shouldParse = clause.Tokens.Count > 2;

        if (!_context.ExtensibleItems.TryGetValue(variableName, out ICopyableInstructionSet set))
        {
            throw new TokenException(
                $"The variable name, {variableName}, is not defined or is not of the proper type.")
            {
                Token = clause.Tokens[1]
            };
        }

        switch (set)
        {
            case PlaneInstructionSet planeInstructionSet:
                CopyParseAndStore(
                    planeInstructionSet, sceneInstructionSet, groupInstructionSet,
                    csgSurfaceInstructionSet, shouldParse);
                break;
            case SphereInstructionSet sphereInstruction:
                CopyParseAndStore(
                    sphereInstruction, sceneInstructionSet, groupInstructionSet,
                    csgSurfaceInstructionSet, shouldParse);
                break;
            case CubeInstructionSet cubeInstruction:
                CopyParseAndStore(
                    cubeInstruction, sceneInstructionSet, groupInstructionSet,
                    csgSurfaceInstructionSet, shouldParse);
                break;
            case CylinderInstructionSet cylinderInstructionSet:
                CopyParseAndStore(
                    cylinderInstructionSet, sceneInstructionSet, groupInstructionSet,
                    csgSurfaceInstructionSet, shouldParse);
                break;
            case ConicInstructionSet conicInstructionSet:
                CopyParseAndStore(
                    conicInstructionSet, sceneInstructionSet, groupInstructionSet,
                    csgSurfaceInstructionSet, shouldParse);
                break;
            case TriangleInstructionSet triangleInstructionSet:
                CopyParseAndStore(
                    triangleInstructionSet, sceneInstructionSet, groupInstructionSet,
                    csgSurfaceInstructionSet, shouldParse);
                break;
            case SmoothTriangleInstructionSet smoothTriangleInstructionSet:
                CopyParseAndStore(
                    smoothTriangleInstructionSet, sceneInstructionSet, groupInstructionSet,
                    csgSurfaceInstructionSet, shouldParse);
                break;
            case ObjectFileInstructionSet objectFileInstructionSet:
                CopyParseAndStore(
                    objectFileInstructionSet, sceneInstructionSet, groupInstructionSet,
                    csgSurfaceInstructionSet, shouldParse);
                break;
            case CsgSurfaceInstructionSet surfaceInstructionSet:
                CopyParseAndStore(
                    surfaceInstructionSet, sceneInstructionSet, groupInstructionSet,
                    csgSurfaceInstructionSet, shouldParse);
                break;
            case GroupInstructionSet instructionSet:
                CopyParseAndStore(
                    instructionSet, sceneInstructionSet, groupInstructionSet,
                    csgSurfaceInstructionSet, shouldParse);
                break;
        }
    }

    /// <summary>
    /// This method is used to make a copy of the given instruction set, parse more
    /// information if appropriate, and store it in the proper destination.
    /// </summary>
    /// <remarks>If none of the target instruction sets are provided, the copy will be set
    /// up to add its object to the top level object list.</remarks>
    /// <param name="instructionSet">The instruction set to copy.</param>
    /// <param name="sceneInstructionSet">If given, add the copy here.</param>
    /// <param name="groupInstructionSet">If given, add the copy here.</param>
    /// <param name="csgSurfaceInstructionSet">If given, add the copy here.</param>
    /// <param name="shouldParse">A flag noting whether we should parse more info.</param>
    private void CopyParseAndStore<TObject>(
        CopyableObjectInstructionSet<TObject> instructionSet,
        SceneInstructionSet sceneInstructionSet, GroupInstructionSet groupInstructionSet,
        CsgSurfaceInstructionSet csgSurfaceInstructionSet, bool shouldParse)
        where TObject : Surface, new()
    {
        instructionSet = (CopyableObjectInstructionSet<TObject>) instructionSet.Copy();

        if (shouldParse)
            ParseSurfaceInfo(instructionSet);

        if (sceneInstructionSet != null)
        {
            sceneInstructionSet.AddInstruction(new AddChildInstruction<Scene, Surface, TObject>(
                instructionSet, scene => scene.Surfaces));
        }
        else if (groupInstructionSet != null)
            groupInstructionSet.AddInstruction(instructionSet);
        else if (csgSurfaceInstructionSet != null)
            csgSurfaceInstructionSet.AddInstruction(instructionSet);
        else
            _ = new TopLevelObjectInstruction<TObject>(_context.InstructionContext, instructionSet);
    }
}
