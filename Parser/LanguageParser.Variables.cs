using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Instructions;
using RayTracer.Pigments;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle a clause that specifies the assignment of a value to
    /// a variable
    /// </summary>
    private void HandleSetVariableClause(Clause clause)
    {
        string name = clause.Tokens[0].Text;
        Term term = (Term) clause.Expressions[0];

        _context.InstructionContext.AddInstruction(new SetVariableInstruction(name, term));
    }

    /// <summary>
    /// This method is used to handle a clause that specifies the assignment of a value to
    /// a variable
    /// </summary>
    private void HandleSetThingToVariableClause(Clause clause)
    {
        string name = clause.Tokens[0].Text;
        string type = clause.Tokens[2].Text;

        switch (type)
        {
            case "material":
                MaterialInstructionSet materialInstructionSet = ParseMaterialClause();
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<Material>(name, materialInstructionSet));
                break;
            case "pigment":
                PigmentInstructionSet pigmentInstructionSet = ParsePigmentClause();
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<Pigment>(name, pigmentInstructionSet));
                break;
            case "transform":
                TransformInstructionSet transformInstructionSet = ParseTransformClause();
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<Matrix>(name, transformInstructionSet));

                CurrentParser.MatchToken(
                    true, () => "Expecting a close brace here.",
                    BounderToken.CloseBrace);
                break;
        }
   }
}
