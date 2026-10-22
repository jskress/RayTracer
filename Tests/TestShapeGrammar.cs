using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;
using RayTracer.Instructions.Surfaces.Shapes;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover a shape grammar: the scopes its splits, repeats and faces hand on, the variables each rule
/// sees there, the boxes and roofs it puts in them, its choices, and the scene language that writes one.
/// </summary>
[TestClass]
public class TestShapeGrammar
{
    private const double Near = 1e-9;

    /// <summary>
    /// This class is an action that puts nothing in its scope but notes it, and what its rule saw there.
    /// </summary>
    private sealed class Noted(List<(ShapeScope Scope, Variables Seen)> notes) : ShapeAction
    {
        internal override void Apply(ShapeDerivation derivation, ShapeScope scope, Variables variables, int depth)
        {
            notes.Add((scope, variables));
        }
    }

    /// <summary>
    /// This class is a resolver worked out from the variables, standing in for an expression.
    /// </summary>
    private sealed class Worked<TValue>(Func<Variables, TValue> work) : Resolver<TValue>
    {
        public override TValue Resolve(RenderContext context, Variables variables) => work(variables);
    }

    private static LiteralResolver<double> Number(double value) => new () { Value = value };

    private static double Seen(Variables variables, string name) => (double) variables.GetValue(name, typeof(double));

    private static ShapeScope Lot(double width, double height, double depth) => new (
        Point.Zero, new Vector(1, 0, 0), new Vector(0, 1, 0), new Vector(0, 0, 1), width, height, depth);

    // What a grammar of the given rules puts in a scope, its first rule handed it.
    private static List<Surface> Derived(ShapeScope scope, int seed, params ShapeRule[] rules)
    {
        List<Surface> made = [];

        new ShapeDerivation(rules, new RenderContext(), seed, made.Add).Derive(rules[0].Name, scope, new Variables(), 0);

        return made;
    }

    // The box round a surface, as tight as it is; readied only once, since each readying pads its box again.
    private static BoundingBox BoxOf(Surface surface)
    {
        if (surface.BoundingBox is null)
            surface.PrepareForRendering();

        return surface.TrueBoundingBox();
    }

    private static ShapeRule Rule(string name, params ShapeAction[] actions) => new () { Name = name, Actions = [..actions] };

    private static void AreNear(Point expected, Point actual, string what)
    {
        Assert.IsTrue((expected - actual).Magnitude < 1e-9, $"{what}: expected {expected}, was {actual}");
    }

    private static void AreNear(Vector expected, Vector actual, string what)
    {
        Assert.IsTrue((expected - actual).Magnitude < 1e-9, $"{what}: expected {expected}, was {actual}");
    }

    /// <summary>
    /// A split gives each fixed part its size and the shares what the fixed ones leave, in proportion, each part told
    /// its number and how many there are; fixed parts that will not fit are all shrunk alike, and the shares come to
    /// nothing.
    /// </summary>
    [TestMethod]
    public void TestASplitGivesFixedSizesAndSharesTheRest()
    {
        List<(ShapeScope Scope, Variables Seen)> notes = [];
        ShapeSplit split = new ()
        {
            Axis = 0,
            Parts =
            [
                (Number(2), false, [new Noted(notes)]), (Number(1), true, [new Noted(notes)]),
                (Number(3), true, [new Noted(notes)]), (Number(1), false, [new Noted(notes)])
            ]
        };

        Derived(Lot(10, 4, 2), 0, Rule("Lot", split));

        CollectionAssert.AreEqual(new[] { 0, 2, 3.75, 9 }, notes.Select(note => note.Scope.Corner.X).ToArray());
        CollectionAssert.AreEqual(new[] { 2, 1.75, 5.25, 1 }, notes.Select(note => note.Scope.Width).ToArray());
        CollectionAssert.AreEqual(new[] { 0.0, 1, 2, 3 }, notes.Select(note => Seen(note.Seen, "index")).ToArray());
        Assert.IsTrue(notes.All(note => Seen(note.Seen, "count") == 4));
        Assert.IsTrue(notes.All(note => note.Scope is { Height: 4, Depth: 2 }), "a split changes only its own axis");

        notes.Clear();
        Derived(Lot(4, 10, 2), 0, Rule("Lot", new ShapeSplit
        {
            Axis = 1,
            Parts = [(Number(6), false, [new Noted(notes)]), (Number(6), false, [new Noted(notes)]), (Number(1), true, [new Noted(notes)])]
        }));

        CollectionAssert.AreEqual(new[] { 0, 5.0 }, notes.Select(note => note.Scope.Corner.Y).ToArray());
        CollectionAssert.AreEqual(new[] { 5, 5.0 }, notes.Select(note => note.Scope.Height).ToArray());
    }

