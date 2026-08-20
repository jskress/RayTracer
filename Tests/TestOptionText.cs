using RayTracer.General;
using RayTracer.Options;

namespace Tests;

/// <summary>
/// These tests cover the command line options whose value is the name of one choice out of a few.
/// <para>
/// Both of these options promise, in their own help text, that the name may be abbreviated and that
/// case does not matter.  The tests below are mostly about holding them to that, because the obvious
/// way of implementing it does not: title-casing the text first leaves a word that is already all
/// capitals alone, taking it for an acronym, so <c>VERBOSE</c> came out unchanged and was refused.
/// </para>
/// </summary>
[TestClass]
public class TestOptionText
{
    private static OutputLevel LevelFor(string text)
    {
        return new RenderOptions { OutputLevelText = text }.OutputLevel;
    }

    private static ProgressStyle StyleFor(string text)
    {
        return new RenderOptions { ProgressStyleText = text }.ProgressStyle;
    }

    [TestMethod]
    public void TestTheDefaultsAreWhatARenderGetsWhenNobodyAsks()
    {
        RenderOptions options = new ();

        Assert.AreEqual(OutputLevel.Normal, options.OutputLevel);
        Assert.AreEqual(ProgressStyle.Bar, options.ProgressStyle);
    }

    [TestMethod]
    public void TestEveryOutputLevelCanBeNamed()
    {
        Assert.AreEqual(OutputLevel.Quiet, LevelFor("quiet"));
        Assert.AreEqual(OutputLevel.Normal, LevelFor("normal"));
        Assert.AreEqual(OutputLevel.Chatty, LevelFor("chatty"));
        Assert.AreEqual(OutputLevel.Verbose, LevelFor("verbose"));
    }

    [TestMethod]
    public void TestEveryProgressStyleCanBeNamed()
    {
        Assert.AreEqual(ProgressStyle.Bar, StyleFor("bar"));
        Assert.AreEqual(ProgressStyle.Tool, StyleFor("tool"));
        Assert.AreEqual(ProgressStyle.None, StyleFor("none"));
    }

    [TestMethod]
    public void TestAnAbbreviationIsEnough()
    {
        Assert.AreEqual(OutputLevel.Quiet, LevelFor("q"));
        Assert.AreEqual(OutputLevel.Verbose, LevelFor("verb"));
        Assert.AreEqual(ProgressStyle.Tool, StyleFor("t"));
    }

    [TestMethod]
    public void TestAllCapitalsIsStillTheSameName()
    {
        // The fault this method exists for.  Both options say in their help text that their values are
        // not case-sensitive, and shouting one of them used to be an error.
        Assert.AreEqual(OutputLevel.Quiet, LevelFor("QUIET"));
        Assert.AreEqual(OutputLevel.Verbose, LevelFor("VERBOSE"));
        Assert.AreEqual(OutputLevel.Chatty, LevelFor("C"));
        Assert.AreEqual(ProgressStyle.Tool, StyleFor("TOOL"));
    }

    [TestMethod]
    public void TestAnyOtherMixtureOfCasesWorksToo()
    {
        Assert.AreEqual(OutputLevel.Chatty, LevelFor("ChAtTy"));
        Assert.AreEqual(ProgressStyle.None, StyleFor("nOnE"));
    }

    [TestMethod]
    public void TestNothingAtAllIsRefusedRatherThanGuessedAt()
    {
        // The one that was silent.  Every name begins with an empty string, so searching for one
        // matched the first value declared -- and the first output level declared is Quiet.  A script
        // filling the value in from an unset variable therefore rendered in complete silence: no
        // output, no error, and nothing to suggest the level had not been read as `normal`.
        Assert.ThrowsExactly<ArgumentException>(() => LevelFor(""));
        Assert.ThrowsExactly<ArgumentException>(() => LevelFor("   "));
        Assert.ThrowsExactly<ArgumentException>(() => StyleFor(""));
    }

    [TestMethod]
    public void TestTextThatNamesNothingIsRefusedAndSaysWhatWouldDo()
    {
        ArgumentException level = Assert.ThrowsExactly<ArgumentException>(() => LevelFor("loud"));

        StringAssert.Contains(level.Message, "quiet, normal, chatty or verbose");

        ArgumentException style = Assert.ThrowsExactly<ArgumentException>(() => StyleFor("verbose"));

        StringAssert.Contains(style.Message, "bar, tool or none");
    }

    [TestMethod]
    public void TestBothValuesReadBackAsTheyWereWritten()
    {
        // The properties are read as well as written; the round trip is what the help screen shows.
        RenderOptions options = new ()
        {
            OutputLevelText = "CHATTY",
            ProgressStyleText = "tool"
        };

        Assert.AreEqual("chatty", options.OutputLevelText);
        Assert.AreEqual("tool", options.ProgressStyleText);
    }
}
