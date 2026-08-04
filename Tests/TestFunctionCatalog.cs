using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Graphics;
using RayTracer.Terms;

namespace Tests;

[TestClass]
public class TestFunctionCatalog
{
    /// <summary>
    /// This class declares functions that exist only to try the catalog's resolution rules.  A
    /// tuple satisfies both of the one-argument forms, and a vector <i>is</i> a tuple, so the pair
    /// is genuinely ambiguous -- something the DSL's own functions are written to avoid, and so
    /// cannot demonstrate.
    /// </summary>
    private static class AmbiguousFunctions
    {
        [Function("ambiguous")]
        public static double OfTuple(NumberTuple tuple) => tuple.X;

        [Function("ambiguous")]
        public static double OfVector(Vector vector) => vector.Y;
    }

    /// <summary>
    /// This tests that the catalog knows the functions that were declared to it, and only those.
    /// </summary>
    [TestMethod]
    public void TestKnownNames()
    {
        FunctionCatalog catalog = FunctionCatalog.Instance;

        Assert.IsTrue(catalog.IsKnown("sqrt"));
        Assert.IsTrue(catalog.IsKnown("dot"));
        Assert.IsFalse(catalog.IsKnown("sqroot"));

        // A second name on one method is another way to call the same function.
        Assert.IsTrue(catalog.IsKnown("length"));
        Assert.IsTrue(catalog.IsKnown("magnitude"));
        Assert.AreSame(
            catalog.SignaturesFor("length")[0].Method,
            catalog.SignaturesFor("magnitude")[0].Method);

        // Overloads are several forms under one name.  Named by shape rather than counted, so that
        // adding a form elsewhere does not fail a test that was not about it.
        Assert.HasCount(1, catalog.SignaturesFor("sqrt"));
        Assert.IsEmpty(catalog.SignaturesFor("sqroot"));

        List<string> minimums = catalog.SignaturesFor("min")
            .Select(signature => signature.ToString())
            .ToList();

        Assert.Contains("min(number, number)", minimums);
        Assert.Contains("min(vector, vector)", minimums);
        Assert.Contains("min(vector)", minimums);
    }

    /// <summary>
    /// This tests the parse-time check, which can only count the values a call supplies, since at
    /// parse time they have no types yet.
    /// </summary>
    [TestMethod]
    public void TestArityChecking()
    {
        FunctionCatalog catalog = FunctionCatalog.Instance;

        Assert.IsTrue(catalog.Accepts("sqrt", 1));
        Assert.IsFalse(catalog.Accepts("sqrt", 0));
        Assert.IsFalse(catalog.Accepts("sqrt", 2));
        Assert.IsTrue(catalog.Accepts("pow", 2));
        Assert.IsFalse(catalog.Accepts("sqroot", 1));
    }

    /// <summary>
    /// This tests that a call whose values are already of the right types resolves and runs.
    /// </summary>
    [TestMethod]
    public void TestExactMatch()
    {
        FunctionMatch match = FunctionCatalog.Instance.Match("sqrt", 16.0);

        Assert.IsTrue(match.IsMatch);
        Assert.IsNull(match.Error);
        Assert.IsTrue(4.0.Near((double) match.Invoke()));

        match = FunctionCatalog.Instance.Match("pow", 2.0, 10.0);

        Assert.IsTrue(match.IsMatch);
        Assert.IsTrue(1024.0.Near((double) match.Invoke()));

        match = FunctionCatalog.Instance.Match(
            "dot", new Vector(1, 2, 3), new Vector(4, 5, 6));

        Assert.IsTrue(match.IsMatch);
        Assert.IsTrue(32.0.Near((double) match.Invoke()));
    }

    /// <summary>
    /// This tests that the DSL's own conversions apply to a call's values, so a tuple -- which is
    /// what a scene's <c>[1, 2, 3]</c> evaluates to -- satisfies a function that wants a vector,
    /// exactly as it does everywhere else in the language.
    /// </summary>
    [TestMethod]
    public void TestArgumentsAreConverted()
    {
        FunctionMatch match = FunctionCatalog.Instance.Match(
            "length", new NumberTuple(3, 4, 0, double.NaN));

        Assert.IsTrue(match.IsMatch);
        Assert.IsInstanceOfType<Vector>(match.Arguments[0]);
        Assert.IsTrue(5.0.Near((double) match.Invoke()));
    }

    /// <summary>
    /// This tests that which form of an overloaded function a call means follows from the types of
    /// the values it supplies.
    /// </summary>
    [TestMethod]
    public void TestOverloadSelection()
    {
        FunctionMatch match = FunctionCatalog.Instance.Match("min", 3.0, 7.0);

        Assert.IsTrue(match.IsMatch);
        Assert.AreEqual(typeof(double), match.Signature.ReturnType);
        Assert.IsTrue(3.0.Near((double) match.Invoke()));

        match = FunctionCatalog.Instance.Match(
            "min", new Vector(1, 8, 3), new Vector(5, 2, 9));

        Assert.IsTrue(match.IsMatch);
        Assert.AreEqual(typeof(Vector), match.Signature.ReturnType);

        Vector smaller = (Vector) match.Invoke();

        Assert.IsTrue(smaller.Matches(new Vector(1, 2, 3)));
    }

