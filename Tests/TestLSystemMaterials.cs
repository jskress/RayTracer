using System.Text;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Geometry.LSystems;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// These tests cover giving an L-system more than one material: by binding a character in the
/// production, and by branching depth.  The thread running through them is that a material is
/// state rather than a stamp -- it holds until something changes it, and a branch gives it back
/// when it closes -- which is what sets it apart from the surface a <c>~</c> puts down.
/// </summary>
[TestClass]
public class TestLSystemMaterials
{
    private static Material MaterialOf(string name) => new ()
    {
        Pigment = new SolidPigment(name switch
        {
            "bark" => new Color(0.3, 0.2, 0.1),
            "leaf" => new Color(0.2, 0.6, 0.2),
            _ => new Color(0.9, 0.6, 0.7)
        })
    };

    /// <summary>
    /// Builds an L-system from an axiom alone -- generation 0, so the axiom is the production
    /// verbatim -- and prepares it, so the geometry the walk produced can be inspected.
    /// </summary>
    private static LSystem Prepare(
        string axiom,
        (char Character, Material Material)[] bindings = null,
        Material[] depthMaterials = null,
        Material own = null)
    {
        LSystem lsystem = new () { Axiom = axiom, Generations = 0, Material = own };

        foreach ((char character, Material material) in bindings ?? [])
            lsystem.MaterialBindings[new Rune(character)] = material;

        if (depthMaterials is not null)
            lsystem.DepthMaterials.AddRange(depthMaterials);

        lsystem.PrepareForRendering();

        return lsystem;
    }

    /// <summary>
    /// Collects the materials of the stem geometry, which for the pipes renderer is every sphere
    /// and cylinder it laid down.
    /// </summary>
    private static List<Material> StemMaterials(LSystem lsystem)
    {
        return lsystem.Surfaces
            .Where(surface => surface is Sphere or Cylinder)
            .Select(surface => surface.Material)
            .ToList();
    }

    [TestMethod]
    public void TestAPlantWithNoMaterialsNamedIsUnchanged()
    {
        // The compatibility guarantee, stated as a test: with nothing bound, every piece of stem
        // is left without a material of its own, which is what lets it inherit the L-system's --
        // exactly as it did before any of this existed.
        LSystem lsystem = Prepare("FF");

        Assert.IsTrue(StemMaterials(lsystem).All(material => material is null));
    }

    [TestMethod]
    public void TestABoundCharacterColoursWhatFollowsIt()
    {
        // The heart of it.  'B' draws nothing itself; it changes what the segments after it are
        // drawn with, and leaves the ones before it alone.
        Material blossom = MaterialOf("blossom");
        LSystem lsystem = Prepare("FBF", [('B', blossom)]);
        List<Material> materials = StemMaterials(lsystem);

        Assert.IsNull(materials[0], "the opening sphere should predate the binding");
        Assert.IsTrue(materials.Any(material => ReferenceEquals(material, blossom)),
            "nothing was drawn with the bound material");
    }

    [TestMethod]
    public void TestAMaterialNamedAfterASegmentDoesNotColourIt()
    {
        // The mistake that is easy to make, and worth pinning down because the plant simply comes
        // out the wrong colour rather than failing: a material rune colours what follows it, so one
        // written after the segment it was meant for is too late.
        Material blossom = MaterialOf("blossom");
        LSystem lsystem = Prepare("FB", [('B', blossom)]);

        Assert.IsFalse(StemMaterials(lsystem).Any(material => ReferenceEquals(material, blossom)));
    }

    [TestMethod]
    public void TestAMaterialSetInsideABranchDoesNotEscapeIt()
    {
        // What makes this usable at all: colouring one limb must not colour its neighbours.  The
        // branch inherits what was in force where it forked and hands it back when it closes, so
        // the segment after the bracket is drawn with what came before it.
        Material blossom = MaterialOf("blossom");
        LSystem lsystem = Prepare("F[BF]F", [('B', blossom)]);
        List<Material> materials = StemMaterials(lsystem);

        Assert.IsTrue(materials.Any(material => ReferenceEquals(material, blossom)),
            "the branch never took the material");
        Assert.IsNull(materials[^1], "the material leaked out of the branch it was set in");
    }

    [TestMethod]
    public void TestAMaterialSetBeforeABranchIsInheritedByIt()
    {
        // The other half of the same rule: a branch starts out drawing with whatever the turtle
        // was drawing with when it forked.
        Material blossom = MaterialOf("blossom");
        LSystem lsystem = Prepare("BF[F]", [('B', blossom)]);

        Assert.IsTrue(StemMaterials(lsystem)
            .Skip(1)
            .All(material => ReferenceEquals(material, blossom)));
    }

