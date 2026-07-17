using System.Text;
using RayTracer.Extensions;

namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This class represents the collection of production rules that apply to a specific
/// variable.
/// </summary>
public class ProductionRuleSet
{
    private readonly List<ProductionRule> _rules = [];

    // The running probability total per rule, so a rule's stochastic productions can be laid out
    // end to end across the [0, 1) interval as they are added.  This has to be keyed by the whole
    // rule key, not just the variable: one variable may have several rules told apart by context,
    // each its own independent set of stochastic productions summing to 1 on its own, and keying
    // by the variable alone would run their totals together and push the later ones past 1.
    private readonly Dictionary<string, double> _bands = new ();

    /// <summary>
    /// This property holds the collection of runes that should be ignored regarding
    /// context evaluation.
    /// </summary>
    internal Rune[] SymbolsToIgnore { get; init; }

    /// <summary>
    /// This method is used to add a new rule based on the given rule specification.
    /// </summary>
    /// <param name="ruleSpec">The specification to base the new rule on.</param>
    public void Add(ProductionRuleSpec ruleSpec)
    {
        string key = ruleSpec.Key.RemoveAllWhitespace();
        ProductionRule rule = _rules
            .FirstOrDefault(r => r.Key == key);
        double band = _bands.GetValueOrDefault(key);

        // Our first rule for the key.
        if (rule == null)
        {
            rule = new ProductionRule
            {
                Key = key,
                Variable = ruleSpec.Variable,
                LeftContext = ruleSpec.LeftContext,
                RightContext = ruleSpec.RightContext,
                SymbolsToIgnore = SymbolsToIgnore
            };

            _rules.Add(rule);
            // We reverse the order of the comparison so we sort longest to shortest.
            _rules.Sort((rule1, rule2) =>
                rule2.Key.Length.CompareTo(rule1.Key.Length));
        }

        rule.Productions.AddEntry(ruleSpec.Production.RemoveAllWhitespace().AsRunes(), band);

        band += ruleSpec.BreakValue;

        if (band > 1)
            throw new Exception($"Probabilities for the {ruleSpec.Key} productions are larger than 100%.");

        _bands[key] = band;
    }

    /// <summary>
    /// This method is used to locate the particular rule
    /// </summary>
    /// <param name="source">The source set of runes.</param>
    /// <param name="index">The index of the current rune in the source.</param>
    /// <param name="random">The random number generator to use, when necessary.</param>
    /// <returns>The appropriate production.</returns>
    public Rune[] GetProduction(Rune[] source, int index, Random random)
    {
        ProductionRule rule = _rules.Count switch
        {
            < 1 => throw new NotSupportedException("There are no production rules in this rule set."),
            _ => _rules
                .FirstOrDefault(r => r.Matches(source, index))
        };

        (_, Rune[] production) = rule is null
            ? (double.NaN, [source[index]])
            : rule.Productions.Count > 1
                ? rule.Productions.GetByValue(random.NextDouble())
                : rule.Productions.GetByIndex(0);

        return production; 
    }
}
