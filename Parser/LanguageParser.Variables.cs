using Lex.Clauses;
using Lex.Parser;
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
        ICopyableInstructionSet instructionSet = null;

        switch (type)
        {
            case "pigment":
                PigmentInstructionSet pigmentInstructionSet = ParsePigmentClause();
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<Pigment>(name, pigmentInstructionSet));
                break;
            case "material":
                MaterialInstructionSet materialInstructionSet = DetermineProperInstructionSet(
                    ParseMaterialClause, set => ParseMaterialClause(set), true);
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<Material>(name, materialInstructionSet));
                instructionSet = materialInstructionSet;
                break;
            case "transform":
                TransformInstructionSet transformInstructionSet = DetermineProperInstructionSet(
                        () => ParseTransformClause(),
                        set => ParseTransformClause(set), false);
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<Matrix>(name, transformInstructionSet));

                CurrentParser.MatchToken(
                    true, () => "Expecting a close brace here.",
                    BounderToken.CloseBrace);

                instructionSet = transformInstructionSet;
                break;
        }

        if (instructionSet != null)
            _context.ExtensibleItems[name] = instructionSet;
    }

    /// <summary>
    /// This method is used to determine the proper instruction set to use as the assigned
    /// value of a variable.
    /// </summary>
    /// <returns>The instruction set to use.</returns>
    private TSet DetermineProperInstructionSet<TSet>(
        Func<TSet> creator, Action<TSet> parser, bool consumesBrace)
        where TSet : ICopyableInstructionSet
    {
        Clause clause = LanguageDsl.ParseClause(CurrentParser, "startThingClause");

        // We are not setting a variable as an extension of another one.
        if (BounderToken.OpenBrace.Matches(clause.Tokens[0]))
        {
            TSet set = creator();

            return set;
        }

        return DetermineProperInstructionSet(clause, parser, consumesBrace);
    }

    /// <summary>
    /// This method is used to determine the proper instruction set to use as the assigned
    /// value of a variable.
    /// </summary>
    /// <returns>The instruction set to use.</returns>
    private TSet DetermineProperInstructionSet<TSet>(
        Clause clause, Action<TSet> parser, bool consumesBrace)
        where TSet : ICopyableInstructionSet
    {
        string baseName = clause.Tokens[0].Text;

        if (!_context.ExtensibleItems.TryGetValue(baseName, out ICopyableInstructionSet set) ||
            set is not TSet instructionSet)
        {
            throw new TokenException(
                $"The variable name, {baseName}, is not defined or does not refer to a material.")
            {
                Token = clause.Tokens[0]
            };
        }

        instructionSet = (TSet) instructionSet.Copy();

        parser(instructionSet);

        if (!consumesBrace)
        {
            CurrentParser.MatchToken(
                true, () => "Expecting a close brace here.",
                BounderToken.CloseBrace);
        }

        return instructionSet;
    }
}