    /// <summary>
    /// A repeat tiles its scope with as many whole parts of about its size as fit, stretched to fit exactly, each
    /// told its number by `index` and by a name of its own; a scope narrower than one part is one part, and a size of
    /// nought tiles nothing.
    /// </summary>
    [TestMethod]
    public void TestARepeatFitsWholeTilesAndNumbersThem()
    {
        List<(ShapeScope Scope, Variables Seen)> notes = [];

        Derived(Lot(10, 3, 1), 0, Rule("Lot", new ShapeRepeat
        {
            Axis = 0, Size = Number(2.4), IndexName = "bay", Then = [new Noted(notes)]
        }));

        CollectionAssert.AreEqual(new[] { 0, 2.5, 5, 7.5 }, notes.Select(note => note.Scope.Corner.X).ToArray());
        Assert.IsTrue(notes.All(note => Math.Abs(note.Scope.Width - 2.5) < Near));
        CollectionAssert.AreEqual(new[] { 0.0, 1, 2, 3 }, notes.Select(note => Seen(note.Seen, "bay")).ToArray());
        Assert.IsTrue(notes.All(note => Seen(note.Seen, "count") == 4));

        notes.Clear();
        Derived(Lot(1, 3, 1), 0, Rule("Lot", new ShapeRepeat { Axis = 0, Size = Number(2.4), Then = [new Noted(notes)] }));
        Assert.AreEqual(1, notes.Count);
        Assert.AreEqual(1, notes[0].Scope.Width, Near);

        notes.Clear();
        Derived(Lot(1, 3, 1), 0, Rule("Lot", new ShapeRepeat { Axis = 0, Size = Number(0), Then = [new Noted(notes)] }));
        Assert.AreEqual(0, notes.Count);
    }

    /// <summary>
    /// Each face of a box is handed on as a scope standing on that face, its axes right-handed and its Z pointing
    /// into the box, so that the face's own box -- as deep as asked -- lies inside the box it came from, against that
    /// face; and `sides` takes the upright faces not named, never the top or bottom.
    /// </summary>
    [TestMethod]
    public void TestFacesStandOnTheBoxLookingIn()
    {
        Dictionary<string, List<(ShapeScope Scope, Variables Seen)>> notes = new ()
        {
            ["front"] = [], ["back"] = [], ["left"] = [], ["right"] = [], ["top"] = [], ["bottom"] = []
        };
        ShapeScope box = Lot(4, 3, 2) with { Corner = new Point(1, 0, -1) };
        Point middle = box.At(2, 1.5, 1);

        Derived(box, 0, Rule("Mass", new ShapeFaces
        {
            Depth = Number(0.5),
            Faces = notes.ToDictionary(pair => pair.Key, pair => new List<ShapeAction> { new Noted(pair.Value) })
        }));

        Dictionary<string, Vector> outward = new ()
        {
            ["front"] = new Vector(0, 0, -1), ["back"] = new Vector(0, 0, 1), ["left"] = new Vector(-1, 0, 0),
            ["right"] = new Vector(1, 0, 0), ["top"] = new Vector(0, 1, 0), ["bottom"] = new Vector(0, -1, 0)
        };

        foreach ((string name, List<(ShapeScope Scope, Variables Seen)> seen) in notes)
        {
            Assert.AreEqual(1, seen.Count, name);

            ShapeScope face = seen[0].Scope;

            AreNear(face.Z, -outward[name], $"the {name} face's Z");
            AreNear(face.X.Cross(face.Y), face.Z, $"the {name} face's axes are right-handed");
            Assert.AreEqual(0.5, face.Depth, Near, name);

            // The face's own box: every corner inside the box, and its near face on the box's face.
            for (int corner = 0; corner < 8; corner++)
            {
                Point at = face.At(
                    (corner & 1) * face.Width, (corner >> 1 & 1) * face.Height, (corner >> 2 & 1) * face.Depth);
                Vector from = at - box.Corner;

                Assert.IsTrue(from.X is > -Near and < 4 + Near && from.Y is > -Near and < 3 + Near &&
                              from.Z is > -Near and < 2 + Near, $"the {name} face's corner {at} is outside the box");
            }

            Point onFace = face.At(face.Width / 2, face.Height / 2, 0);
            double reach = Math.Abs(outward[name].Dot(new Vector(4, 3, 2))) / 2;

            Assert.AreEqual(reach, (onFace - middle).Dot(outward[name]), Near, $"the {name} face's scope is not on it");
        }

        List<(ShapeScope Scope, Variables Seen)> fronts = [];
        List<(ShapeScope Scope, Variables Seen)> sides = [];

        Derived(box, 0, Rule("Mass", new ShapeFaces
        {
            Faces = new Dictionary<string, List<ShapeAction>> { ["front"] = [new Noted(fronts)], ["sides"] = [new Noted(sides)] }
        }));

        Assert.AreEqual(1, fronts.Count);
        CollectionAssert.AreEqual(
            new[] { notes["back"][0].Scope, notes["left"][0].Scope, notes["right"][0].Scope },
            sides.Select(side => side.Scope with { Depth = 0.5 }).ToArray(),
            "sides should be the back, the left and the right");
    }

