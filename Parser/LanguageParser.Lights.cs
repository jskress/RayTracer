using Lex.Clauses;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Graphics;
using RayTracer.Instructions;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a point light block.
    /// </summary>
    private void HandleStartPointLightClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Point light");

        PointLightInstructionSet instructionSet = ParsePointLightClause();

        _ = new TopLevelObjectInstruction<PointLight>(_context.InstructionContext, instructionSet);
    }

    /// <summary>
    /// This method is used to create the instruction set from a point light block.
    /// </summary>
    private PointLightInstructionSet ParsePointLightClause()
    {
        PointLightInstructionSet instructionSet = new ();

        _context.PushInstructionSet(instructionSet);

        ParseBlock("pointLightEntryClause", HandlePointLightEntryClause);

        _context.PopInstructionSet();

        return instructionSet;
    }

    /// <summary>
    /// This method is used to handle an item clause of a light block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandlePointLightEntryClause(Clause clause)
    {
        PointLightInstructionSet instructionSet = (PointLightInstructionSet) _context.CurrentSet;
        string field = clause.Text();
        Term term = clause.Term();
    
        ObjectInstruction<PointLight> instruction = field switch
        {
            "named" => CreateNamedInstruction<PointLight>(term),
            "location" => new SetObjectPropertyInstruction<PointLight, Point>(
                target => target.Location, term),
            "color" => new SetObjectPropertyInstruction<PointLight, Color>(
                target => target.Color, term),
            _ => throw new Exception($"Internal error: unknown light property found: {field}.")
        };

        instructionSet.AddInstruction(instruction);
    }
}
