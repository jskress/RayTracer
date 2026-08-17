using System.Text;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Terms;

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
    /// This property holds how to turn a module's argument text into a term.  It is only ever asked
    /// for when a production actually carries arguments.
    /// </summary>
    internal Func<string, Term> Compile { get; init; }

    /// <summary>
    /// This property holds the scope the L-system was written in, which is what a rule's condition
    /// and successor arithmetic are worked out against.
    /// </summary>
    internal Variables Scope { get; init; }

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
                SymbolsToIgnore = SymbolsToIgnore,
                Formals = ruleSpec.Formals,
                Condition = ruleSpec.Condition is null ? null : Compile(ruleSpec.Condition)
            };

            _rules.Add(rule);
            // We reverse the order of the comparison so we sort longest to shortest.
            _rules.Sort((rule1, rule2) =>
                rule2.Key.Length.CompareTo(rule1.Key.Length));
        }

        rule.Productions.AddEntry(
            ModuleWord.Parse(
                ModuleWord.StripWhitespaceBetweenModules(ruleSpec.Production), Compile),
            band);

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
    /// <param name="modules">The word being rewritten.</param>
    /// <param name="letters">The word's letters on their own, which is what context matching reads.
    /// Context looks only at letters and never at numbers, so a word's parameters simply do not
    /// arise here -- which is exactly what non-parametric context means.</param>
    /// <param name="index">The index of the current module in the word.</param>
    /// <param name="random">The random number generator to use, when necessary.</param>
    /// <returns>The appropriate production.</returns>
    public Module[] GetProduction(Module[] modules, Rune[] letters, int index, Random random)
    {
        if (_rules.Count < 1)
            throw new NotSupportedException("There are no production rules in this rule set.");

        Module module = modules[index];
        Variables scope = null;
        ProductionRule rule = _rules
            .FirstOrDefault(candidate => candidate.Matches(letters, index) &&
                                         candidate.AppliesTo(module, Scope, out scope));

        // Nothing matched, so the module stands.  That is Prusinkiewicz and Lindenmayer's rule and
        // it is already how a letter with no rule at all behaves: a module whose letter has rules
        // but whose arity or condition suits none of them is simply carried through untouched.
        if (rule is null)
            return [module];

        (_, ModuleTemplate[] production) = rule.Productions.Count > 1
            ? rule.Productions.GetByValue(random.NextDouble())
            : rule.Productions.GetByIndex(0);

        return production
            .Select(template => template.Resolve(scope))
            .ToArray();
    }
}
