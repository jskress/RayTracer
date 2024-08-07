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
        Term term = clause.Term();

        ObjectInstruction<TObject> instruction = field switch
        {
            "named" => CreateNamedInstruction<TObject>(term),
            "material" => GetMaterialInstruction<TObject>(clause),
            "transform" => GetTransformInstruction<TObject>(clause),
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
    /// <param name="clause">The clause that tells us what type of instruction to create.</param>
    /// <returns>The proper instruction.</returns>
    private ObjectInstruction<TObject> GetMaterialInstruction<TObject>(Clause clause)
        where TObject : Surface, new()
    {
        Token token = clause.Tokens[1];

        if (BounderToken.OpenBrace.Matches(token))
        {
            return new SetChildInstruction<TObject, Material>(
                ParseMaterialClause(), target => target.Material);
        }

        if (token.Text == "inherited")
            return new MarkMaterialForInheritanceInstruction<TObject>();

        if (clause.Tokens.Count > 2)
        {
            clause.Tokens.RemoveFirst();

            return new SetChildInstruction<TObject, Material>(
                DetermineProperInstructionSet<MaterialInstructionSet>(
                    clause, set => ParseMaterialClause(set), true),
                target => target.Material);
        }

        return new SetObjectPropertyInstruction<TObject, Material>(
            target => target.Material, new VariableTerm(token),
            material => material == null ? "Could not resolve this to a material." : null);
    }

    /// <summary>
    /// This is a helper method for creating the right instruction for setting the transform
    /// property of a surface.
    /// </summary>
    /// <param name="clause">The clause that tells us what type of instruction to create.</param>
    /// <returns>The proper instruction.</returns>
    private ObjectInstruction<TObject> GetTransformInstruction<TObject>(Clause clause)
        where TObject : Surface, new()
    {
        Token token = clause.Tokens[1];

        if (clause.Tokens.Count > 2)
        {
            clause.Tokens.RemoveFirst();

            return new SetChildInstruction<TObject, Matrix>(
                DetermineProperInstructionSet<TransformInstructionSet>(
                    clause, set => ParseTransformClause(set), false),
                target => target.Transform);
        }

        return new SetObjectPropertyInstruction<TObject, Matrix>(
            target => target.Transform, new VariableTerm(token),
            matrix => matrix == null ? "Could not resolve this to a matrix." : null);
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

    /// <summary>
    /// This method is used to handle a clause for general surface properties.
    /// </summary>
    /// <param name="instructionSet">The instruction set to add instructions to.</param>
    private void ParseSurfaceInfo<TObject>(ObjectInstructionSet<TObject> instructionSet)
        where TObject : Surface, new()
    {
        _context.PushInstructionSet(instructionSet);

        ParseBlock("surfaceEntryClause", HandleSurfaceInfoEntry);

        _context.PopInstructionSet();
    }

    /// <summary>
    /// This method is used to handle an item clause of an object block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleSurfaceInfoEntry(Clause clause)
    {
        IInstructionSet instructionSet = _context.CurrentSet;

        switch (instructionSet)
        {
            case PlaneInstructionSet:
                HandlePlaneEntryClause(clause);
                break;
            case SphereInstructionSet:
                HandleSphereEntryClause(clause);
                break;
            case CubeInstructionSet:
                HandleCubeEntryClause(clause);
                break;
            case CylinderInstructionSet:
            case ConicInstructionSet:
                HandleCircularSurfaceEntryClause(clause);
                break;
            case TriangleInstructionSet:
                HandleTriangleEntryClause(clause);
                break;
            case SmoothTriangleInstructionSet:
                HandleSmoothTriangleEntryClause(clause);
                break;
            case ObjectFileInstructionSet:
                HandleObjectFileEntryClause(clause);
                break;
            case CsgSurfaceInstructionSet:
                HandleCsgEntryClause(clause, "surface");
                break;
            case GroupInstructionSet:
                HandleGroupEntryClause(clause);
                break;
        }
    }
}
