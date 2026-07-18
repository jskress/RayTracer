using System.Text;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Geometry.LSystems;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace Tests;

[TestClass]
public class TestLSystemLeaves
{
    // Build an L-system from an axiom alone (generation 0, so the axiom is the production
    // verbatim) and prepare it, so we can inspect the geometry a '~' actually stamps down.
    // The pipes renderer's own geometry is only ever spheres and cylinders, never patches, so
    // counting bicubic patches counts default leaves.
    private static LSystem Prepare(string axiom, Func<Surface> leafFactory = null)
    {
        LSystem lsystem = new LSystem { Axiom = axiom, Generations = 0 };

        if (leafFactory is not null)
            lsystem.LeafFactory = leafFactory;

        lsystem.PrepareForRendering();

        return lsystem;
    }

    // The same, but with surfaces bound to the characters a production may name after a '~'.
    private static LSystem PrepareWithSurfaces(
        string axiom, params (char Character, Func<Surface> Factory)[] bindings)
    {
        LSystem lsystem = new LSystem { Axiom = axiom, Generations = 0 };

        foreach ((char character, Func<Surface> factory) in bindings)
            lsystem.SurfaceFactories[new Rune(character)] = factory;

        lsystem.PrepareForRendering();

        return lsystem;
    }

    private static ProductionRuleSpec Rule(string key, string production)
    {
        int lt = key.IndexOf('<');
        int gt = key.IndexOf('>');
        int variableStart = lt < 0 ? 0 : lt + 1;
        int variableEnd = gt < 0 ? key.Length : gt;

        return new ProductionRuleSpec
        {
            Key = key,
            Variable = key[variableStart..variableEnd].AsRunes()[0],
            LeftContext = lt < 0 ? null : ProductionBranch.Parse(key[..lt].AsRunes()),
            RightContext = gt < 0 ? null : ProductionBranch.Parse(key[(gt + 1)..].AsRunes()),
            Production = production
        };
    }

    [TestMethod]
    public void TestEachLeafCommandStampsOneLeaf()
    {
        // Three '~' in the production, so three leaves.
        LSystem lsystem = Prepare("F~F~F~");

        Assert.AreEqual(3, lsystem.Surfaces.Count(surface => surface is BicubicPatch));
    }

    [TestMethod]
    public void TestTheDefaultLeafIsAGreenPatch()
    {
        LSystem lsystem = Prepare("F~");
        BicubicPatch leaf = lsystem.Surfaces.OfType<BicubicPatch>().Single();

        Assert.IsNotNull(leaf.Material);
        Assert.IsTrue(leaf.Material.Pigment.Matches(new SolidPigment(Colors.ForestGreen)));
    }

    [TestMethod]
    public void TestANamedLeafSurfaceIsUsedAndKeepsItsOwnMaterial()
    {
        // A scene that names its own leaf surface (here a red cube -- a shape the pipes renderer
        // never makes on its own) must stamp that surface, with its material intact, rather than
        // the built-in green patch.
        Material red = new () { Pigment = new SolidPigment(Colors.Red) };
        LSystem lsystem = Prepare("F~", () => new Cube { Material = red });
        Cube leaf = lsystem.Surfaces.OfType<Cube>().Single();

        Assert.AreEqual(0, lsystem.Surfaces.Count(surface => surface is BicubicPatch));
        Assert.IsTrue(leaf.Material.Pigment.Matches(new SolidPigment(Colors.Red)));
    }

    [TestMethod]
    public void TestALeafIsPlacedAtTheTurtleLocation()
    {
        // After one 'F' the turtle has moved one length along its heading (+X), so the leaf's
        // local origin -- its base -- must land there.
        LSystem lsystem = Prepare("F~");
        BicubicPatch leaf = lsystem.Surfaces.OfType<BicubicPatch>().Single();
        Point placed = leaf.Transform * Point.Zero;

        Assert.IsTrue(placed.Matches(new Point(1, 0, 0)), placed.ToString());
    }

    [TestMethod]
    public void TestALeafGrowsAlongTheBranch()
    {
        // The leaf's local +Z (its growth direction) is carried to the turtle's heading.
        LSystem lsystem = Prepare("F~");
        BicubicPatch leaf = lsystem.Surfaces.OfType<BicubicPatch>().Single();
        Vector growth = leaf.Transform * Directions.In;

        Assert.IsTrue(growth.Matches(Directions.Right), growth.ToString());
    }

    [TestMethod]
    public void TestALeafFollowsTheTurtlesRoll()
    {
        // The leaf's local +Y is its face; a roll before the leaf turns the turtle's up, so the
        // rolled leaf's face must differ from the unrolled one's -- placement uses the whole
        // frame, not just the heading.
        Vector faceNoRoll = Prepare("F~").Surfaces.OfType<BicubicPatch>().Single()
            .Transform * Directions.Up;
        Vector faceRolled = Prepare("F/~").Surfaces.OfType<BicubicPatch>().Single()
            .Transform * Directions.Up;

        Assert.IsFalse(faceNoRoll.Matches(faceRolled), $"{faceNoRoll} vs {faceRolled}");
    }