    /// <summary>
    /// A fill is a box of the scope in the grammar's own space -- no transform of its own -- so a pattern runs on
    /// unbroken from one tile to the next; a scope with nothing to it is filled with nothing.
    /// </summary>
    [TestMethod]
    public void TestAFillIsABoxInTheGrammarsSpace()
    {
        ShapeScope turned = new (
            new Point(2, 1, 3), new Vector(0, 0, 1), new Vector(0, 1, 0), new Vector(-1, 0, 0), 4, 2, 0.5);
        List<Surface> made = Derived(turned, 0, Rule("Wall", new ShapeFill()));
        Polyhedron wall = (Polyhedron) made.Single();

        Assert.IsTrue(Matrix.Identity.Matches(wall.Transform));
        Assert.AreEqual(6, wall.FaceCount);
        AreNear(new Point(1.5, 1, 3), BoxOf(wall).Minimum, "the wall's low corner");
        AreNear(new Point(2, 3, 7), BoxOf(wall).Maximum, "the wall's high corner");
        Assert.IsTrue(Containment.Holds(wall, new Point(1.75, 2, 5)));
        Assert.IsFalse(Containment.Holds(wall, new Point(1.25, 2, 5)));

        Assert.AreEqual(0, Derived(turned with { Depth = 0 }, 0, Rule("Wall", new ShapeFill())).Count);
    }

    /// <summary>
    /// An extrude gives a scope its height; a move goes along the scope's own axes, a size sets its size and keeps
    /// its corner.
    /// </summary>
    [TestMethod]
    public void TestExtrudeMoveAndSizeWorkInTheScopesAxes()
    {
        List<(ShapeScope Scope, Variables Seen)> notes = [];
        ShapeScope turned = new (
            new Point(2, 0, 3), new Vector(0, 0, 1), new Vector(0, 1, 0), new Vector(-1, 0, 0), 4, 0, 2);

        Derived(turned, 0, Rule("Lot", new ShapeExtrude
        {
            Height = Number(5),
            Then =
            [
                new Noted(notes),
                new ShapeMove
                {
                    By = new LiteralResolver<Vector> { Value = new Vector(1, 0.5, 0.25) }, Then = [new Noted(notes)]
                },
                new ShapeResize
                {
                    To = new LiteralResolver<Vector> { Value = new Vector(1, 2, 3) }, Then = [new Noted(notes)]
                }
            ]
        }));

        Assert.AreEqual(5, notes[0].Scope.Height);
        Assert.AreEqual(5, Seen(notes[0].Seen, "height"));
        AreNear(new Point(1.75, 0.5, 4), notes[1].Scope.Corner, "the moved corner");
        Assert.AreEqual(new ShapeScope(turned.Corner, turned.X, turned.Y, turned.Z, 1, 2, 3), notes[2].Scope);
        Assert.AreEqual(3, Seen(notes[2].Seen, "depth"));
    }

