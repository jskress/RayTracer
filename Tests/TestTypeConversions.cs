using RayTracer.Basics;
using RayTracer.Graphics;
using RayTracer.Terms;

namespace Tests;

[TestClass]
public class TestTypeConversions
{
    /// <summary>
    /// The DSL hands numbers around as doubles, so this is the shape every number literal
    /// arrives in.
    /// </summary>
    private static object Coerce<TTarget>(object value)
    {
        (CoercionResult result, object coerced) = CoerceWithResult<TTarget>(value);

        Assert.AreEqual(CoercionResult.OfProperType, result,
            $"could not coerce {value ?? "null"} to {typeof(TTarget).Name}");

        return coerced;
    }

    private static (CoercionResult, object) CoerceWithResult<TTarget>(object value)
    {
        // TypeConversions is internal, so reach it the way the Terms layer does.
        return (ValueTuple<CoercionResult, object>) typeof(Term).Assembly
            .GetType("RayTracer.Terms.TypeConversions")!
            .GetMethod("Coerce", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [value, new[] { typeof(TTarget) }])!;
    }

    [TestMethod]
    public void TestANumberBecomesANullableInt()
    {
        // Every "seed" in the DSL runs through here, and none of them worked: a Nullable<int>
        // matches none of the exact-type tests further down, so it fell past all of them to the
        // failure at the end -- "Could not convert 7 to any of the types, Nullable`1".  Nothing
        // in the gallery names a seed, which is how that went unnoticed.
        Assert.AreEqual(7, Coerce<int?>(7.0));
    }

    [TestMethod]
    public void TestNullIsFineForANullableButNotForItsUnderlyingType()
    {
        // The one value a nullable can take that its underlying type cannot.  This is why the
        // nullable has to be unwrapped ahead of the null guard: Nullable<int> reports itself as
        // a value type, so that guard would otherwise reject exactly this.
        (CoercionResult nullableResult, object nullableValue) = CoerceWithResult<int?>(null);

        Assert.AreEqual(CoercionResult.OfProperType, nullableResult);
        Assert.IsNull(nullableValue);

        (CoercionResult intResult, _) = CoerceWithResult<int>(null);

        Assert.AreEqual(CoercionResult.CouldNotCoerce, intResult);
    }

    [TestMethod]
    public void TestANullableTakesTheSameConversionsItsUnderlyingTypeDoes()
    {
        // A nullable should be no harder to satisfy than what it wraps, so whatever reaches the
        // underlying type must reach it too -- including a rounded double.
        Assert.AreEqual(7, Coerce<int?>(7.0));
        Assert.AreEqual(7, Coerce<int>(7.0));
        Assert.AreEqual((short) 7, Coerce<short?>(7.0));
        Assert.AreEqual(true, Coerce<bool?>(true));
    }

    [TestMethod]
    public void TestNonNullableConversionsStillWork()
    {
        // A guard that unwrapping nullables first didn't disturb anything already working.
        Assert.AreEqual(7, Coerce<int>(7.0));
        Assert.AreEqual("7", Coerce<string>(7.0));
        Assert.IsInstanceOfType<Point>(Coerce<Point>(new NumberTuple(1, 2, 3, double.NaN)));
        Assert.IsInstanceOfType<Vector>(Coerce<Vector>(new NumberTuple(1, 2, 3, double.NaN)));
        Assert.IsInstanceOfType<Color>(Coerce<Color>(new NumberTuple(1, 2, 3, double.NaN)));
    }
}
