using Lex.Clauses;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Instructions;

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
    /// This method is used to handle a clause for general surface properties.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    /// <param name="instructionSet">The instruction set to add instructions to.</param>
    /// <param name="noun">A noun to use for the object type in case of errors.</param>
    private void HandleSurfaceClause<TObject>(
        Clause clause, ObjectInstructionSet<TObject> instructionSet, string noun)
        where TObject : Surface, new()
    {
        string field = clause.Tokens[0].Text;
        Term term = (Term) clause.Expressions.FirstOrDefault();

        ObjectInstruction<TObject> instruction = field switch
        {
            "named" => CreateNamedInstruction<TObject>(term),
            "material" => new SetChildInstruction<TObject, Material>(
                ParseMaterialClause(), target => target.Material),
            "no" => new SetObjectPropertyInstruction<TObject, bool>(
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
        TransformInstructionSet instructions = ParseTransformClause();
        
        if (instructions != null)
        {
            instructionSet.AddInstruction(new SetChildInstruction<TObject, Matrix>(
                instructions, target => target.Transform));
        }
    }
}
