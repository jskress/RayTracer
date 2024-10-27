using System.Text;
using RayTracer.Basics;
using RayTracer.Extensions;

namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This class provides the implementation of an L-system producer.
/// </summary>
public class LSystemProducer
{
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

    private readonly Dictionary<Rune, Spectrum<Rune[]>> _entries = new ();

    private Rune[] _axiom;
    private ThreadSafeRandom _random;

    /// <summary>
    /// This method is used to add a production rule to the L-system.
    /// </summary>
    /// <param name="rule">The production rule to add.</param>
    /// <returns>This object, for fluency.</returns>
    public LSystemProducer AddRule(ProductionRule rule)
    {
        string error = rule == null
            ? "A proud rule is required."
            : rule.Validate();

        if (error != null)
            throw new ArgumentException(error);

        Rune[] runes = rule.Variable.AsRunes();
        Rune key = runes[0];

        runes = rule.Production.AsRunes();

        if (!_entries.TryGetValue(key, out Spectrum<Rune[]> entry))
            _entries[key] = entry = new Spectrum<Rune[]>();

        entry.AddEntry(runes, rule.BreakValue);

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

        _random = ThreadSafeRandom.GetGenerator(Seed);

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

        foreach (Rune rune in source)
        {
            if (_entries.TryGetValue(rune, out Spectrum<Rune[]> entry))
            {
                Rune[] replacement;

                if (entry.Count > 1)
                    (_, replacement) = entry.GetByValue(_random.NextDouble());
                else
                    (_, replacement) = entry.GetByIndex(0);

                runes.AddRange(replacement);
            }
            else
                runes.Add(rune);
        }

        return runes.ToArray();
    }
}
