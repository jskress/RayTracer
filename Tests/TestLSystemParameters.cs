using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Renderer;
using RayTracer.Geometry;
using RayTracer.Geometry.LSystems;
using RayTracer.Parser;

namespace Tests;

/// <summary>
/// These tests cover parametric L-systems: modules that carry numbers, productions guarded by a
/// condition, and arithmetic in a successor.
/// <para>
/// This is section 1.10 of Prusinkiewicz and Lindenmayer, and their reason for wanting it is worth
/// keeping in mind while reading these: without parameters every length is an integer multiple of
/// one step, so a structure whose parts are in an irrational proportion -- which is most of what
/// grows -- cannot be drawn at all.
/// </para>
/// </summary>
[TestClass]
public class TestLSystemParameters
{
    /// <summary>
    /// Builds a producer that can work out arithmetic, against a scope of its own.
    /// </summary>
    private static LSystemProducer Producer(string axiom, Variables scope = null)
    {
        return new LSystemProducer
        {
            Axiom = axiom,
            Compile = LanguageParser.CompileModuleArgument,
            Scope = scope ?? new Variables()
        };
    }

    private static ProductionRuleSpec Rule(
        char letter, string[] formals, string production, string condition = null)
    {
        return new ProductionRuleSpec
        {
            Key = letter.ToString(),
            Variable = new System.Text.Rune(letter),
            Formals = formals,
            Condition = condition,
            Production = production,
            BreakValue = 1
        };
    }

    /// <summary>
    /// Draws a word and hands back the tube segments it made, so that what the turtle actually did
    /// with a module's numbers can be measured rather than assumed.
    /// </summary>
    private static List<TubeSegment> Draw(string axiom, double length = 1)
    {
        LSystem lsystem = new LSystem
        {
            Axiom = axiom,
            Generations = 0,
            Compile = LanguageParser.CompileModuleArgument,
            Scope = new Variables(),
            RenderingControls = new LSystemRenderingControls
            {
                RendererType = LSystemRendererType.Tubes,
                Factor = 1,
                Length = length
            }
        };

        lsystem.PrepareForRendering();

        return lsystem.Surfaces.OfType<TubeSegment>().ToList();
    }

    private static double LengthOf(TubeSegment segment)
    {
        return (segment.End - segment.Start).Magnitude;
    }

    [TestMethod]
    public void TestAModulesFirstNumberIsHowFarTheTurtleSteps()
    {
        List<TubeSegment> segments = Draw("F(1)F(2.5)F(0.25)");

        Assert.AreEqual(3, segments.Count);
        Assert.AreEqual(1, LengthOf(segments[0]), 1e-9);
        Assert.AreEqual(2.5, LengthOf(segments[1]), 1e-9);
        Assert.AreEqual(0.25, LengthOf(segments[2]), 1e-9);
    }

    [TestMethod]
    public void TestAModuleWithoutANumberStillUsesTheControls()
    {
        // The other half of the book's rule, and the half that keeps every L-system written before
        // parameters existed drawing exactly as it did: a symbol with no parameter uses the default
        // specified outside the L-system.
        List<TubeSegment> segments = Draw("FF(3)F", length: 1.4);

        Assert.AreEqual(1.4, LengthOf(segments[0]), 1e-9);
        Assert.AreEqual(3, LengthOf(segments[1]), 1e-9);
        Assert.AreEqual(1.4, LengthOf(segments[2]), 1e-9);
    }

    [TestMethod]
    public void TestArithmeticInASuccessorIsWorkedOutPerApplication()
    {
        // ABOP's equation (1.9), the one that cannot be said without parameters: each generation's
        // segments are shorter than the last by an irrational ratio.
        LSystemProducer producer = Producer("A(1)")
            .AddRule(Rule('A', ["s"], "F(s)A(s / 2)"));

        Assert.AreEqual("F(1)A(0.5)", ModuleWord.AsText(producer.Produce(1)));
        Assert.AreEqual("F(1)F(0.5)A(0.25)", ModuleWord.AsText(producer.Produce(2)));
        Assert.AreEqual("F(1)F(0.5)F(0.25)A(0.125)", ModuleWord.AsText(producer.Produce(3)));
    }

    [TestMethod]
    public void TestARuleOnlyAppliesWhenTheArityAgrees()
    {
        // The book requires the letter *and* the number of parameters to agree.  That is what lets
        // F(x) and F(x, t) be two different rules rather than one that sometimes goes wrong.
        LSystemProducer producer = Producer("A(1)A(1, 2)")
            .AddRule(Rule('A', ["x"], "B"));

        // The one-parameter module is rewritten; the two-parameter one has no rule that fits and so
        // stands, which is the identity the book falls back to.
        Assert.AreEqual("BA(1, 2)", ModuleWord.AsText(producer.Produce(1)));
    }