    [TestMethod]
    public void TestALeafAtTheEdgeIsNotCulledByTheBoundingBox()
    {
        // The leaf reaches past the turtle path's own extent (its tip sits near x = 2, beyond the
        // stems that end near x = 1.5).  If the group's bounding box were built from the turtle
        // path alone, a ray aimed only at the leaf would be culled before the leaf was ever
        // tested -- so this ray must still find the leaf.
        LSystem lsystem = Prepare("F~");
        List<Intersection> intersections = [];

        lsystem.Intersect(new Ray(new Point(1.7, 1, 0), new Vector(0, -1, 0)), intersections);

        Assert.IsTrue(intersections.Count > 0);
    }

    [TestMethod]
    public void TestAProductionCanNameWhichSurfaceEachLeafStamps()
    {
        // '~L' grows one bound surface where '~K' grows another, so a single plant can carry
        // both leaves and fruit.  Each occurrence gets its own copy to place independently.
        LSystem lsystem = PrepareWithSurfaces("F~LF~KF~L",
            ('L', () => new Cube()),
            ('K', () => new Torus { MajorRadius = 1, MinorRadius = 0.25 }));
        List<Cube> cubes = lsystem.Surfaces.OfType<Cube>().ToList();

        Assert.AreEqual(2, cubes.Count);
        Assert.AreEqual(1, lsystem.Surfaces.Count(surface => surface is Torus));
        Assert.AreEqual(0, lsystem.Surfaces.Count(surface => surface is BicubicPatch));
        Assert.AreNotSame(cubes[0], cubes[1]);
    }

    [TestMethod]
    public void TestABareLeafCommandStillStampsTheDefault()
    {
        // Naming surfaces must not disturb a '~' that names none of them.
        LSystem lsystem = PrepareWithSurfaces("F~", ('L', () => new Cube()));

        Assert.AreEqual(1, lsystem.Surfaces.Count(surface => surface is BicubicPatch));
        Assert.AreEqual(0, lsystem.Surfaces.Count(surface => surface is Cube));
    }

    [TestMethod]
    public void TestARuneAfterALeafThatNamesNoSurfaceIsStillRunAsACommand()
    {
        // The committed scenes' productions read "...~]", so a '~' must leave a rune it does not
        // recognise alone.  Here the ']' still has to close the branch: the last segment must
        // resume from the branch point at x = 1, not carry on from inside the turned branch.
        LSystem lsystem = PrepareWithSurfaces("F[+F~]F", ('L', () => new Cube()));
        List<Cylinder> cylinders = lsystem.Surfaces.OfType<Cylinder>().ToList();
        Point lastStart = cylinders[2].Transform * Point.Zero;

        Assert.AreEqual(1, lsystem.Surfaces.Count(surface => surface is BicubicPatch));
        Assert.AreEqual(3, cylinders.Count);
        Assert.IsTrue(lastStart.Matches(new Point(1, 0, 0)), lastStart.ToString());
    }

    [TestMethod]
    public void TestASurfaceCharacterClaimedByALeafIsNotAlsoRunAsACommand()
    {
        // Binding a character that is also a command means a '~' before it claims it: "~F" stamps
        // the surface, and that F does not additionally draw.  This is the accepted cost of
        // letting a bare '~' go on meaning "the default surface"; bind another character to
        // avoid it.
        LSystem plain = PrepareWithSurfaces("FF");
        LSystem bound = PrepareWithSurfaces("F~F", ('F', () => new Cube()));

        Assert.AreEqual(2, plain.Surfaces.Count(surface => surface is Cylinder));
        Assert.AreEqual(1, bound.Surfaces.Count(surface => surface is Cylinder));
        Assert.AreEqual(1, bound.Surfaces.Count(surface => surface is Cube));
    }

    [TestMethod]
    public void TestLeavesAreRealSymbolsWhenIgnoringCommands()
    {
        // '~' is geometry, not a turtle-orientation command, so it must stay out of the set that
        // "ignore commands" sweeps aside for context evaluation.  Here the F becomes FF only when
        // flanked by a leaf on each side; with '~' correctly treated as a real symbol, the F in
        // "~F~" matches and doubles, so two stem cylinders result.  Were '~' swept aside, the F
        // would find no leaf beside it and stay single.
        LSystem lsystem = new () { Axiom = "~F~", Generations = 1, IgnoreOrientationCommands = true };

        lsystem.ProductionRules.Add(Rule("~<F>~", "FF"));
        lsystem.PrepareForRendering();

        Assert.AreEqual(2, lsystem.Surfaces.Count(surface => surface is Cylinder));
        Assert.AreEqual(2, lsystem.Surfaces.Count(surface => surface is BicubicPatch));
    }
}
