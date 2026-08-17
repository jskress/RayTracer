using System.Text;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Terms;

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
    public string Axiom { get; init; }

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

    /// <summary>
    /// This property holds how to turn a module's argument text into a term.  It is only ever asked
    /// for when a word actually carries arguments.
    /// </summary>
    public Func<string, Term> Compile { get; init; }

    /// <summary>
    /// This property holds the scope the L-system was written in, which a rule's condition and its
    /// successor arithmetic are worked out against.
    /// </summary>
    public Variables Scope { get; init; }

    private readonly Dictionary<Rune, ProductionRuleSet> _ruleSets = new ();

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
                SymbolsToIgnore = SymbolsToIgnore,
                Compile = Compile,
                Scope = Scope
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
    public Module[] Produce(int generation)
    {
        if (string.IsNullOrEmpty(Axiom?.Trim()))
            throw new Exception("Axiom is required but was not provided or is of zero length.");

        // A generator private to this run, rather than a shared cached one keyed by the seed.
        // The rewriting below is sequential, so nothing here needs a thread-safe generator; and
        // a shared one would carry its position between runs, so a second L-system on the same
        // seed -- or a second render of this one in the same process -- would draw from where the
        // first left off and grow a different tree.  (This is the same trap the noise generator
        // was caught in; see NoiseGenerator.)
        _random = new Random(Seed ?? DefaultSeed);

        // The axiom is a word like any other, so it is read the same way and may carry numbers of
        // its own -- which is what lets a model start from F(1, 0) rather than from a bare letter.
        Module[] modules = ModuleWord
            .Parse(ModuleWord.StripWhitespaceBetweenModules(Axiom), Compile)
            .Select(template => template.Resolve(Scope))
            .ToArray();

        while (generation > 0)
        {
            modules = ApplyProductions(modules);

            generation--;
        }

        return modules;
    }

    /// <summary>
    /// This method is used to apply our productions to the given source to create a single
    /// generation of the L-system production.
    /// </summary>
    /// <param name="source">The source to start with; i.e., the previous generation.</param>
    /// <returns>The result of applying our productions to the source.</returns>
    private Module[] ApplyProductions(Module[] source)
    {
        List<Module> produced = [];

        // The word's letters on their own, worked out once for the whole pass rather than per
        // module.  Context matching reads only letters -- it asks what stands beside this module,
        // never what numbers it carries -- so it needs nothing else, and the whole of the existing
        // context machinery goes on working untouched.
        Rune[] letters = source
            .Select(module => module.Letter)
            .ToArray();

        for (int index = 0; index < source.Length; index++)
        {
            Module module = source[index];

            if (_ruleSets.TryGetValue(module.Letter, out ProductionRuleSet ruleSet))
                produced.AddRange(ruleSet.GetProduction(source, letters, index, _random));
            else
                produced.Add(module);
        }

        return produced.ToArray();
    }
}