    [TestMethod]
    public void TestDepthMaterialsColourByHowFarBranchedTheTurtleIs()
    {
        // The trunk draws with the first, a branch off it with the second.  This is the thing that
        // would otherwise need a production rule written per level.
        Material bark = MaterialOf("bark");
        Material leaf = MaterialOf("leaf");
        LSystem lsystem = Prepare("F[F]", depthMaterials: [bark, leaf]);
        List<Material> materials = StemMaterials(lsystem);

        Assert.IsTrue(materials.Any(material => ReferenceEquals(material, bark)));
        Assert.IsTrue(materials.Any(material => ReferenceEquals(material, leaf)));
    }

    [TestMethod]
    public void TestDepthBeyondTheListKeepsTheLastMaterial()
    {
        // A plant may branch deeper than a scene bothered to name materials for, and when it does
        // it goes on drawing with the last one rather than falling back to nothing.
        Material bark = MaterialOf("bark");
        Material leaf = MaterialOf("leaf");
        LSystem lsystem = Prepare("F[F[F[F]]]", depthMaterials: [bark, leaf]);

        Assert.IsFalse(StemMaterials(lsystem).Skip(1).Any(material => material is null),
            "a branch deeper than the list was left with no material");
    }

    [TestMethod]
    public void TestAGapInTheDepthsCarriesOnWithTheOneAbove()
    {
        // A scene may name depth 0 and depth 2 without bothering with the one between, and the
        // level it skipped should go on drawing with what its parent drew with rather than falling
        // through to nothing.
        Material bark = MaterialOf("bark");
        Material leaf = MaterialOf("leaf");
        LSystem lsystem = Prepare("F[F[F]]", depthMaterials: [bark, bark, leaf]);

        Assert.IsFalse(StemMaterials(lsystem).Any(material => material is null));
    }

    [TestMethod]
    public void TestABoundCharacterOutranksTheDepthItStandsAt()
    {
        // Naming a material outright is the more deliberate instruction of the two, so it wins
        // wherever both would apply.
        Material bark = MaterialOf("bark");
        Material blossom = MaterialOf("blossom");
        LSystem lsystem = Prepare("BF", [('B', blossom)], [bark]);

        Assert.IsTrue(StemMaterials(lsystem)
            .Skip(1)
            .All(material => ReferenceEquals(material, blossom)));
    }

    [TestMethod]
    public void TestABoundCharacterIsNotAlsoReadAsACommand()
    {
        // Binding a character has the last word over whatever else it might have meant, so that a
        // scene is never told it may not use some letter because the turtle had claimed it.  'F'
        // bound to a material stops drawing and starts colouring.
        Material blossom = MaterialOf("blossom");
        LSystem lsystem = Prepare("FF", [('F', blossom)]);

        // Only the opening sphere the pipes renderer lays down at the origin survives; the two
        // 'F's drew nothing, having been spent on the binding instead.
        Assert.AreEqual(1, StemMaterials(lsystem).Count);
    }

    [TestMethod]
    public void TestATracedPolygonTakesTheTurtlesMaterial()
    {
        // A blade traced as a polygon is drawn by the turtle rather than stamped, so it takes what
        // the turtle is drawing with -- which is what lets a production give a young shoot's blade
        // a different green from an old one's.
        Material leaf = MaterialOf("leaf");
        LSystem lsystem = Prepare("B{f+f+f}", [('B', leaf)]);
        List<Material> blades = lsystem.Surfaces
            .Where(surface => surface is Triangle)
            .Select(surface => surface.Material)
            .ToList();

        Assert.IsTrue(blades.Count > 0, "no polygon was filled");
        Assert.IsTrue(blades.All(material => ReferenceEquals(material, leaf)));
    }

    [TestMethod]
    public void TestTheDefaultLeafTakesTheTurtlesMaterial()
    {
        // The default leaf is green so that a bare, unstyled tree already reads as leaves on
        // branches.  That green is a fallback, though, not a choice the scene made, so naming a
        // material has to be able to override it -- otherwise a production cannot give the leaves
        // on a young shoot a different green from the ones on old wood.
        Material young = MaterialOf("leaf");
        LSystem lsystem = Prepare("B~", [('B', young)]);
        Surface stamped = lsystem.Surfaces.Last();

        Assert.IsInstanceOfType<BicubicPatch>(stamped, "the default leaf was not stamped");
        Assert.IsTrue(ReferenceEquals(young, stamped.Material),
            $"the default leaf ignored the turtle's material: {stamped.Material?.Pigment}");
    }

