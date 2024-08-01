using System.Text.RegularExpressions;
using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class represents the unary string substitution operation.
/// </summary>
public class StringSubstitutionOperation : UnaryOperation
{
    private static readonly Regex VariablePattern = new Regex(@"\${(.*)}");

    public StringSubstitutionOperation(Term term) : base(term) {}

    /// <summary>
    /// This method is used to evaluate this term to inject variable values into a string. 
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected override object Evaluate(Variables variables, params Type[] targetTypes)
    {
        string value = (string) Operand.GetValue(variables, typeof(string));

        // Don't bother doing anything if the string doesn't have variable references.
        if (!string.IsNullOrEmpty(value))
        {
            value = VariablePattern.Replace(value, evaluator =>
            {
                string result = evaluator.Value;
                string name = evaluator.Groups[1].Value;

                if (variables.ContainsKey(name))
                {
                    object variableValue = variables.GetValue(name);

                    result = variableValue?.ToString() ?? "<null>";
                }

                return result;
            });
        }

        return value;
    }
}
