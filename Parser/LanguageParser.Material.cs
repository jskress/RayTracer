using Lex.Clauses;
using RayTracer.Core;
using RayTracer.Instructions;
using RayTracer.Pigments;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to parse a clause of zero or more transformations.
    /// </summary>
    private MaterialInstructionSet ParseMaterialClause()
    {
        MaterialInstructionSet instructionSet = new ();

        _context.PushInstructionSet(instructionSet);

        ParseBlock("materialEntryClause", HandleMaterialEntryClause);

        _context.PopInstructionSet();

        return instructionSet;
    }

    /// <summary>
    /// This method is used to handle an item clause of a material block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleMaterialEntryClause(Clause clause)
    {
        MaterialInstructionSet instructionSet = (MaterialInstructionSet) _context.CurrentSet;
        string field = clause.Tokens[0].Text;
        Term term = (Term) clause.Expressions.FirstOrDefault();

        ObjectInstruction<Material> instruction = field switch
        {
            "pigment" => new SetChildInstruction<Material, Pigment>(
                ParsePigmentClause(), target => target.Pigment),
            "ambient" => new SetObjectPropertyInstruction<Material, double>(
                target => target.Ambient, term),
            "diffuse" => new SetObjectPropertyInstruction<Material, double>(
                target => target.Diffuse, term),
            "specular" => new SetObjectPropertyInstruction<Material, double>(
                target => target.Specular, term),
            "shininess" => new SetObjectPropertyInstruction<Material, double>(
                target => target.Shininess, term),
            "reflective" => new SetObjectPropertyInstruction<Material, double>(
                target => target.Reflective, term),
            "transparency" => new SetObjectPropertyInstruction<Material, double>(
                target => target.Transparency, term),
            "index" => new SetObjectPropertyInstruction<Material, double>(
                target => target.IndexOfRefraction, term),
            "ior" => new SetObjectPropertyInstruction<Material, double>(
                target => target.IndexOfRefraction, term),
            _ => throw new Exception($"Internal error: unknown material property found: {field}.")
        };

        instructionSet.AddInstruction(instruction);
    }
}
