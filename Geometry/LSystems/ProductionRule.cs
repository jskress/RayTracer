using RayTracer.Extensions;

namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This class represents a production rule for an L-system.
/// </summary>
public class ProductionRule
{
    /// <summary>
    /// This property holds the variable that the rule applies to.
    /// </summary>
    public string Variable { get; set; }

    /// <summary>
    /// This property holds the break value for the rule.  It is used to govern the subset
    /// of the [0, 1) interval over which this rule applies for its variable.
    /// </summary>
    public double BreakValue { get; set; }

    /// <summary>
    /// This property holds the production that this rule represents.
    /// </summary>
    public string Production { get; set; }

    /// <summary>
    /// This method is used to validate the content of this production rule.  If no errors
    /// are found, then <c>null</c> will be returned.
    /// </summary>
    /// <returns>The text of any error or <c>null</c>.</returns>
    public string Validate()
    {
        if (string.IsNullOrEmpty(Variable) || Variable.AsRunes().Length > 1)
            return "The variable must contain exactly one character.";

        return string.IsNullOrEmpty(Production) ?
            "The production must contain at least one character."
            : null;
    }
}