    /// <summary>
    /// A gabled roof is gable ends in the walls under two slabs meeting at a ridge as high as the slope says, along
    /// X or, `across`, along Z; a hipped one slopes on all four sides to a ridge half the narrower side in; a flat one
    /// is a slab; each reaches past the walls by its overhang.
    /// </summary>
    [TestMethod]
    public void TestARoofStandsOnTheBox()
    {
        ShapeScope box = Lot(6, 3, 4);
        double slope = Math.Tan(Math.PI / 4);
        List<Surface> gable = Derived(box, 0, Rule("Mass", new ShapeRoof
        {
            Kind = "gable", Angle = Number(Math.PI / 4), Overhang = Number(0.3)
        }));
        Polyhedron ends = (Polyhedron) gable[0];

        Assert.AreEqual(3, gable.Count);
        AreNear(new Point(0, 3, 0), BoxOf(ends).Minimum, "the gable ends' low corner");
        AreNear(new Point(6, 3 + 2 * slope, 4), BoxOf(ends).Maximum, "the gable ends' high corner");
        Assert.IsTrue(Containment.Holds(ends, new Point(3, 4.9, 2)), "under the ridge");
        Assert.IsFalse(Containment.Holds(ends, new Point(3, 3.6, 0.5)), "above the slope");
        Assert.IsTrue(Containment.Holds(gable[1], new Point(3, 3.55, 0.5)), "in the near slope");
        Assert.IsTrue(Containment.Holds(gable[2], new Point(3, 3.55, 3.5)), "in the far slope");
        Assert.AreEqual(-0.3, BoxOf(gable[1]).Minimum.X, Near, "the overhang");
        Assert.AreEqual(-0.3, BoxOf(gable[1]).Minimum.Z, Near, "the overhang");

        // A verge of nought: the eaves still reach out, the gable ends not at all, as a terrace's houses' do.
        List<Surface> terraced = Derived(box, 0, Rule("Mass", new ShapeRoof
        {
            Kind = "gable", Angle = Number(Math.PI / 4), Overhang = Number(0.3), Verge = Number(0)
        }));

        foreach (Surface slab in terraced.Skip(1))
        {
            Assert.AreEqual(0, BoxOf(slab).Minimum.X, Near, "the verge");
            Assert.AreEqual(6, BoxOf(slab).Maximum.X, Near, "the verge");
        }

        Assert.AreEqual(-0.3, BoxOf(terraced[1]).Minimum.Z, Near, "the eaves with no verge");
        Assert.AreEqual(4.3, BoxOf(terraced[2]).Maximum.Z, Near, "the eaves with no verge");

        Polyhedron across = (Polyhedron) Derived(box, 0, Rule("Mass", new ShapeRoof
        {
            Kind = "gable", Angle = Number(Math.PI / 4), Across = true
        }))[0];

        AreNear(new Point(6, 3 + 3 * slope, 4), BoxOf(across).Maximum, "the gable ends across");
        Assert.IsTrue(Containment.Holds(across, new Point(3, 5.9, 2)), "under the ridge across");
        Assert.IsFalse(Containment.Holds(across, new Point(0.5, 3.6, 2)), "above the slope across");

        Polyhedron hip = (Polyhedron) Derived(box, 0, Rule("Mass", new ShapeRoof
        {
            Kind = "hip", Angle = Number(Math.PI / 4)
        })).Single();

        Assert.AreEqual(3 + 2 * slope, BoxOf(hip).Maximum.Y, Near);
        Assert.IsTrue(Containment.Holds(hip, new Point(0.5, 3.4, 2)), "under the end slope");
        Assert.IsFalse(Containment.Holds(hip, new Point(0.5, 3.6, 2)), "above the end slope");
        Assert.IsTrue(Containment.Holds(hip, new Point(3, 3.4, 0.5)), "under the side slope");
        Assert.IsFalse(Containment.Holds(hip, new Point(3, 3.6, 0.5)), "above the side slope");

        Polyhedron flat = (Polyhedron) Derived(box, 0, Rule("Mass", new ShapeRoof
        {
            Kind = "flat", Overhang = Number(0.2)
        })).Single();

        AreNear(new Point(-0.2, 3, -0.2), BoxOf(flat).Minimum, "the flat roof's low corner");
        AreNear(new Point(6.2, 3.08, 4.2), BoxOf(flat).Maximum, "the flat roof's high corner");
    }