    /// <summary>
    /// This tests that a form fitting the values as they are wins over one that would need them
    /// converted, so an overload is never taken by conversion while another already fits.
    /// </summary>
    [TestMethod]
    public void TestExactMatchWinsOverConversion()
    {
        // A point converts to a vector, so the vector form is reachable either way; the number
        // form is not, and the vector form must be chosen without the point ever being consulted
        // as anything else.
        FunctionMatch match = FunctionCatalog.Instance.Match(
            "max", new Vector(1, 5, 2), new Point(4, 3, 6));

        Assert.IsTrue(match.IsMatch);
        Assert.AreEqual(typeof(Vector), match.Signature.ReturnType);
        Assert.IsInstanceOfType<Vector>(match.Arguments[1]);
        Assert.IsTrue(((Vector) match.Invoke()).Matches(new Vector(4, 5, 6)));
    }

    /// <summary>
    /// This tests that each way a call can fail to match is reported, and reported in terms of what
    /// a scene writes rather than the classes underneath.
    /// </summary>
    [TestMethod]
    public void TestFailureMessages()
    {
        FunctionMatch match = FunctionCatalog.Instance.Match("sqroot", 4.0);

        Assert.IsFalse(match.IsMatch);
        Assert.Contains("no function named 'sqroot'", match.Error);

        match = FunctionCatalog.Instance.Match("sqrt", 4.0, 5.0);

        Assert.IsFalse(match.IsMatch);
        Assert.Contains("does not take 2 arguments", match.Error);
        Assert.Contains("it takes 1 argument", match.Error);

        match = FunctionCatalog.Instance.Match("dot", 4.0, 5.0);

        Assert.IsFalse(match.IsMatch);
        Assert.Contains("(number, number)", match.Error);
        Assert.Contains("dot(vector, vector)", match.Error);

        // A variable that resolves to nothing is a value like any other, and is named as such.
        match = FunctionCatalog.Instance.Match("sqrt", [null]);

        Assert.IsFalse(match.IsMatch);
        Assert.Contains("(null)", match.Error);
    }

    /// <summary>
    /// This tests that a call fitting more than one form equally well is reported as ambiguous
    /// rather than guessed at.
    /// </summary>
    [TestMethod]
    public void TestAmbiguousCall()
    {
        FunctionCatalog catalog = new (typeof(AmbiguousFunctions));
        FunctionMatch match = catalog.Match("ambiguous", new Vector(1, 2, 3));

        Assert.IsFalse(match.IsMatch);
        Assert.Contains("ambiguous", match.Error);
        Assert.Contains("ambiguous(tuple)", match.Error);
        Assert.Contains("ambiguous(vector)", match.Error);

        // A plain tuple fits only the tuple form, so it is not ambiguous at all.
        match = catalog.Match("ambiguous", new NumberTuple(7, 8, 9, double.NaN));

        Assert.IsTrue(match.IsMatch);
        Assert.IsTrue(7.0.Near((double) match.Invoke()));
    }

    /// <summary>
    /// This checks every function the DSL declares, since the catalog is about to grow and a
    /// malformed declaration would otherwise only show up as a puzzling failure in a scene.  Each
    /// form must produce a value, take only types the DSL can hand it, and be distinguishable from
    /// every other form of the same name.
    /// </summary>
    [TestMethod]
    public void TestEveryDeclaredFunctionIsWellFormed()
    {
        Type[] usableTypes =
        [
            typeof(double), typeof(bool), typeof(string), typeof(Vector), typeof(Point),
            typeof(Color), typeof(Matrix), typeof(NumberTuple)
        ];
        List<string> problems = [];

        foreach (string name in FunctionCatalog.Instance.Names)
        {
            HashSet<string> shapes = [];

            foreach (FunctionSignature signature in FunctionCatalog.Instance.SignaturesFor(name))
            {
                if (signature.ReturnType == typeof(void))
                    problems.Add($"{signature} produces no value.");

                if (signature.ParameterCount == 0)
                    problems.Add($"{signature} takes no arguments.");

                foreach (Type type in signature.ParameterTypes.Where(type => !usableTypes.Contains(type)))
                    problems.Add($"{signature} takes a {type.Name}, which no scene can supply.");

                string shape = signature.ToString();

                if (!shapes.Add(shape))
                    problems.Add($"{shape} is declared more than once.");
            }
        }

        Assert.IsEmpty(problems, string.Join("\n", problems));
    }
}
