using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover calling the DSL's functions from a scene: that a call parses, that it produces
/// the value it should, and that a call the catalog cannot honour is reported against the text that
/// wrote it.
/// <para>
/// The value a call produces is read back through the size of the rendered image, which is the
/// simplest number a scene can hand out and get back again.  That means these go all the way
/// through the parser and the renderer, rather than building a term tree by hand: what is worth
/// testing here is that the grammar, the tree builder, the catalog and the evaluator agree with each
/// other, and only a real scene exercises all four.
/// </para>
/// </summary>
[TestClass]
public class TestFunctionCalls
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"function-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// Renders a trivial scene whose width is the given expression, and reports the width the image
    /// actually came out.
    /// </summary>
    private int WidthFrom(string expression)
    {
        (int width, string error) = RenderWidth(expression);

        Assert.IsNull(error, error);

        return width;
    }

    /// <summary>
    /// Renders a trivial scene whose width is the given expression, and reports whatever went
    /// wrong, or <c>null</c> if nothing did.
    /// </summary>
    private string ErrorFrom(string expression)
    {
        return RenderWidth(expression).Error;
    }

    private (int Width, string Error) RenderWidth(string expression)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");

        File.WriteAllText(path,
            $"context {{ no gamma  width {expression}  height 30 }}\n" +
            "camera { location [0, 1.5, -5]  look at [0, 1, 0] }\n" +
            "point light { location [-10, 10, -10] }\n" +
            "sphere { translate [0, 1, 0] }");

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            if (renderer is null)
                return (0, captured.ToString());

            renderer.Render(new RenderOptions { OutputFileName = output });

            string text = captured.ToString();

            if (text.Contains("Error"))
                return (0, text);

            Canvas image = new ImageFile(output).Load()[0];

            return (image.Width, null);
        }
        catch (Exception exception)
        {
            return (0, exception.ToString());
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    /// <summary>
    /// This tests that a call is evaluated and its value used.
    /// </summary>
    [TestMethod]
    public void TestACallProducesItsValue()
    {
        Assert.AreEqual(80, WidthFrom("sqrt(6400)"));
        Assert.AreEqual(80, WidthFrom("pow(2, 6) + 16"));
        Assert.AreEqual(80, WidthFrom("abs(-80)"));
    }

    /// <summary>
    /// This tests that a function named by one of the DSL's keywords may still be called.  Several
    /// are -- "min", "max" and "length" are all keywords in their own right -- so this is not a
    /// corner case but the common one.
    /// </summary>
    [TestMethod]
    public void TestAFunctionMayBeNamedByAKeyword()
    {
        Assert.AreEqual(80, WidthFrom("max(80, 20)"));
        Assert.AreEqual(80, WidthFrom("min(80, 500)"));
        Assert.AreEqual(80, WidthFrom("length([48, 64, 0])"));
    }

    /// <summary>
    /// This tests that a call's arguments are themselves expressions, so calls nest and compose with
    /// the arithmetic around them.
    /// </summary>
    [TestMethod]
    public void TestCallsNestAndCompose()
    {
        Assert.AreEqual(80, WidthFrom("sqrt(pow(80, 2))"));
        Assert.AreEqual(80, WidthFrom("max(sqrt(16), 8) * 10"));
        Assert.AreEqual(80, WidthFrom("length([3, 4, 0]) * 16"));
        Assert.AreEqual(80, WidthFrom("abs(min(-80, -20))"));

        // A parenthesized expression is still just that, whether it stands alone or is an argument.
        // The call form is a name *followed* by a parenthesis, so the two cannot be confused.
        Assert.AreEqual(80, WidthFrom("(40 + 40)"));
        Assert.AreEqual(80, WidthFrom("sqrt((40 + 40) * 80)"));
    }

    /// <summary>
    /// This tests that a variable may be handed to a function, since a scene's variables are read
    /// late and so are exactly what the catalog cannot resolve against until then.
    /// </summary>
    [TestMethod]
    public void TestAVariableMayBeAnArgument()
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");

        File.WriteAllText(path,
            """
            side = 6400
            context { no gamma  width sqrt(side)  height 30 }
            camera { location [0, 1.5, -5]  look at [0, 1, 0] }
            point light { location [-10, 10, -10] }
            sphere { translate [0, 1, 0] }
            """);

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            Assert.IsNotNull(renderer, $"the scene did not parse: {captured}");

            renderer.Render(new RenderOptions { OutputFileName = output });

            Assert.AreEqual(80, new ImageFile(output).Load()[0].Width);
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    /// <summary>
    /// This tests the functions whose behaviour is a choice rather than a given, since those are the
    /// ones that could be wrong.  The rest are one-line calls into <c>Math</c> and are covered by
    /// <see cref="TestFunctionCatalog.TestEveryDeclaredFunctionIsWellFormed"/>.
    /// </summary>
    [TestMethod]
    public void TestTheFunctionsThatMadeAChoice()
    {
        // Angles are radians, whatever "angles are" says, and the scene above never sets it -- so
        // degrees, the DSL's default, are in force and sin(90) is still not 1.
        Assert.AreEqual(80, WidthFrom("sin(π / 2) * 80"));
        Assert.AreEqual(80, WidthFrom("round(sin(90) * 80) + 8"));

        // Only the conversion out of radians is a function; going in is what the postfix angle
        // operators are for, so that no word means opposite things in two places.
        Assert.AreEqual(80, WidthFrom("toDegrees(π) - 100"));
        Assert.IsNotNull(ErrorFrom("radians(90)"));

        // mod counts down from its divisor where % takes the sign of what is divided, which is what
        // lets a field repeat rather than mirror about the origin.
        Assert.AreEqual(80, WidthFrom("mod(-1, 4) * 26 + 2"));
        Assert.AreEqual(80, WidthFrom("(-1 % 4) * -80"));

        // A cube root is defined for a negative number; a power of a third is not.
        Assert.AreEqual(80, WidthFrom("cbrt(-8) * -40"));

        // One argument asks a vector for its smallest or largest component; two compare a pair.
        Assert.AreEqual(80, WidthFrom("max([20, 80, 60])"));
        Assert.AreEqual(80, WidthFrom("min([80, 300, 900])"));
        Assert.AreEqual(80, WidthFrom("length(max([48, 0, 0], [0, 64, 0]))"));

        // Noise is repeatable, and lands between 0 and 1 -- so this is 80 only if both hold.
        Assert.AreEqual(80, WidthFrom("(noise([1.5, 2.5, 3.5]) - noise([1.5, 2.5, 3.5]) + 1) * 80"));
        Assert.AreEqual(80, WidthFrom("floor(noise([1.5, 2.5, 3.5])) + 80"));

        // Smoothstep is flat outside its edges and half way up in the middle.
        Assert.AreEqual(80, WidthFrom("smoothstep(0, 10, 5) * 160"));
        Assert.AreEqual(80, WidthFrom("smoothstep(0, 10, -3) + 80"));
        Assert.AreEqual(80, WidthFrom("smoothstep(0, 10, 30) * 80"));
    }

    /// <summary>
    /// This tests that a misspelled name and a wrong number of arguments are both caught while the
    /// scene is being read, since neither needs a value to look at.
    /// </summary>
    [TestMethod]
    public void TestACallThatCannotWorkIsReportedWhenRead()
    {
        string error = ErrorFrom("sqroot(6400)");

        Assert.IsNotNull(error);
        Assert.Contains("no function named 'sqroot'", error);

        error = ErrorFrom("sqrt(1, 2)");

        Assert.IsNotNull(error);
        Assert.Contains("does not take 2 arguments", error);

        // A call supplying nothing at all is a call, and is turned away for taking no arguments
        // rather than for being unreadable.
        error = ErrorFrom("sqrt()");

        Assert.IsNotNull(error);
        Assert.Contains("does not take 0 arguments", error);
    }

    /// <summary>
    /// This tests that a call whose values are of the wrong types is reported when the scene is
    /// evaluated, which is the soonest those types are known.
    /// </summary>
    [TestMethod]
    public void TestACallGivenTheWrongTypesIsReportedWhenEvaluated()
    {
        string error = ErrorFrom("dot(1, 2)");

        Assert.IsNotNull(error);
        Assert.Contains("dot(vector, vector)", error);
    }

    /// <summary>
    /// This tests that a scattered value may be had wherever an expression stands, and that asking for
    /// the same one twice gives the same number -- which a running stream could not do, and which is
    /// what makes a scene that scatters things reproducible.
    /// </summary>
    [TestMethod]
    public void TestAScatteredValueMayBeHadInAnExpression()
    {
        Assert.AreEqual(80, WidthFrom("random(3) == random(3) ? 80 : 40"));
        Assert.AreEqual(80, WidthFrom("random(3) != random(4) ? 80 : 40"));
        Assert.AreEqual(80, WidthFrom("random(3, 1) != random(3, 2) ? 80 : 40"));

        // Always between zero and one.
        Assert.AreEqual(80, WidthFrom("random(7) >= 0 and random(7) < 1 ? 80 : 40"));

        // And a known one, so that the number a scene gets is pinned here as well as in the generator.
        Assert.AreEqual(469, WidthFrom("round(random(3) * 1000)"));
    }

    /// <summary>
    /// This tests that a scattered value is turned away from a field, where it would mean nothing: a
    /// surface is found by looking for where a function crosses zero, and a function whose neighboring
    /// values are unrelated crosses zero everywhere and nowhere.
    /// </summary>
    [TestMethod]
    public void TestAScatteredValueIsRefusedInAField()
    {
        string path = Path.Combine(_directory, "field.igl");

        File.WriteAllText(path, """
            camera { location [0, 1, -5]  look at [0, 0, 0] }
            point light { location [-5, 5, -5] }
            isosurface {
                function { x² + y² + z² - 1 + random(x) }
                bounded by [-2, -2, -2], [2, 2, 2]
            }
            """);

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            // The refusal comes when the field is built, which is while the scene is being made ready
            // rather than while it is being read, so the render has to be started to provoke it.
            ImageRenderer renderer = new LanguageParser(path).Parse();

            renderer?.Render(new RenderOptions
            {
                OutputFileName = Path.Combine(_directory, "field.png"), Width = 40, Height = 30
            });
        }
        catch (Exception exception)
        {
            Console.Write(exception);
        }
        finally
        {
            Console.SetOut(was);
        }

        string refused = captured.ToString();

        Assert.Contains("no place in a density or an isosurface", refused);
        Assert.Contains("noise", refused);
    }
}
