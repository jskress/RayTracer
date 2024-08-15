using Lex.Clauses;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Instructions;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to parse a clause of zero one turbulence.
    /// </summary>
    private TurbulenceInstructionSet ParseTurbulenceClause()
    {
        Clause clause = LanguageDsl.ParseClause(CurrentParser, "turbulenceClause");

        if (clause == null)
            return null;

        TurbulenceInstructionSet instructionSet = new ();
        bool phased = clause.Tokens.Count > 1;

        instructionSet.AddInstruction(new SetObjectPropertyInstruction<Turbulence, int>(
            target => target.Depth, clause.Term()));
        instructionSet.AddInstruction(new SetObjectPropertyInstruction<Turbulence, bool>(
            target => target.Phased, phased));

        if (phased)
        {
            instructionSet.AddInstruction(new SetObjectPropertyInstruction<Turbulence, int>(
                target => target.Tightness, clause.Term(1)));

            if (clause.Tokens.Count > 2)
            {
                instructionSet.AddInstruction(new SetObjectPropertyInstruction<Turbulence, double>(
                    target => target.Scale, clause.Term(2)));
            }
        }

        return instructionSet;
    }
}