    [TestMethod]
    public void TestAConditionDecidesWhetherARuleApplies()
    {
        LSystemProducer producer = Producer("A(1)A(9)")
            .AddRule(Rule('A', ["t"], "B", "t > 5"));

        Assert.AreEqual("A(1)B", ModuleWord.AsText(producer.Produce(1)));
    }

    [TestMethod]
    public void TestAModuleReachesTheSceneItWasWrittenIn()
    {
        // A module's arithmetic is an ordinary expression in the language, so a value the scene
        // named is in scope.  This is why parametric L-systems need no equivalent of the book's
        // #define: the scene already has one.
        Variables scope = new ();

        scope.SetValue("ratio", 4.0);

        LSystemProducer producer = Producer("A(8)", scope)
            .AddRule(Rule('A', ["s"], "F(s / ratio)"));

        Assert.AreEqual("F(2)", ModuleWord.AsText(producer.Produce(1)));
    }

    [TestMethod]
    public void TestAFormalNameDoesNotEscapeItsOwnApplication()
    {
        // Each application gets a child scope, so one module's numbers cannot be seen by the next
        // module rewritten, nor leak back into the scene.  Were they to leak, the second A below
        // would quietly reuse the first one's s.
        Variables scope = new ();
        LSystemProducer producer = Producer("A(3)B", scope)
            .AddRule(Rule('A', ["s"], "F(s)"))
            .AddRule(Rule('B', [], "F(1)"));

        Assert.AreEqual("F(3)F(1)", ModuleWord.AsText(producer.Produce(1)));
        Assert.IsNull(scope.GetValue("s", typeof(double)),
            "a rule's formal parameter should not be left behind in the scene's scope");
    }

    /// <summary>
    /// Renders a scene written into a temporary file and hands back the picture.
    /// </summary>
    private static Canvas Picture(string scene)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"lsystem-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "out.png");

            File.WriteAllText(path, scene);

            ImageRenderer renderer = new LanguageParser(path).Parse();

            renderer.Render(new RenderOptions
            {
                OutputFileName = output, Width = 90, Height = 70
            });

            return new ImageFile(output).Load()[0];
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// A plant that grows until its counter passes the given number and then stops.  The stopping
    /// rule is written with a "&gt;" on purpose.
    /// </summary>
    private static string ConditionalScene(int stopAt)
    {
        return $$"""
            context { angles are degrees  no gamma }
            camera { location [0, 2, -9]  look at [0, 1.5, 0] }
            point light { location [10, 14, -12] }
            background [0.4, 0.5, 0.8]
            tree = lsystem {
                axiom 'A(1)'
                productions {
                    'A(t) : t > {{stopAt}}' -> 'F(1)'
                    'A(t) : t <= {{stopAt}}' -> 'F(1)A(t + 1)'
                }
                controls { tubes  angle 20  diameter 0.2 }
            }
            lsystem tree { generations 6  material { pigment Red } }
            """;
    }

    [TestMethod]
    public void TestAConditionMayHoldTheCharacterThatMarksAContext()
    {
        // The hazard this test exists for: a condition may perfectly well read "t > 3", and '>' is
        // also what the parser looks for to find a rule's right context.  Split the wrong way round,
        // a conditional rule is read as having a right context of "3", matches nothing, and the
        // scene quietly draws the wrong plant -- no error anywhere.
        //
        // A condition stopping the growth at three should therefore give a shorter plant than one
        // stopping it at one.  If the '>' were being eaten as a context marker, neither rule would
        // fire and the two would be identical.
        Canvas shortPlant = Picture(ConditionalScene(1));
        Canvas tallPlant = Picture(ConditionalScene(4));

        int shortInk = Ink(shortPlant);
        int tallInk = Ink(tallPlant);

        Assert.IsTrue(shortInk > 0, "the short plant drew nothing at all");
        Assert.IsTrue(tallInk > shortInk,
            $"a plant grown to four should show more than one grown to one ({tallInk} against " +
            $"{shortInk}); the condition is not being read");
    }

    /// <summary>
    /// Counts what is not background, which here is the plant.
    /// </summary>
    private static int Ink(Canvas canvas)
    {
        int found = 0;

        for (int x = 0; x < canvas.Width; x++)
        {
            for (int y = 0; y < canvas.Height; y++)
            {
                Color pixel = canvas.GetPixel(x, y);

                if (pixel.Red > pixel.Blue + 0.05)
                    found++;
            }
        }

        return found;
    }
}
