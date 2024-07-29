using System.Text.RegularExpressions;
using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class represents the unary string substitution operation.
/// </summary>
public class StringSubstitutionOperation : UnaryOperation<string>
{
    private static readonly Regex VariablePattern = new Regex(@"\${(.*)}");

    public StringSubstitutionOperation(Term term) : base(term) {}

    /// <summary>
    /// This method is used to perform any variable substitution required in the given
    /// string.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="value">The value to operate on.</param>
    /// <returns>The updated value.</returns>
    protected override string Apply(Variables variables, string value)
    {
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
