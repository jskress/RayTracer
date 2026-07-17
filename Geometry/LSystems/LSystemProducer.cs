using System.Text;
using RayTracer.Extensions;

namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This class provides the implementation of an L-system producer.
/// </summary>
public class LSystemProducer
{
    internal static readonly Rune LeftBracket = new ('[');
    internal static readonly Rune RightBracket = new (']');

    /// <summary>
    /// The seed used when a scene doesn't name one of its own.  It must be a fixed value, not a
    /// random one: a stochastic L-system given no seed should still grow the same tree every
    /// render, the way a scene that names nothing random should look the same twice.  A scene
    /// wanting a different tree changes the seed; it does not get a different one by accident.
    /// </summary>
    private const int DefaultSeed = 0;

    /// <summary>
    /// This property holds the axiom, or starting point, for the L-system production.
    /// </summary>
    public string Axiom
    {
        set => _axiom = value.AsRunes();
    }

    /// <summary>
    /// This property holds the seed for any randomness to use.
    /// If it is not specified, default randomness will be used where needed.
    /// </summary>
    public int? Seed { get; init; }

    /// <summary>
    /// This property holds the collection of runes that should be ignored regarding
    /// context evaluation.
    /// </summary>
    public Rune[] SymbolsToIgnore { get; init; }

    private readonly Dictionary<Rune, ProductionRuleSet> _ruleSets = new ();

    private Rune[] _axiom;
    private Random _random;

    /// <summary>
    /// This method is used to add a production rule to the L-system.
    /// </summary>
    /// <param name="ruleSpec">The production rule to add.</param>
    /// <returns>This object, for fluency.</returns>
    public LSystemProducer AddRule(ProductionRuleSpec ruleSpec)
    {
        ArgumentNullException.ThrowIfNull(ruleSpec);

        if (!_ruleSets.TryGetValue(ruleSpec.Variable, out ProductionRuleSet ruleSet))
        {
            _ruleSets[ruleSpec.Variable] = ruleSet = new ProductionRuleSet
            {
                SymbolsToIgnore = SymbolsToIgnore
            };
        }

        ruleSet.Add(ruleSpec);

        return this;
    }

    /// <summary>
    /// This method is used to produce the requested generation of the L-system
    /// </summary>
    /// <param name="generation">The number of generations to iterate over.</param>
    /// <returns>The resulting production.</returns>
    public string Produce(int generation)
    {
        if (_axiom.IsNullOrEmpty())
            throw new Exception("Axiom is required but was not provided or is of zero length.");

        // A generator private to this run, rather than a shared cached one keyed by the seed.
        // The rewriting below is sequential, so nothing here needs a thread-safe generator; and
        // a shared one would carry its position between runs, so a second L-system on the same
        // seed -- or a second render of this one in the same process -- would draw from where the
        // first left off and grow a different tree.  (This is the same trap the noise generator
        // was caught in; see NoiseGenerator.)
        _random = new Random(Seed ?? DefaultSeed);

        Rune[] runes = _axiom;

        while (generation > 0)
        {
            runes = ApplyProductions(runes);

            generation--;
        }

        return runes.AsString();
    }

    /// <summary>
    /// This method is used to apply our productions to the given source to create a single
    /// generation of the L-system production.
    /// </summary>
    /// <param name="source">The source to start with; i.e., the previous generation.</param>
    /// <returns>The result of applying our productions to the source.</returns>
    private Rune[] ApplyProductions(Rune[] source)
    {
        List<Rune> runes = [];

        for (int index = 0; index < source.Length; index++)
        {
            Rune rune = source[index];
            
            if (_ruleSets.TryGetValue(rune, out ProductionRuleSet ruleSet))
                runes.AddRange(ruleSet.GetProduction(source, index, _random));
            else
                runes.Add(rune);
        }

        return runes.ToArray();
    }
}
