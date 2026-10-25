using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;
using RayTracer.Instructions.Transforms;

namespace Tests;

/// <summary>
/// These tests hold the copies parsing makes of a named thing to being copies.  A name used with additions of its
/// own -- `material Glass { interior { ior 1.33 } }`, `joint Arm { angle 30 }` -- is cloned and the additions
/// parsed into the clone, so whatever the clone shares with the original is written into every use of the name.
/// </summary>
[TestClass]
public class TestResolverCopies
{
    private static LiteralResolver<T> Literal<T>(T value) => new () { Value = value };

    /// <summary>
    /// A copy of a material has an interior of its own: an index given to the copy's interior is not the original's.
    /// </summary>
    [TestMethod]
    public void TestACopiedMaterialHasItsOwnInterior()
    {
        MaterialResolver glass = new ()
        {
            InteriorResolver = new InteriorResolver { IndexOfRefractionResolver = Literal(1.5) }
        };
        MaterialResolver copy = (MaterialResolver) glass.Clone();

        copy.InteriorResolver.IndexOfRefractionResolver = Literal(1.33);

        Assert.AreEqual(1.5, glass.InteriorResolver.IndexOfRefractionResolver.Resolve(new RenderContext(), new Variables()));
    }

    /// <summary>
    /// A copy of a joint turns its own way: an angle given to the copy is not the original's, and the copy's
    /// transforms hold the copy's turn, not the original's.
    /// </summary>
    [TestMethod]
    public void TestACopiedJointHasItsOwnTurn()
    {
        JointResolver arm = new ();

        arm.Turn.AxisResolver = Literal(new Vector(0, 0, 1));
        arm.Turn.AngleResolver = Literal(0.2);
        arm.TransformResolver = new TransformResolver();
        arm.TransformResolver.TransformCreators.Add(arm.Turn);

        JointResolver copy = (JointResolver) arm.Clone();

        copy.Turn.AngleResolver = Literal(0.7);

        Assert.AreEqual(0.2, arm.Turn.AngleResolver.Resolve(new RenderContext(), new Variables()), "the original's angle");
        Assert.AreSame(copy.Turn, copy.TransformResolver.TransformCreators[0], "the copy turns by its own turn");
        Assert.AreSame(arm.Turn, arm.TransformResolver.TransformCreators[0], "the original keeps its own");
    }

    /// <summary>
    /// A joint's own properties are set only where they were said, so a block that only moves a joint -- the block
    /// on a call of a primitive that gives one back -- leaves how it turns alone.
    /// </summary>
    [TestMethod]
    public void TestABlockThatOnlyMovesAJointKeepsItsTurn()
    {
        JointResolver made = new ();

        made.Turn.PivotResolver = Literal(new Point(1, 2, 3));
        made.Turn.AxisResolver = Literal(new Vector(0, 1, 0));
        made.Turn.AngleResolver = Literal(0.5);

        Joint joint = (Joint) made.Resolve(new RenderContext(), new Variables());

        new JointResolver().ApplyTo(new RenderContext(), new Variables(), joint);

        Assert.AreEqual(new Point(1, 2, 3), joint.Pivot);
        Assert.AreEqual(0.5, joint.Angle, 1e-12);
        Assert.IsNotNull(joint.Axis);
    }
}