    /// <summary>
    /// A roof on a turned scope is turned with it, its faces still facing out.
    /// </summary>
    [TestMethod]
    public void TestARoofTurnsWithItsScope()
    {
        ShapeScope turned = new (
            new Point(0, 0, 0), new Vector(0, 0, 1), new Vector(0, 1, 0), new Vector(-1, 0, 0), 6, 3, 4);

        foreach (bool across in new[] { false, true })
        {
            Polyhedron ends = (Polyhedron) Derived(turned, 0, Rule("Mass", new ShapeRoof
            {
                Kind = "gable", Angle = Number(Math.PI / 4), Across = across
            }))[0];
            double rise = across ? 3 : 2;

            AreNear(new Point(-4, 3, 0), BoxOf(ends).Minimum, "the turned roof's low corner");
            AreNear(new Point(0, 3 + rise, 6), BoxOf(ends).Maximum, "the turned roof's high corner");
            Assert.IsTrue(Containment.Holds(ends, new Point(-2, 3 + rise - 0.1, 3)), "under the turned ridge");

            // Down onto a slope a unit in from the eaves, a quarter of the way across: met at a height of a unit
            // above the walls, on a face leaning out and up.
            Point middle = new (-2, 0, 3);
            Ray down = new (across ? new Point(-2, 10, 5) : new Point(-1, 10, 3), new Vector(0, -1, 0));
            List<Intersection> hits = [];

            ends.AddIntersections(down, hits);
            Assert.AreEqual(2, hits.Count);
            Assert.AreEqual(6, hits[0].Distance, 1e-9);

            Vector normal = hits[0].Surface.SurfaceNormalAt(down.At(hits[0].Distance), hits[0]);

            Assert.AreEqual(Math.Sqrt(0.5), normal.Y, 1e-9, "the slope's face should lean up at 45 degrees");
            Assert.IsTrue(normal.Dot(new Vector(down.Origin.X - middle.X, 0, down.Origin.Z - middle.Z).Unit) > 0.7,
                "the slope's face should lean out");
        }
    }

    /// <summary>
    /// A choice goes each way as often as its weight says, the same way for the same seed and place however often
    /// the grammar is carried out, another way for another seed, and two choices at one place draw apart.
    /// </summary>
    [TestMethod]
    public void TestAChoiceIsWeightedAndTheSameForTheSamePlace()
    {
        List<(ShapeScope Scope, Variables Seen)> heavy = [];
        List<(ShapeScope Scope, Variables Seen)> light = [];

        ShapeRule Tiles(int id) => Rule("Lot", new ShapeRepeat
        {
            Axis = 0, Size = Number(1),
            Then =
            [
                new ShapeRepeat
                {
                    Axis = 1, Size = Number(1),
                    Then = [new ShapeChoose { Id = id, Ways = [(Number(4), [new Noted(heavy)]), (Number(1), [new Noted(light)])] }]
                }
            ]
        });

        string Picks(int seed, int id)
        {
            heavy.Clear();
            light.Clear();
            Derived(Lot(40, 40, 1), seed, Tiles(id));

            return string.Join(",", light.Select(note => $"{note.Scope.Corner.X}:{note.Scope.Corner.Y}"));
        }

        string first = Picks(3, 1);

        Assert.AreEqual(0.2, light.Count / 1600.0, 0.03, "the lighter way's share");
        Assert.AreEqual(1600, heavy.Count + light.Count);
        Assert.AreEqual(first, Picks(3, 1), "the same seed should choose the same");
        Assert.AreNotEqual(first, Picks(4, 1), "another seed should choose otherwise");
        Assert.AreNotEqual(first, Picks(3, 2), "another choice should draw otherwise");
    }

