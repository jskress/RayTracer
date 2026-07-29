using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Graphics;

namespace Tests;

/// <summary>
/// These tests cover the variable pool: that one name may hold a value for each type it is used
/// as, that scopes nest, and that a value of a more particular type still answers a request for
/// the type it descends from.
/// </summary>
[TestClass]
public class TestVariables
{
    [TestMethod]
    public void TestOneNameHoldsAValueForEachType()
    {
        // This is the whole point of the pool: a color and a number may share a name without
        // either standing in the other's way.
        Variables variables = new ();

        variables.SetValue("Turquoise", Colors.Turquoise);
        variables.SetValue("Turquoise", 1.61);

        Assert.IsInstanceOfType<Color>(variables.GetValue("Turquoise", typeof(Color)));
        Assert.AreEqual(1.61, (double) variables.GetValue("Turquoise", typeof(double)));
    }

    [TestMethod]
    public void TestSomethingMoreParticularAnswersForWhatItDescendsFrom()
    {
        // A point is a tuple of numbers, so a name holding a point satisfies a request for a
        // tuple.  Values are filed under the exact type they were made as -- which is what lets
        // one name hold several -- and without this that exactness hid a subclass from any
        // request for its base.  It is what left a named point unusable as a translation.
        Variables variables = new ();

        variables.SetValue("here", new Point(1, 2, 3));

        object asTuple = variables.GetValue("here", typeof(NumberTuple));

        Assert.IsNotNull(asTuple, "a point should answer a request for a tuple");
        Assert.IsInstanceOfType<Point>(asTuple);

        variables.SetValue("way", new Vector(0, 1, 0));

        Assert.IsInstanceOfType<Vector>(variables.GetValue("way", typeof(NumberTuple)),
            "a vector should answer a request for a tuple too");
    }

    [TestMethod]
    public void TestAnExactMatchIsPreferredToADescendant()
    {
        // Holding both, a request for the tuple should still get the tuple itself rather than
        // the point, since that is what was actually asked for.
        Variables variables = new ();
        NumberTuple tuple = new (9, 9, 9, 0);

        variables.SetValue("both", new Point(1, 2, 3));
        variables.SetValue("both", tuple);

        Assert.AreSame(tuple, variables.GetValue("both", typeof(NumberTuple)));
        Assert.IsInstanceOfType<Point>(variables.GetValue("both", typeof(Point)));
    }

    [TestMethod]
    public void TestWhichDescendantAnswersIsAlwaysTheSame()
    {
        // Holding a point and a vector, both of which are tuples, a request for a tuple is
        // genuinely ambiguous -- so it is settled by type name, and settled the same way every
        // time rather than by however the dictionary feels like ordering itself.
        Variables first = new ();
        Variables second = new ();

        first.SetValue("thing", new Point(1, 2, 3));
        first.SetValue("thing", new Vector(4, 5, 6));

        // The same two, put in the other way round.
        second.SetValue("thing", new Vector(4, 5, 6));
        second.SetValue("thing", new Point(1, 2, 3));

        Assert.AreEqual(
            first.GetValue("thing", typeof(NumberTuple)).GetType(),
            second.GetValue("thing", typeof(NumberTuple)).GetType(),
            "which one answers should not depend on the order they were set in");
    }

    [TestMethod]
    public void TestANameNotHeldFallsThroughToTheEnclosingScope()
    {
        Variables outer = new ();
        Variables inner = new (outer);

        outer.SetValue("radius", 2.0);

        Assert.AreEqual(2.0, (double) inner.GetValue("radius", typeof(double)));
        Assert.IsNull(inner.GetValue("missing", typeof(double)));
    }

    [TestMethod]
    public void TestAnEnclosingScopeIsSearchedForADescendantToo()
    {
        Variables outer = new ();
        Variables inner = new (outer);

        outer.SetValue("here", new Point(1, 2, 3));

        Assert.IsInstanceOfType<Point>(inner.GetValue("here", typeof(NumberTuple)));
    }
}
