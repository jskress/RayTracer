using ImageMagick;
using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;

namespace Tests;

/// <summary>
/// These tests cover animation: values given at keys, a scene carried out again for every frame with its time,
/// where the frames go, and placements that follow the time blurred through the frame.
/// </summary>
[TestClass]
public class TestAnimation
{
    private string _directory;

    [TestInitialize]
    public void MakeTheDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"animation-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveTheDirectory()
    {
        Directory.Delete(_directory, true);
    }

    private static Keyframes Keys(bool smooth, bool loops, params (double Time, object Value, Keyframes.Way Way)[] keys) =>
        new () { Keys = [..keys], Smooth = smooth, Loops = loops };

    /// <summary>
    /// Between two keys a value goes at an even pace unless said; before the first it holds the first and after the
    /// last the last; each way of easing takes the share of the way it should, and a hold waits for its key.
    /// </summary>
    [TestMethod]
    public void TestAValueGoesFromKeyToKeyAsItIsTold()
    {
        Keyframes even = Keys(false, false, (1, 10.0, Keyframes.Way.Linear), (3, 20.0, Keyframes.Way.Linear));

        Assert.AreEqual(10.0, even.At(0));
        Assert.AreEqual(10.0, even.At(1));
        Assert.AreEqual(15.0, even.At(2));
        Assert.AreEqual(17.5, even.At(2.5));
        Assert.AreEqual(20.0, even.At(9));

        foreach ((Keyframes.Way way, double quarter) in new[]
                 {
                     (Keyframes.Way.EaseIn, 0.0625), (Keyframes.Way.EaseOut, 0.4375),
                     (Keyframes.Way.EaseInOut, 0.15625), (Keyframes.Way.Hold, 0.0)
                 })
        {
            Keyframes eased = Keys(false, false, (0, 0.0, Keyframes.Way.Linear), (4, 1.0, way));

            Assert.AreEqual(quarter, (double) eased.At(1), 1e-12, $"{way} a quarter of the way");
            Assert.AreEqual(1.0, (double) eased.At(4), 1e-12, $"{way} at its key");
        }
    }

    /// <summary>
    /// A value keeps its kind: a tuple stays a tuple, a point a point, a vector a vector and a color a color, each
    /// part going its own way.
    /// </summary>
    [TestMethod]
    public void TestAValueKeepsItsKind()
    {
        object Halfway(object from, object to) =>
            Keys(false, false, (0, from, Keyframes.Way.Linear), (2, to, Keyframes.Way.Linear)).At(1);

        Assert.AreEqual(new NumberTuple(1, 2, 3, 0), Halfway(new NumberTuple(0, 0, 0, 0), new NumberTuple(2, 4, 6, 0)));
        Assert.IsInstanceOfType<Point>(Halfway(new Point(0, 0, 0), new Point(2, 2, 2)));
        Assert.IsInstanceOfType<Vector>(Halfway(new Vector(0, 0, 0), new Vector(2, 2, 2)));

        Color color = (Color) Halfway(new Color(1, 0, 0), new Color(0, 0, 1));

        Assert.AreEqual(0.5, color.Red, 1e-12);
        Assert.AreEqual(0.5, color.Blue, 1e-12);
    }

    /// <summary>
    /// Looping keys begin again at the first once the last is reached, before the first as well as after; smooth
    /// keys pass through every key with no stop and no kink in their pace there.
    /// </summary>
    [TestMethod]
    public void TestKeysLoopAndPassSmoothly()
    {
        Keyframes loop = Keys(false, true, (0, 0.0, Keyframes.Way.Linear), (1, 2.0, Keyframes.Way.Linear),
            (2, 0.0, Keyframes.Way.Linear));

        Assert.AreEqual(1.0, (double) loop.At(2.5), 1e-12);
        Assert.AreEqual(1.0, (double) loop.At(10.5), 1e-12);
        Assert.AreEqual(1.0, (double) loop.At(-1.5), 1e-12);

        Keyframes smooth = Keys(true, false, (0, 0.0, Keyframes.Way.Linear), (1, 2.0, Keyframes.Way.Linear),
            (3, 3.0, Keyframes.Way.Linear), (4, 0.0, Keyframes.Way.Linear));
        const double d = 1e-6;

        foreach (double key in new[] { 1.0, 3.0 })
        {
            double before = ((double) smooth.At(key) - (double) smooth.At(key - d)) / d;
            double after = ((double) smooth.At(key + d) - (double) smooth.At(key)) / d;

            Assert.AreEqual(before, after, 1e-3, $"the pace either side of the key at {key}");
            Assert.IsTrue(Math.Abs(before) > 0.1, $"the value stopped at the key at {key}");
        }

        Assert.AreEqual(2.0, (double) smooth.At(1), 1e-12);
        Assert.AreEqual(3.0, (double) smooth.At(3), 1e-12);

        // Keys evenly along a straight line are the line itself: a smooth run's pace at a key is the pace across
        // it, neither more nor less.
        Keyframes straight = Keys(true, false, (0, 0.0, Keyframes.Way.Linear), (1, 1.0, Keyframes.Way.Linear),
            (2, 2.0, Keyframes.Way.Linear), (3, 3.0, Keyframes.Way.Linear));

        foreach (double time in new[] { 1.25, 1.5, 1.8 })
            Assert.AreEqual(time, (double) straight.At(time), 1e-12, $"the straight run at {time}");
    }

