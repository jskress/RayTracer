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

    [TestMethod]
    public void TestStochasticProductions()
    {
        ProductionRuleSpec spec1 = NewRule("A", "C", 0.33);
        ProductionRuleSpec spec2 = NewRule("A", "C", 0.33);
        ProductionRuleSpec spec3 = NewRule("A", "D", 0.34);
        LSystemProducer producer = new LSystemProducer { Axiom = "A", Seed = 7 }
            .AddRule(spec1)
            .AddRule(spec2)
            .AddRule(spec3);

        Verify(producer, "A", "C", "D");
        Verify(producer, "A", "D", "C");
        Verify(producer, "A", "C", "D");
    }

    [TestMethod]
    public void TestContextSensitiveProductions()
    {
        ProductionRuleSpec spec = NewRule("B<A>C", "AA");
        LSystemProducer producer = new LSystemProducer { Axiom = "A" }
            .AddRule(spec);

        /*
        Verify(producer, "A", "A", "A");

        producer.Axiom = "ABC";

        Verify(producer, "ABC", "ABC", "ABC");

        producer.Axiom = "BAC";

        Verify(producer, "BAC", "BAAC", "BAAC");

        spec = NewRule("BC<S>G[H]M", "T");
        producer = new LSystemProducer { Axiom = "ABC[DE][SG[HI[JK]L]MNO]" }
            .AddRule(spec);

        Verify(producer, 
            "ABC[DE][SG[HI[JK]L]MNO]",
            "ABC[DE][TG[HI[JK]L]MNO]", "ABC[DE][TG[HI[JK]L]MNO]");
        */

        producer = new LSystemProducer
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
            "F1F0F1[+F1F1]F1", "F1F1F1F1[-F1F1]F1", "F1F0F0F0[+F1F1]F1");
    }

    private static void Verify(LSystemProducer producer, params string[] expected)
    {
        for (int generation = 0; generation < expected.Length; generation++)
            Assert.AreEqual(expected[generation], producer.Produce(generation));
    }
}
