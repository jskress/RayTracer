using Lex.Clauses;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Core;
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
    /// This method is used to handle the beginning of a plane block.
    /// </summary>
    private void HandleStartPlaneClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Plane");

        PlaneInstructionSet instructionSet = ParsePlaneClause();

        _ = new TopLevelObjectInstruction<Plane>(_context.InstructionContext, instructionSet);
    }

    /// <summary>
    /// This method is used to create the instruction set from a plane block.
    /// </summary>
    private PlaneInstructionSet ParsePlaneClause()
    {
        PlaneInstructionSet instructionSet = new ();

        _context.PushInstructionSet(instructionSet);

        ParseBlock("surfaceEntryClause", HandlePlaneEntryClause);

        _context.PopInstructionSet();

        return instructionSet;
    }

    /// <summary>
    /// This method is used to handle an item clause of a plane block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandlePlaneEntryClause(Clause clause)
    {
        PlaneInstructionSet instructionSet = (PlaneInstructionSet) _context.CurrentSet;

        if (clause == null) // We must have hit a transform property...
            HandleSurfaceTransform(instructionSet);
        else
            HandleSurfaceClause(clause, instructionSet, "plane");
    }

    /// <summary>
    /// This method is used to handle the beginning of a sphere block.
    /// </summary>
    private void HandleStartSphereClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Sphere");

        SphereInstructionSet instructionSet = ParseSphereClause();

        _ = new TopLevelObjectInstruction<Sphere>(_context.InstructionContext, instructionSet);
    }

    /// <summary>
    /// This method is used to create the instruction set from a sphere block.
    /// </summary>
    private SphereInstructionSet ParseSphereClause()
    {
        SphereInstructionSet instructionSet = new ();

        _context.PushInstructionSet(instructionSet);

        ParseBlock("surfaceEntryClause", HandleSphereEntryClause);

        _context.PopInstructionSet();

        return instructionSet;
    }

    /// <summary>
    /// This method is used to handle an item clause of a sphere block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleSphereEntryClause(Clause clause)
    {
        SphereInstructionSet instructionSet = (SphereInstructionSet) _context.CurrentSet;

        if (clause == null) // We must have hit a transform property...
            HandleSurfaceTransform(instructionSet);
        else
            HandleSurfaceClause(clause, instructionSet, "sphere");
    }

    /// <summary>
    /// This method is used to handle the beginning of a cube block.
    /// </summary>
    private void HandleStartCubeClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Cube");

        CubeInstructionSet instructionSet = ParseCubeClause();

        _ = new TopLevelObjectInstruction<Cube>(_context.InstructionContext, instructionSet);
    }

    /// <summary>
    /// This method is used to create the instruction set from a cube block.
    /// </summary>
    private CubeInstructionSet ParseCubeClause()
    {
        CubeInstructionSet instructionSet = new ();

        _context.PushInstructionSet(instructionSet);

        ParseBlock("surfaceEntryClause", HandleCubeEntryClause);

        _context.PopInstructionSet();

        return instructionSet;
    }

    /// <summary>
    /// This method is used to handle an item clause of a cube block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleCubeEntryClause(Clause clause)
    {
        CubeInstructionSet instructionSet = (CubeInstructionSet) _context.CurrentSet;

        if (clause == null) // We must have hit a transform property...
            HandleSurfaceTransform(instructionSet);
        else
            HandleSurfaceClause(clause, instructionSet, "cube");
    }

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

    /// <summary>
    /// This method is used to handle a clause for general surface properties.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    /// <param name="instructionSet">The instruction set to add instructions to.</param>
    /// <param name="noun">A noun to use for the object type in case of errors.</param>
    private void HandleSurfaceClause<TObject>(
        Clause clause, ObjectInstructionSet<TObject> instructionSet, string noun)
        where TObject : Surface, new()
    {
        string field = ToCmd(clause);
        Term term = (Term) clause.Expressions.FirstOrDefault();

        ObjectInstruction<TObject> instruction = field switch
        {
            "named" => CreateNamedInstruction<TObject>(term),
            "material" => GetMaterialInstruction<TObject>(clause.Tokens[1]),
            "transform" => new SetObjectPropertyInstruction<TObject, Matrix>(
                target => target.Transform, new VariableTerm(clause.Tokens[1]),
                matrix => matrix == null ? "Could not resolve this to a matrix." : null),
            "no.shadow" => new SetObjectPropertyInstruction<TObject, bool>(
                target => target.NoShadow, true),
            _ => throw new Exception($"Internal error: unknown {noun} property found: {field}.")
        };

        instructionSet.AddInstruction(instruction);
    }

    /// <summary>
    /// This is a helper method for creating the right instruction for setting the material
    /// property of a surface.
    /// </summary>
    /// <param name="token">The token that tells us what type of instruction to create.</param>
    /// <returns>The proper instruction.</returns>
    private ObjectInstruction<TObject> GetMaterialInstruction<TObject>(Token token)
        where TObject : Surface, new()
    {
        if (BounderToken.OpenBrace.Matches(token))
        {
            return new SetChildInstruction<TObject, Material>(
                ParseMaterialClause(), target => target.Material);
        }

        if (token.Text == "inherited")
        {
            if (_context.ParentSet is not GroupInstructionSet)
            {
                throw new TokenException("The material cannot be inherited since the surface is not in a group.")
                {
                    Token = token
                };
            }

            return new MarkMaterialForInheritanceInstruction<TObject>();
        }

        return new SetObjectPropertyInstruction<TObject, Material>(
            target => target.Material, new VariableTerm(token),
            material => material == null ? "Could not resolve this to a material." : null);
    }

    /// <summary>
    /// This method is used to handle adding a transformation instruction for the current
    /// surface if such a clause was specified.
    /// </summary>
    /// <param name="instructionSet">The instruction set to add instructions to.</param>
    private void HandleSurfaceTransform<TObject>(ObjectInstructionSet<TObject> instructionSet)
        where TObject : Surface, new()
    {
        Token token = CurrentParser.PeekNextToken();
        TransformInstructionSet instructions = ParseTransformClause();

        if (instructions == null) // We found something we don't understand.
        {
            throw new TokenException("Expecting a close brace here.")
            {
                Token = token
            };
        }

        if (instructionSet.TouchesPropertyNamed("Transform"))
        {
            throw new TokenException("Cannot specify transforms when the transform property is used directly.")
            {
                Token = token
            };
        }

        instructionSet.AddInstruction(new SetChildInstruction<TObject, Matrix>(
            instructions, target => target.Transform));
    }
}
