using System.Text;
using RayTracer.Geometry.LSystems;
using RayTracer.Terms;

namespace Tests;

/// <summary>
/// These tests cover the reading of an L-system word into its modules.
/// <para>
/// This is the one place in the L-system work with a genuine ambiguity to navigate.  Prusinkiewicz
/// and Lindenmayer point it out themselves: <c>+</c>, <c>&amp;</c>, <c>^</c> and <c>/</c> are used
/// both as letters of the alphabet and as arithmetic operators, and which is meant depends on where
/// it appears.  So these tests are less about parsing than about proving that a word's letters and
/// its arithmetic never get mistaken for one another.
/// </para>
/// </summary>
[TestClass]
public class TestLSystemModuleWord
{
    /// <summary>
    /// A stand-in for the expression compiler that records the text it was handed rather than
    /// compiling it.  That is deliberate: what these tests are checking is *where the word was cut*,
    /// and borrowing the real expression parser to check that would only prove the two agree.
    /// </summary>
    private sealed class Recorder
    {
        public List<string> Seen { get; } = [];

        public Term Compile(string text)
        {
            Seen.Add(text);

            return null;
        }
    }

    private static string LettersOf(ModuleTemplate[] modules)
    {
        return string.Concat(modules.Select(module => module.Letter.ToString()));
    }

    [TestMethod]
    public void TestAWordWithoutParametersIsOneModulePerCharacter()
    {
        // Every L-system written before parameters existed is this case, so it had better be
        // exactly what it always was.
        ModuleTemplate[] modules = ModuleWord.Parse("F[+F]F");

        Assert.AreEqual(6, modules.Length);
        Assert.AreEqual("F[+F]F", LettersOf(modules));
        Assert.IsTrue(modules.All(module => module.Arguments.Length == 0));
    }

    [TestMethod]
    public void TestAWordWithoutParametersNeedsNoCompiler()
    {
        // The compiler is only asked for when a module actually carries arguments.  This is what
        // lets an L-system be built in code, or in a test, without a scene to work anything out.
        ModuleTemplate[] modules = ModuleWord.Parse("$A/[&FL!A]");

        Assert.AreEqual("$A/[&FL!A]", LettersOf(modules));
    }

    [TestMethod]
    public void TestAnOperatorInsideParenthesesIsArithmeticAndOutsideIsALetter()
    {
        // The heart of it, and the book's own example.  In "F(x*h)+F(x*q)" the star is arithmetic
        // and the plus is a turn.  Read the other way round, this word is nonsense.
        Recorder recorder = new ();
        ModuleTemplate[] modules = ModuleWord.Parse("F(x*h)+F(x*q)", recorder.Compile);

        Assert.AreEqual(3, modules.Length);
        Assert.AreEqual("F+F", LettersOf(modules));
        CollectionAssert.AreEqual(new[] { "x*h", "x*q" }, recorder.Seen);
    }

    [TestMethod]
    public void TestEveryTurtleOperatorSurvivesBeingALetter()
    {
        // The four the book names as ambiguous, plus the ones this renderer adds, all sitting
        // between parameterised modules where an operator would be legal arithmetic.
        Recorder recorder = new ();
        ModuleTemplate[] modules = ModuleWord.Parse("F(1)+F(2)-F(3)&F(4)^F(5)/F(6)", recorder.Compile);

        Assert.AreEqual("F+F-F&F^F/F", LettersOf(modules));
        CollectionAssert.AreEqual(new[] { "1", "2", "3", "4", "5", "6" }, recorder.Seen);
    }

    [TestMethod]
    public void TestArgumentsAreSplitOnTopLevelCommasOnly()
    {
        // A comma inside a nested call belongs to that call.  Counting parentheses rather than
        // splitting on every comma is what makes "max(x, 1)" one argument instead of two.
        Recorder recorder = new ();
        ModuleTemplate[] modules = ModuleWord.Parse("F(max(x, 1), t - 2)", recorder.Compile);

        Assert.AreEqual(1, modules.Length);
        Assert.AreEqual(2, modules[0].Arguments.Length);
        CollectionAssert.AreEqual(new[] { "max(x, 1)", " t - 2" }, recorder.Seen);
    }

    [TestMethod]
    public void TestAParenthesisMustBelongToTheLetterBeforeIt()
    {
        // A parenthesis only opens a parameter list when it comes straight after a letter.  Here
        // the open bracket is a letter of its own, so what follows it is a module in its own right
        // rather than the bracket's arguments.
        Recorder recorder = new ();
        ModuleTemplate[] modules = ModuleWord.Parse("[F(2)]", recorder.Compile);

        Assert.AreEqual("[F]", LettersOf(modules));
        CollectionAssert.AreEqual(new[] { "2" }, recorder.Seen);
    }

    [TestMethod]
    public void TestAnUnclosedParenthesisIsRefused()
    {
        Exception exception = Assert.ThrowsExactly<Exception>(
            () => ModuleWord.Parse("F(1", new Recorder().Compile));

        StringAssert.Contains(exception.Message, "never closed");
    }

    [TestMethod]
    public void TestAnEmptyArgumentIsRefused()
    {
        Exception exception = Assert.ThrowsExactly<Exception>(
            () => ModuleWord.Parse("F(1,)", new Recorder().Compile));

        StringAssert.Contains(exception.Message, "empty argument");
    }

    [TestMethod]
    public void TestAPredecessorGivesUpItsLetterAndItsFormalNames()
    {
        (Rune letter, string[] formals) = ModuleWord.ParsePredecessor("F(x, t)");

        Assert.AreEqual(new Rune('F'), letter);
        CollectionAssert.AreEqual(new[] { "x", "t" }, formals);
    }

    [TestMethod]
    public void TestAPredecessorWithoutParametersBindsNothing()
    {
        (Rune letter, string[] formals) = ModuleWord.ParsePredecessor("A");

        Assert.AreEqual(new Rune('A'), letter);
        Assert.AreEqual(0, formals.Length);
    }

    [TestMethod]
    public void TestAPredecessorMustBeOneModule()
    {
        // "AB" is two letters, and a context-free production rewrites one.  Saying so here means a
        // scene finds out when it is read rather than by quietly never matching anything.
        Assert.ThrowsExactly<Exception>(() => ModuleWord.ParsePredecessor("AB"));
        Assert.ThrowsExactly<Exception>(() => ModuleWord.ParsePredecessor("F(x)Q"));
    }
}
