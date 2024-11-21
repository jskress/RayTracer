using System.Text;

namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This class represents the base class for our production rule support.
/// </summary>
public abstract class ProductionRuleBase
{
    /// <summary>
    /// This property holds the key by which this rule is to be known.
    /// This is the original string from the variable and left and right contexts were
    /// parsed.
    /// </summary>
    public string Key { get; init; }

    /// <summary>
    /// This property holds the variable that the rule applies to.
    /// </summary>
    public Rune Variable { get; init; }

    /// <summary>
    /// This property holds the left context, if any, for this rule.
    /// </summary>
    public ProductionBranch LeftContext { get; init; }

    /// <summary>
    /// This property holds the right context, if any, for this rule.
    /// </summary>
    public ProductionBranch RightContext { get; init; }

    /// <summary>
    /// This method provides a string representation of this production rule.
    /// </summary>
    /// <returns>This rule, as a string.</returns>
    public override string ToString()
    {
        string left = LeftContext == null ? "" : $"{LeftContext}<";
        string right = RightContext == null ? "" : $">{RightContext}";
        
        return $"{Key}: {left}{Variable}{right}";
    }
}