    /// <summary>
    /// Renders a scene with the given options and hands back what stopped it, or null.
    /// </summary>
    private string Run(string body, string output, long? frame = null, string frames = null, int size = 16,
        string camera = "camera { location [0, 1, -6]  look at [0, 0.5, 0] }", bool still = false)
    {
        string scene = Path.Combine(_directory, "scene.igl");
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        File.WriteAllText(scene, camera + "\npoint light { location [-4, 6, -6] }\n" + body);
        Console.SetOut(captured);

        try
        {
            new LanguageParser(scene).Parse()?.Render(new RenderOptions
            {
                OutputFileName = Path.Combine(_directory, output), Width = size, Height = size,
                Frame = frame, Frames = frames, Still = still
            });
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
        finally
        {
            Console.SetOut(was);
        }

        return captured.ToString().Contains("Error") ? captured.ToString() : null;
    }

    private Canvas Picture(string name) => new ImageFile(Path.Combine(_directory, name)).Load()[0];

    // A ball that turns from red to blue over two seconds: its color at the middle of the picture says what time
    // the frame was carried out at.
    private const string Turning = """
        context { animation length 2  rate 3  no gamma }
        Tint = keys { 0: color [1, 0, 0]  2: color [0, 0, 1] }
        sphere { material { pigment Tint(time)  ambient 1  diffuse 0 }  translate Y 0.5 }
        """;

    /// <summary>
    /// An animation renders every frame of its length at its rate, each to a file of its own carrying its number,
    /// each at its own time; a frame asked for alone is a still at that frame's time, written where it was asked
    /// to be; a run of frames renders just those.
    /// </summary>
    [TestMethod]
    public void TestAnAnimationRendersEveryFrameAtItsTime()
    {
        Assert.IsNull(Run(Turning, "turn.png"));

        string[] written = Directory.GetFiles(_directory, "turn-*.png").Select(Path.GetFileName).Order().ToArray();

        CollectionAssert.AreEqual(Enumerable.Range(0, 6).Select(frame => $"turn-{frame:D7}.png").ToArray(), written);

        for (int frame = 0; frame < 6; frame++)
        {
            Color middle = Picture($"turn-{frame:D7}.png").GetPixel(8, 8);
            double time = frame / 3.0;

            Assert.AreEqual(1 - time / 2, middle.Red, 0.02, $"frame {frame}'s red");
            Assert.AreEqual(time / 2, middle.Blue, 0.02, $"frame {frame}'s blue");
        }

        Assert.IsNull(Run(Turning, "one.png", frame: 3));
        Assert.IsTrue(File.Exists(Path.Combine(_directory, "one.png")), "a frame asked for alone is written as asked");
        Assert.AreEqual(0.5, Picture("one.png").GetPixel(8, 8).Red, 0.02);

        Assert.IsNull(Run(Turning, "run.png", frames: "2-4"));
        Assert.AreEqual(3, Directory.GetFiles(_directory, "run-*.png").Length);
        Assert.IsTrue(File.Exists(Path.Combine(_directory, "run-0000004.png")));
    }

    /// <summary>
    /// Each frame carries the scene out from the start, and finds none of the frame before's things still
    /// standing: the second frame of a run is the very picture the second frame rendered alone is.
    /// </summary>
    [TestMethod]
    public void TestAFrameIsTheSameRenderedInARunOrAlone()
    {
        const string sliding = """
            context { animation frames 2 }
            sphere { material { pigment [1, 1, 1] }  translate [frame * 1.5 - 0.75, 0.5, 0]  scale 0.5 }
            """;

        Assert.IsNull(Run(sliding, "slide.png", size: 32));
        Assert.IsNull(Run(sliding, "alone.png", frame: 1, size: 32));

        Canvas run = Picture("slide-0000001.png");
        Canvas alone = Picture("alone.png");

        for (int x = 0; x < 32; x++)
        {
            for (int y = 0; y < 32; y++)
                Assert.IsTrue(run.GetPixel(x, y).Matches(alone.GetPixel(x, y)), $"the pixel at {x}, {y}");
        }
    }

    /// <summary>
    /// An animation written to a GIF or a WebP is one movie of all its frames, looping.
    /// </summary>
    [TestMethod]
    public void TestAnAnimationGoesIntoAMovie()
    {
        foreach (string movie in new[] { "turn.gif", "turn.webp" })
        {
            Assert.IsNull(Run(Turning, movie));

            using MagickImageCollection frames = new (Path.Combine(_directory, movie));

            Assert.AreEqual(6, frames.Count, movie);
            Assert.AreEqual(0u, frames[0].AnimationIterations, $"{movie} should loop");
        }

        Assert.AreEqual(0, Directory.GetFiles(_directory, "turn-*").Length, "a movie writes no frames of its own");

        // A GIF can only say a frame lasts 33 hundredths of a second at three a second; a WebP says 333 thousandths.
        using (MagickImageCollection gif = new (Path.Combine(_directory, "turn.gif")))
            Assert.AreEqual(33u, gif[0].AnimationDelay);

        CollectionAssert.AreEqual(Enumerable.Repeat(333, 6).ToArray(),
            WebPDurations(File.ReadAllBytes(Path.Combine(_directory, "turn.webp"))));
    }

    // How long each frame of an animated WebP lasts, in milliseconds, read from its ANMF chunks.
    private static int[] WebPDurations(byte[] data)
    {
        List<int> durations = [];

        for (int at = 12; at + 8 <= data.Length;)
        {
            string tag = System.Text.Encoding.ASCII.GetString(data, at, 4);
            int size = BitConverter.ToInt32(data, at + 4);

            if (tag == "ANMF")
                durations.Add(data[at + 20] | data[at + 21] << 8 | data[at + 22] << 16);

            at += 8 + size + (size & 1);
        }

        return durations.ToArray();
    }

    /// <summary>
    /// An MP4 is written by ffmpeg, found on the path -- an animation's frames, or a still's one -- and asked for
    /// with no ffmpeg to be found, it is refused before anything is rendered.
    /// </summary>
    [TestMethod]
    public void TestAVideoIsWrittenByFfmpegOrRefused()
    {
        string bin = Path.Combine(_directory, "bin");
        string empty = Path.Combine(_directory, "empty");

        Directory.CreateDirectory(bin);
        Directory.CreateDirectory(empty);
        File.WriteAllText(Path.Combine(bin, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg"), "");

        Assert.AreEqual(Path.Combine(bin, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg"),
            MovieFile.FfmpegOn(string.Join(Path.PathSeparator, empty, bin)));
        Assert.IsNull(MovieFile.FfmpegOn(empty));
        StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(() =>
            new MovieFile(Path.Combine(_directory, "none.mp4"), 24, empty)).Message, "no ffmpeg on the path");
        Assert.IsTrue(MovieFile.IsAVideo("a.MP4") && MovieFile.IsAMovie("a.webp") && !MovieFile.IsAMovie("a.png"));

        if (MovieFile.FfmpegOn(Environment.GetEnvironmentVariable("PATH")) is null)
            Assert.Inconclusive("There is no ffmpeg on this machine's path to write a video with.");

        Assert.IsNull(Run(Turning, "turn.mp4"));
        Assert.IsNull(Run("sphere { translate Y 0.5 }", "still.mp4"));

        foreach (string video in new[] { "turn.mp4", "still.mp4" })
        {
            byte[] start = File.ReadAllBytes(Path.Combine(_directory, video))[..12];

            Assert.AreEqual("ftyp", System.Text.Encoding.ASCII.GetString(start, 4, 4), $"{video} is not an MP4");
        }

        Assert.AreEqual(0, Directory.GetFiles(_directory, "turn-*").Length, "a video writes no frames of its own");
    }

    /// <summary>
    /// A scene that is no animation, rendered with no frame asked for, is a still at time nought, as it always was.
    /// </summary>
    [TestMethod]
    public void TestAStillIsAtTimeNought()
    {
        const string still = """
            context { no gamma }
            Tint = keys { 0: color [1, 0, 0]  2: color [0, 0, 1] }
            sphere { material { pigment Tint(time)  ambient 1  diffuse 0 }  translate Y 0.5 }
            """;

        Assert.IsNull(Run(still, "still.png"));
        Assert.AreEqual(1, Directory.GetFiles(_directory, "still*").Length);
        Assert.AreEqual(1, Picture("still.png").GetPixel(8, 8).Red, 0.02);
    }

    /// <summary>
    /// An animation asked for as a still renders the frame it names for that, or its first, written as asked; a
    /// scene that is no animation asked for as a still renders as it always does.
    /// </summary>
    [TestMethod]
    public void TestAnAnimationAskedForAsAStillIsTheFrameItNames()
    {
        Assert.IsNull(Run(Turning.Replace("rate 3", "rate 3  still 4"), "named.png", still: true));
        Assert.AreEqual(1, Directory.GetFiles(_directory, "named*").Length);
        Assert.AreEqual(1 - 4 / 3.0 / 2, Picture("named.png").GetPixel(8, 8).Red, 0.02);

        Assert.IsNull(Run(Turning, "first.png", still: true));
        Assert.AreEqual(1, Picture("first.png").GetPixel(8, 8).Red, 0.02);

        Assert.IsNull(Run("context { no gamma }\nsphere { material { pigment [0, 1, 0]  ambient 1  diffuse 0 } }",
            "plain.png", still: true));
        Assert.AreEqual(1, Directory.GetFiles(_directory, "plain*").Length);

        foreach (string wrong in new[] { "3-1", "a", "1-2-3", "-4" })
        {
            Assert.ThrowsExactly<ArgumentException>(() => new RenderOptions { Frames = wrong }, wrong);
        }

        Assert.AreEqual((2L, 9L), new RenderOptions { Frames = "2-9" }.FrameRun);
        Assert.AreEqual((5L, 5L), new RenderOptions { Frames = "5" }.FrameRun);
    }

    /// <summary>
    /// Counts the pixels of a picture that are partly the ball's, in front of a black ground: neither black nor
    /// the ball's full white.
    /// </summary>
    private static int Smeared(Canvas picture)
    {
        int count = 0;

        for (int x = 0; x < picture.Width; x++)
        {
            for (int y = 0; y < picture.Height; y++)
            {
                double red = picture.GetPixel(x, y).Red;

                if (red is > 0.1 and < 0.9)
                    count++;
            }
        }

        return count;
    }

    /// <summary>
    /// A placement that follows the time is blurred along its way through the frame, as far as the shutter is
    /// open, and a placement that does not is left sharp -- as is the moving one with the shutter shut.
    /// </summary>
    [TestMethod]
    public void TestAPlacementThatFollowsTheTimeIsBlurred()
    {
        string Ball(string place) => $$"""
            context { animation length 1  rate 4  no gamma }
            sphere { material { pigment [1, 1, 1]  ambient 1  diffuse 0 }  scale 0.6  translate X {{place}} }
            """;
        string Camera(double shutter) =>
            $"camera {{ location [0, 0, -6]  look at [0, 0, 0]  shutter {shutter}  blur samples 16 }}";

        Assert.IsNull(Run(Ball("time * 4 - 1.5"), "moving.png", frame: 1, size: 48, camera: Camera(1)));
        Assert.IsNull(Run(Ball("time * 4 - 1.5"), "shut.png", frame: 1, size: 48, camera: Camera(0)));
        Assert.IsNull(Run(Ball("-0.5"), "still.png", frame: 1, size: 48, camera: Camera(1)));

        int moving = Smeared(Picture("moving.png"));
        int shut = Smeared(Picture("shut.png"));
        int still = Smeared(Picture("still.png"));

        Assert.IsTrue(moving > shut * 3, $"blurred, {moving} pixels were part ball, against {shut} with the shutter shut");
        Assert.IsTrue(still < moving / 3, $"a ball holding still smeared {still} pixels");
    }

    /// <summary>
    /// What cannot be animated is refused, with the reason.
    /// </summary>
    [TestMethod]
    public void TestWhatCannotBeAnimatedIsRefused()
    {
        StringAssert.Contains(Run("Bad = keys { 1: 0  1: 2 }  sphere { translate X Bad(time) }", "a.png"),
            "later than the one before");
        StringAssert.Contains(Run("Bad = keys { 0: 0  1: color [1, 0, 0] }  sphere { translate X Bad(time) }", "a.png"),
            "same kind");
        StringAssert.Contains(Run("Bad = keys smooth { 0: 0  1: 2 ease in }", "a.png"), "cannot ease or hold");
        StringAssert.Contains(Run("Bad = keys { }", "a.png"), "at least one key");
        StringAssert.Contains(Run("Bad = keys { 0: 0  1: 2 }  sphere { translate X Bad(time, 2) }", "a.png"),
            "called with one number");
        StringAssert.Contains(Run("context { animation length 0 }", "a.png"), "longer than nothing");
        StringAssert.Contains(Run("context { animation frames 2 }\nrender\nrender", "a.png"), "one render command");
    }
}