    /// <summary>
    /// An if goes by what its scope's rule sees: the third bay of a floor is a door, the rest are not.
    /// </summary>
    [TestMethod]
    public void TestAnIfGoesByTheScopesVariables()
    {
        List<(ShapeScope Scope, Variables Seen)> doors = [];
        List<(ShapeScope Scope, Variables Seen)> windows = [];

        Derived(Lot(10, 3, 1), 0, Rule("Floor", new ShapeRepeat
        {
            Axis = 0, Size = Number(2),
            Then =
            [
                new ShapeIf
                {
                    Condition = new Worked<bool>(seen => Seen(seen, "index") == 2 && Seen(seen, "width") > 1.5),
                    Then = [new Noted(doors)], Else = [new Noted(windows)]
                }
            ]
        }));

        Assert.AreEqual(4, doors[0].Scope.Corner.X, Near);
        Assert.AreEqual(1, doors.Count);
        Assert.AreEqual(4, windows.Count);
    }

    /// <summary>
    /// A placed thing stands with its own origin on the middle of the scope's near face, its axes the scope's, so
    /// its -Z faces out of the building; and a rule naming another hands it the same scope.
    /// </summary>
    [TestMethod]
    public void TestAPlacedThingStandsOnTheFace()
    {
        ShapeScope turned = new (
            new Point(2, 1, 3), new Vector(0, 0, 1), new Vector(0, 1, 0), new Vector(-1, 0, 0), 4, 2, 0.5);
        Surface placed = Derived(turned, 0,
            Rule("Bay", new ShapeSymbol { Name = "Window" }),
            Rule("Window", new ShapePlace { Surfaces = new SphereResolver() })).Single();

        AreNear(new Point(2, 2, 5), placed.Transform * Point.Zero, "the placed thing's origin");
        AreNear(new Vector(1, 0, 0), placed.Transform * new Vector(0, 0, -1), "the placed thing's -Z");
        AreNear(new Vector(0, 0, 1), placed.Transform * new Vector(1, 0, 0), "the placed thing's X");
    }

    /// <summary>
    /// A grammar whose rule hands its scope back to itself is stopped, and a rule handed a scope it does not have is
    /// refused, each with the reason.
    /// </summary>
    [TestMethod]
    public void TestAGrammarGoingRoundInACircleIsStopped()
    {
        StringAssert.Contains(Assert.ThrowsExactly<Exception>(() =>
            Derived(Lot(1, 1, 1), 0, Rule("A", new ShapeSymbol { Name = "B" }), Rule("B", new ShapeSymbol { Name = "A" }))).Message,
            "hand its scope back to itself");
        StringAssert.Contains(Assert.ThrowsExactly<Exception>(() =>
            Derived(Lot(1, 1, 1), 0, Rule("A", new ShapeSymbol { Name = "C" }))).Message, "no rule named 'C'");
    }

    /// <summary>
    /// A grammar's resolver builds its building once, standing on the ground with its middle over the origin and its
    /// front toward -Z; the block on a call of a primitive that gives one back holds no rules and builds nothing more.
    /// </summary>
    [TestMethod]
    public void TestTheResolverBuildsOnceOnTheGround()
    {
        ShapeGrammarResolver resolver = new ()
        {
            Axiom = "Lot",
            SizeResolver = new LiteralResolver<Vector> { Value = new Vector(4, 0, 2) },
            Rules =
            [
                Rule("Lot", new ShapeExtrude
                {
                    Height = Number(3), Then = [new ShapeSplit { Axis = 0, Parts = [(Number(1), true, [new ShapeFill()]), (Number(1), true, [new ShapeFill()])] }]
                })
            ]
        };
        ShapeGrammar grammar = (ShapeGrammar) resolver.ResolveToSurface(new RenderContext(), new Variables());

        Assert.AreEqual(2, grammar.Surfaces.Count);
        AreNear(new Point(-2, 0, -1), BoxOf(grammar.Surfaces[0]).Minimum, "the first half's low corner");
        AreNear(new Point(2, 3, 1), BoxOf(grammar.Surfaces[1]).Maximum, "the second half's high corner");
        Assert.IsNull(resolver.Validate());

        ShapeGrammarResolver extras = (ShapeGrammarResolver) resolver.Clone();

        extras.Rules.Clear();
        Assert.AreEqual(1, resolver.Rules.Count, "a clone's rules are its own");
        Assert.AreEqual(0, ((ShapeGrammar) extras.ResolveToSurface(new RenderContext(), new Variables())).Surfaces.Count);
    }

