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
                ObjectInstructionSet<Cylinder> cylinderInstructionSet =
                    ParseCircularSurfaceClause<Cylinder>(open);
                _ = new TopLevelObjectInstruction<Cylinder>(_context.InstructionContext, cylinderInstructionSet);
                break;
            }
            case "conic":
            {
                ObjectInstructionSet<Conic> conicInstructionSet =
                    ParseCircularSurfaceClause<Conic>(open);
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
    /// This method is used to create the instruction set from a circularSurface block.
    /// </summary>
    private ObjectInstructionSet<TObject> ParseCircularSurfaceClause<TObject>(bool open)
        where TObject : CircularSurface, new()
    {
        ObjectInstructionSet<TObject> instructionSet = new ();

        _context.PushInstructionSet(instructionSet);

        ParseBlock("circularSurfaceEntryClause", HandleCircularSurfaceEntryClause);

        _context.PopInstructionSet();

        if (open)
        {
            instructionSet.AddInstruction(new SetObjectPropertyInstruction<TObject, bool>(
                target => target.Closed, false));
        }

        return instructionSet;
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
            case ObjectInstructionSet<Cylinder> cylinderInstructionSet:
                HandleCircularSurfaceEntryClause(cylinderInstructionSet, clause);
                break;
            case ObjectInstructionSet<Conic> conicInstructionSet:
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
            "material" => BounderToken.OpenBrace.Matches(clause.Tokens[1])
                ? new SetChildInstruction<TObject, Material>(
                    ParseMaterialClause(), target => target.Material)
                : new SetObjectPropertyInstruction<TObject, Material>(
                    target => target.Material,
                    new VariableTerm(clause.Tokens[1])),
            "transform" => new SetObjectPropertyInstruction<TObject, Matrix>(
                target => target.Transform, new VariableTerm(clause.Tokens[1])),
            "no.shadow" => new SetObjectPropertyInstruction<TObject, bool>(
                target => target.NoShadow, true),
            _ => throw new Exception($"Internal error: unknown {noun} property found: {field}.")
        };

        instructionSet.AddInstruction(instruction);
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
