using RayTracer.Extensions;
using RayTracer.Geometry.LSystems;

namespace Tests;

[TestClass]
public class TestLSystemProductions
{
    private static ProductionRuleSpec NewRule(string key, string production, double breakValue = 0)
    {
        int p1 = key.IndexOf('<');
        int p2 = key.IndexOf('>') + 1;
        int vs = p1 + 1;
        int ve = vs + 1;

        return new ProductionRuleSpec
        {
            Key = key,
            Variable = key[vs..ve].AsRunes()[0],
            BreakValue = breakValue,
            LeftContext = p1 < 0 ? null : ProductionBranch.Parse(key[..p1].AsRunes()),
            RightContext = p2 < 1 ? null : ProductionBranch.Parse(key[p2..].AsRunes()),
            Production = production
        };
    }

    [TestMethod]
    public void TestDeterministicProductions()
    {
        ProductionRuleSpec spec = NewRule("F", "F+F");
        LSystemProducer producer = new LSystemProducer { Axiom = "F" }
            .AddRule(spec);

        Verify(producer, "F", "F+F", "F+F+F+F");
    }

    private static LSystemProducer CoinFlip(int? seed) =>
        new LSystemProducer { Axiom = "AAAAAAAA", Seed = seed }
            .AddRule(NewRule("A", "C", 0.5))
            .AddRule(NewRule("A", "D", 0.5));

    [TestMethod]
    public void TestStochasticProductionsAreReproducibleForASeed()
    {
        // Building the same stochastic system twice with the same seed grows the same string --
        // the property a render relies on.  Produce is a pure function of its inputs: it does not
        // draw from a sequence that advances between calls, so a single producer asked the same
        // thing twice answers the same both times.  (It used to draw from a shared, advancing
        // generator, which is what made repeated renders of one tree differ.)
        for (int generation = 0; generation <= 4; generation++)
            Assert.AreEqual(CoinFlip(7).Produce(generation), CoinFlip(7).Produce(generation));

        LSystemProducer producer = CoinFlip(7);

        Assert.AreEqual(producer.Produce(3), producer.Produce(3));
    }

    [TestMethod]
    public void TestStochasticProductionsVaryWithTheSeed()
    {
        // Eight independent coin-flips, so two different seeds should disagree somewhere -- the
        // choice really is being steered by the seed, not fixed.
        Assert.AreNotEqual(CoinFlip(1).Produce(1), CoinFlip(2).Produce(1));
    }

    [TestMethod]
    public void TestAnUnseededStochasticSystemIsStillReproducible()
    {
        // The bug this fixes: with no seed named, the producer drew from a shared random source
        // and grew a different tree every render.  It falls back to a fixed default seed now, so
        // two unseeded producers agree -- a scene wanting a different tree names a seed rather
        // than getting one by chance.
        Assert.AreEqual(CoinFlip(null).Produce(1), CoinFlip(null).Produce(1));
    }

    [TestMethod]
    public void TestStochasticRulesForOneVariableInDifferentContextsEachSumToOne()
    {
        // Two stochastic groups for F, told apart by context, each summing to 1 on its own.  The
        // probability bands are kept per rule, so building this must not run the two totals
        // together and wrongly complain that F's productions pass 100%.
        LSystemProducer producer = new LSystemProducer { Axiom = "AFBF", Seed = 1 }
            .AddRule(NewRule("A<F", "x", 0.5))
            .AddRule(NewRule("A<F", "y", 0.5))
            .AddRule(NewRule("B<F", "p", 0.5))
            .AddRule(NewRule("B<F", "q", 0.5));

        // The F after A resolves within its own group (x or y), the F after B within its (p or q).
        string result = producer.Produce(1);

        Assert.IsTrue(result is "AxBp" or "AxBq" or "AyBp" or "AyBq", result);
    }

    [TestMethod]
    public void TestContextSensitiveProductions()
    {
        // A plain left-and-right context rule.  With no B before and C after, the A stays put.
        ProductionRuleSpec spec = NewRule("B<A>C", "AA");

        Verify(new LSystemProducer { Axiom = "A" }.AddRule(spec), "A", "A", "A");
        Verify(new LSystemProducer { Axiom = "ABC" }.AddRule(spec), "ABC", "ABC", "ABC");

        // ...but B before and C after, and it doubles.  The A in BAC becomes AA (BAAC); neither of
        // those two A's then has both a B before and a C after, so it settles there.
        Verify(new LSystemProducer { Axiom = "BAC" }.AddRule(spec), "BAC", "BAAC", "BAAC");

        // Context that reaches across a branch: the S is preceded by BC and followed by G, then a
        // branch [H], then M, and only there does it become T.
        Verify(new LSystemProducer { Axiom = "ABC[DE][SG[HI[JK]L]MNO]" }
                .AddRule(NewRule("BC<S>G[H]M", "T")),
            "ABC[DE][SG[HI[JK]L]MNO]",
            "ABC[DE][TG[HI[JK]L]MNO]",
            "ABC[DE][TG[HI[JK]L]MNO]");

        // The anabaena catenula example from The Algorithmic Beauty of Plants (figure 1.31), which
        // needs ignored symbols so the signals pass through the F's and turn markers as if they
        // were not there.
        LSystemProducer producer = new LSystemProducer
            {
                Axiom = "F1F1F1",
                SymbolsToIgnore = "+-F".AsRunes()
            }
            .AddRule(NewRule("0<0>0", "0"))
            .AddRule(NewRule("0<0>1", "1[+F1F1]"))
            .AddRule(NewRule("0<1>0", "1"))
            .AddRule(NewRule("0<1>1", "1"))
            .AddRule(NewRule("1<0>0", "0"))
            .AddRule(NewRule("1<0>1", "1F1"))
            .AddRule(NewRule("1<1>0", "0"))
            .AddRule(NewRule("1<1>1", "0"))
            .AddRule(NewRule("+", "-"))
            .AddRule(NewRule("-", "+"));

        Verify(producer, "F1F1F1", "F1F0F1", "F1F1F1F1", "F1F0F0F1",
            "F1F0F1[+F1F1]F1", "F1F1F1F1[-F0F1]F1", "F1F0F0F0[+F1F1F1]F1");
    }

    private static void Verify(LSystemProducer producer, params string[] expected)
    {
        for (int generation = 0; generation < expected.Length; generation++)
            Assert.AreEqual(expected[generation], producer.Produce(generation), $"Generation {generation}");
    }
}