    /// <summary>
    /// The scene language: a grammar written out with every action, its fills seen where its rules put them -- a red
    /// left half and a blue right half under a gabled roof -- and its mistakes refused as it is read.
    /// </summary>
    [TestMethod]
    public void TestTheSceneLanguageWritesAGrammar()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"shape-grammar-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            (Canvas Image, string Error) Run(string body)
            {
                string scene = Path.Combine(directory, "scene.igl");
                string output = Path.Combine(directory, "scene.png");
                StringWriter captured = new ();
                TextWriter was = Console.Out;

                File.WriteAllText(scene,
                    "context { angles are degrees  no gamma }\n" +
                    "camera { location [0, 2, -12]  look at [0, 2, 0]  field of view 40 }\n" +
                    "point light { location [-5, 8, -12] }\n" + body);
                Console.SetOut(captured);

                try
                {
                    new LanguageParser(scene).Parse()?.Render(new RenderOptions
                    {
                        OutputFileName = output, Width = 40, Height = 30
                    });
                }
                catch (Exception exception)
                {
                    return (null, exception.Message);
                }
                finally
                {
                    Console.SetOut(was);
                }

                return captured.ToString().Contains("Error")
                    ? (null, captured.ToString())
                    : (new ImageFile(output).Load()[0], null);
            }

            (Canvas image, string error) = Run("""
                Red = material { pigment [1, 0, 0]  ambient 0.5 }
                Blue = material { pigment [0, 0, 1]  ambient 0.5 }
                shapes {
                    axiom Lot
                    size [6, 0, 3]
                    rules {
                        Lot -> extrude 3 { Mass }
                        Mass -> faces depth 0.2 { front: Front  sides: { fill material Red } }
                                roof gable 30 overhang 0.2 verge 0 material Blue gables material Red
                        Front -> split X { ~1 Left  ~1 Right }
                        Left -> repeat Y 1 index in floor { if (floor < 5) { fill material Red } else { nothing } }
                        Right -> move [0, 0, 0] { size [width, height, depth] { choose { 1 Blue  0 Left } } }
                        Blue -> fill material Blue
                    }
                    with seed 2
                }
                """);

            Assert.IsNull(error);

            Color left = image.GetPixel(14, 22);
            Color right = image.GetPixel(26, 22);

            Assert.IsTrue(left.Red > 0.3 && left.Blue < 0.1, $"the left half is {left}");
            Assert.IsTrue(right.Blue > 0.3 && right.Red < 0.1, $"the right half is {right}");

            string Refused(string rules) => Run($"shapes {{ axiom Lot  size [1, 1, 1]  rules {{ {rules} }} }}").Error;

            StringAssert.Contains(Refused("Lot -> fill  Lot -> fill"), "written twice");
            StringAssert.Contains(Refused("Lot -> Wall"), "no rule named 'Wall'");
            StringAssert.Contains(Refused("Lot -> faces { middle: fill }"), "front, back, left, right");
            StringAssert.Contains(Refused("Lot -> split W { ~1 fill }"), "X, Y or Z");
            StringAssert.Contains(Refused("Lot -> roof mansard 30"), "gable, hip or flat");
            StringAssert.Contains(Run("shapes { size [1, 1, 1]  rules { Lot -> fill } }").Error, "needs an axiom");
            StringAssert.Contains(Run("shapes { axiom Lot  rules { Lot -> fill } }").Error, "needs a size");
            StringAssert.Contains(Run("shapes { axiom Hut  size [1, 1, 1]  rules { Lot -> fill } }").Error, "Hut");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
