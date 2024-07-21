using Lex.Clauses;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;
using RayTracer.Instructions;

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

        PointLightInstructionSet lightInstructionSet = new ();

        _context.PushInstructionSet(lightInstructionSet);

        ParseBlock("pointLightEntryClause");
        
        _context.PopInstructionSet();

        if (_context.CurrentSet is SceneInstructionSet sceneInstructionSet)
        {
            sceneInstructionSet.AddInstruction(new AddChildInstruction<Scene, PointLight>(
                sceneInstructionSet, lightInstructionSet,
                scene => scene.Lights));
        }
        else
            _ = new TopLevelObjectInstruction<PointLight>(_context.InstructionContext, lightInstructionSet);
    }

    /// <summary>
    /// This method is used to handle an item clause of a light block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandlePointLightEntryClause(Clause clause)
    {
        PointLightInstructionSet instructionSet = (PointLightInstructionSet) _context.CurrentSet;
        string field = clause.Tokens[0].Text;
        Term term = (Term) clause.Expressions.First();

        ObjectInstruction<PointLight> instruction = field switch
        {
            "named" => new SetObjectPropertyInstruction<PointLight, string>(
                target => target.Name, term),
            "location" => new SetObjectPropertyInstruction<PointLight, Point>(
                target => target.Location, term),
            "color" => new SetObjectPropertyInstruction<PointLight, Color>(
                target => target.Color, term),
            _ => throw new Exception($"Internal error: unknown light property found: {field}.")
        };

        instructionSet.AddInstruction(instruction);
    }
}
