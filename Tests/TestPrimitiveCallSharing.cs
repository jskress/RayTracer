using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;
using RayTracer.Instructions.Surfaces;
using RayTracer.Pigments;
using RayTracer.Terms;

namespace Tests;

/// <summary>
/// These tests cover the key that decides whether two calls of one primitive get one shape between
/// them or a shape each.
/// <para>
/// **None of this can be seen in a picture, and that is the whole difficulty.**  Sharing that changed
/// what was drawn would be a fault in itself, so every rendered image comes out the same either way
/// and the only way to know whether a call shared is to look at the key it made.  A material fell
/// through this and refused a key outright, which meant that handing a primitive its paint -- the
/// ordinary way to use one -- silently gave up the sharing and cost 1.21s against 0.66s over
/// thirty-two calls of a single shape.  Nothing said so.
/// </para>
/// </summary>
[TestClass]
public class TestPrimitiveCallSharing
{
    private static UserPrimitive Primitive(string name = "Thing")
    {
        return new UserPrimitive(name, [], [], "group", new FunctionBody());
    }

    private static Material Painted(double red, double green, double blue)
    {
        return new Material { Pigment = new SolidPigment(new Color(red, green, blue)) };
    }

    [TestMethod]
    public void TestTwoCallsGivenTheSameMaterialShare()
    {
        UserPrimitive primitive = Primitive();
        Material coat = Painted(0.9, 0.1, 0.1);

        Assert.IsNotNull(PrimitiveCallResolver.KeyFor(primitive, [coat]),
            "a call given a material could not be written down at all, so it never shared");
        Assert.AreEqual(
            PrimitiveCallResolver.KeyFor(primitive, [coat]),
            PrimitiveCallResolver.KeyFor(primitive, [coat]),
            "two calls handed the very same material did not come out alike");
    }

    /// <summary>
    /// A material is keyed by which one it is, not by what it looks like, so two that match in every
    /// property still count as two.  That errs the safe way: a share refused costs a rebuild, where a
    /// share granted wrongly paints a piece the wrong color.
    /// </summary>
    [TestMethod]
    public void TestTwoCallsGivenDifferentMaterialsDoNotShare()
    {
        UserPrimitive primitive = Primitive();

        Assert.AreNotEqual(
            PrimitiveCallResolver.KeyFor(primitive, [Painted(0.9, 0.1, 0.1)]),
            PrimitiveCallResolver.KeyFor(primitive, [Painted(0.1, 0.9, 0.1)]),
            "two calls given different materials came out alike, so one would paint the other");
        Assert.AreNotEqual(
            PrimitiveCallResolver.KeyFor(primitive, [Painted(0.9, 0.1, 0.1)]),
            PrimitiveCallResolver.KeyFor(primitive, [Painted(0.9, 0.1, 0.1)]),
            "two materials identical in every property were treated as one");
    }

    /// <summary>
    /// And a material has to be told apart from the other things a call may be given, rather than
    /// merely written down as something.
    /// </summary>
    [TestMethod]
    public void TestAMaterialIsNotConfusedWithWhatElseACallMayBeGiven()
    {
        UserPrimitive primitive = Primitive();
        Material coat = Painted(0.5, 0.5, 0.5);
        string[] keys =
        [
            PrimitiveCallResolver.KeyFor(primitive, [coat]),
            PrimitiveCallResolver.KeyFor(primitive, [2.0]),
            PrimitiveCallResolver.KeyFor(primitive, ["a name"]),
            PrimitiveCallResolver.KeyFor(primitive, [true]),
            PrimitiveCallResolver.KeyFor(primitive, [null])
        ];

        Assert.AreEqual(keys.Length, keys.Distinct().Count(),
            "two calls given different kinds of thing came out with the same key");
    }

    /// <summary>
    /// Two different primitives never share, however alike what they were given.
    /// </summary>
    [TestMethod]
    public void TestTwoDifferentPrimitivesNeverShare()
    {
        Material coat = Painted(0.3, 0.4, 0.5);

        Assert.AreNotEqual(
            PrimitiveCallResolver.KeyFor(Primitive("Ball"), [coat]),
            PrimitiveCallResolver.KeyFor(Primitive("Box"), [coat]),
            "two different primitives given the same material came out alike");
    }
}