    [TestMethod]
    public void TestADepthMaterialDoesNotReachTheLeaves()
    {
        // The counterpart, and the one that matters more in practice: a depth binding describes the
        // wood out at that fork, so painting the leaves growing from it the colour of that wood
        // would be a strange reading -- and in a real tree it turns the whole crown the colour of
        // its own twigs.  Only a material named outright reaches a leaf.
        Material twig = MaterialOf("bark");
        LSystem lsystem = Prepare("F[F~]", depthMaterials: [twig, twig]);
        Surface stamped = lsystem.Surfaces.Last();

        Assert.IsInstanceOfType<BicubicPatch>(stamped);
        Assert.IsFalse(ReferenceEquals(twig, stamped.Material),
            "a depth material was painted onto a leaf");
    }

    [TestMethod]
    public void TestAShootsLeafMayDifferFromItsOwnStem()
    {
        // The turtle carries one material at a time, so a shoot whose wood and leaf should differ
        // says so twice: one material before the segment, another before the leaf.
        Material wood = MaterialOf("bark");
        Material leaf = MaterialOf("leaf");
        LSystem lsystem = Prepare("BFC~", [('B', wood), ('C', leaf)]);

        Assert.IsTrue(StemMaterials(lsystem).Any(material => ReferenceEquals(material, wood)),
            "the stem did not take the first material");
        Assert.IsTrue(ReferenceEquals(leaf, lsystem.Surfaces.Last().Material),
            "the leaf did not take the second material");
    }

    [TestMethod]
    public void TestTheDefaultLeafIsStillGreenWhenNoMaterialIsNamed()
    {
        // ...and the fallback has to survive, or every bare tree would go the colour of its own
        // bark the moment this changed.
        LSystem lsystem = Prepare("~");
        Surface stamped = lsystem.Surfaces.Last();

        Assert.IsInstanceOfType<BicubicPatch>(stamped);
        Assert.IsNotNull(stamped.Material, "the default leaf lost its own green");
    }

    [TestMethod]
    public void TestAStampedSurfaceKeepsItsOwnMaterial()
    {
        // A surface named after a '~' brought a material with it, and that is the more specific
        // instruction, so the turtle's does not overwrite it.  Otherwise colouring a stem would
        // silently repaint every leaf hanging off it.
        Material blossom = MaterialOf("blossom");
        Material leafOwn = MaterialOf("leaf");
        LSystem lsystem = new () { Axiom = "BF~", Generations = 0 };

        lsystem.MaterialBindings[new Rune('B')] = blossom;
        lsystem.LeafFactory = () => new Sphere { Material = leafOwn };

        lsystem.PrepareForRendering();

        Surface stamped = lsystem.Surfaces.Last();

        Assert.IsTrue(ReferenceEquals(leafOwn, stamped.Material), "the stamped surface was repainted");
    }

    [TestMethod]
    public void TestABareStampedSurfaceTakesTheTurtlesMaterial()
    {
        // The other side of that: a stamped surface that brought nothing of its own would otherwise
        // fall all the way through to the L-system's material, skipping the one actually in force
        // where it was placed.
        Material blossom = MaterialOf("blossom");
        LSystem lsystem = new () { Axiom = "BF~", Generations = 0 };

        lsystem.MaterialBindings[new Rune('B')] = blossom;
        lsystem.LeafFactory = () => new Sphere { Material = null };

        lsystem.PrepareForRendering();

        Assert.IsTrue(ReferenceEquals(blossom, lsystem.Surfaces.Last().Material));
    }

    [TestMethod]
    public void TestTheLSystemsOwnMaterialStillFillsInWhatIsLeft()
    {
        // Anything the bindings did not reach goes on inheriting from the L-system, which is what
        // makes naming a material for one part of a plant safe: the rest is unaffected.
        Material blossom = MaterialOf("blossom");
        Material own = MaterialOf("bark");
        LSystem lsystem = Prepare("F[BF]F", [('B', blossom)], own: own);
        List<Material> materials = StemMaterials(lsystem);

        Assert.IsTrue(materials.All(material => material is not null));
        Assert.IsTrue(materials.Any(material => ReferenceEquals(material, own)));
        Assert.IsTrue(materials.Any(material => ReferenceEquals(material, blossom)));
    }
}
