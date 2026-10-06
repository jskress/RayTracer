using System.Globalization;
using RayTracer.Core;

namespace RayTracer.Renderer;

/// <summary>
/// This class carries the counts a render can report about itself.
/// <para>
/// The counts exist to answer a question that is otherwise very hard to answer: <em>where did the
/// time go?</em>  A scene that takes an hour when its own arithmetic said ten minutes might be doing
/// far more work than expected, or the same work far more slowly, and those two want opposite
/// responses.  Samples against pixels tells anti-aliasing's appetite from the picture's; scene rays
/// against samples tells how much shading each of those samples went on to cost.
/// </para>
/// <para>
/// <b>They are counted per thread and added up at the end</b>, rather than by incrementing one shared
/// number.  A scanner fires millions of rays across every core, and a single interlocked counter on
/// that path would have every thread queueing behind the same cache line -- so the instrument would
/// change what it was measuring.  Each thread keeps to its own slot, and the slots are spaced far
/// enough apart to sit in different cache lines.
/// </para>
/// <para>
/// Two threads may still land on the same slot, so each slot is incremented atomically and the
/// counts come out exact.  That is worth having: a count that quietly loses some of what it was
/// counting is a poor thing to reason from, and the atomic increment costs a few nanoseconds beside
/// a ray that costs hundreds -- see <c>TestStatistics</c> for what the cost was measured at.
/// </para>
/// </summary>
public class Statistics
{
    // Sixteen longs is 128 bytes, comfortably more than any cache line in use, so two threads
    // writing to neighboring slots never fight over the same one.
    private const int Stride = 16;

    private readonly int _slots;
    private readonly long[] _pixels;
    private readonly long[] _samples;
    private readonly long[] _primaryRays;
    private readonly long[] _sceneRays;

    public Statistics() : this(Environment.ProcessorCount * 4) {}

    /// <summary>
    /// Creates a set of counts striped a given number of ways.  Four stripes per processor is what a
    /// render uses, and is enough that threads mostly keep out of each other's way.
    /// <para>
    /// The number is worth being able to say for one reason: asking for a single stripe puts every
    /// thread on the same counter, which is the only way to make the contention certain rather than
    /// merely likely, and so the only way to have a test that says what happens under it.
    /// </para>
    /// </summary>
    /// <param name="slots">How many stripes to keep the counts in.</param>
    public Statistics(int slots)
    {
        _slots = Math.Max(1, slots);
        _pixels = new long[_slots * Stride];
        _samples = new long[_slots * Stride];
        _primaryRays = new long[_slots * Stride];
        _sceneRays = new long[_slots * Stride];
    }

    /// <summary>
    /// This property reports how many pixels were finished.
    /// </summary>
    public long Pixels => Total(_pixels);

    /// <summary>
    /// This property reports how many places within pixels were evaluated.  With no anti-aliasing
    /// this equals the pixel count; with adaptive super-sampling it is the interesting number, since
    /// each pixel takes five samples and each of four corners may recurse -- so one pixel can cost
    /// thousands of them where the picture has fine detail in it.
    /// </summary>
    public long Samples => Total(_samples);

    /// <summary>
    /// This property reports how many rays left the camera.  It exceeds the sample count wherever a
    /// sampler takes more than one ray per sample, as focal blur and motion blur both do.
    /// </summary>
    public long PrimaryRays => Total(_primaryRays);

    /// <summary>
    /// This property reports how many rays were put to the scene altogether.  This is the closest
    /// thing to a measure of the work a render actually did: besides the camera's own rays it counts
    /// every shadow ray each light sample needed, every reflection and refraction, and every step
    /// taken through a participating medium.
    /// </summary>
    public long SceneRays => Total(_sceneRays);

    /// <summary>
    /// This property reports the average number of samples each pixel cost.  One means anti-aliasing
    /// did nothing, five means the adaptive renderer never needed to subdivide, and a large number
    /// means it subdivided nearly everywhere.
    /// </summary>
    public double SamplesPerPixel => Ratio(Samples, Pixels);

    /// <summary>
    /// This property reports the average number of scene rays each sample turned into, which is what
    /// the lights, the reflections and any medium cost between them.
    /// </summary>
    public double SceneRaysPerSample => Ratio(SceneRays, Samples);

    /// <summary>
    /// This method counts one finished pixel.
    /// </summary>
    public void CountPixel()
    {
        Interlocked.Increment(ref _pixels[Slot()]);
    }

    /// <summary>
    /// This method counts one sample, and the camera rays it took.
    /// </summary>
    /// <param name="rays">How many rays that sample fired.</param>
    public void CountSample(int rays)
    {
        int slot = Slot();

        Interlocked.Increment(ref _samples[slot]);
        Interlocked.Add(ref _primaryRays[slot], rays);
    }

    /// <summary>
    /// This method counts one ray put to the scene.
    /// </summary>
    public void CountSceneRay()
    {
        Interlocked.Increment(ref _sceneRays[Slot()]);
    }

    /// <summary>
    /// This method hands a thread its own slot.
    /// </summary>
    private int Slot()
    {
        return Environment.CurrentManagedThreadId % _slots * Stride;
    }

    private long Total(long[] counters)
    {
        long total = 0;

        for (int index = 0; index < _slots; index++)
            total += Interlocked.Read(ref counters[index * Stride]);

        return total;
    }

    private static double Ratio(long numerator, long denominator)
    {
        return denominator == 0 ? 0 : (double) numerator / denominator;
    }

    /// <summary>
    /// This property holds what the scene held once it was ready to render, or null before then.
    /// </summary>
    public SceneCensus Census { get; private set; }

    /// <summary>
    /// This property holds the width of the picture rendered, in pixels.
    /// </summary>
    public int Width { get; private set; }

    /// <summary>
    /// This property holds the height of the picture rendered, in pixels.
    /// </summary>
    public int Height { get; private set; }

    /// <summary>
    /// This property reports how long the rendering itself took: from the first pixel to the last,
    /// leaving out reading the scene and building it.
    /// </summary>
    public TimeSpan RenderTime { get; private set; }

    /// <summary>
    /// This method notes what the scene about to be rendered holds, and the size of the picture it
    /// is rendered at.
    /// </summary>
    /// <param name="scene">The scene, ready to render.</param>
    /// <param name="width">The picture's width, in pixels.</param>
    /// <param name="height">The picture's height, in pixels.</param>
    public void Describe(Scene scene, int width, int height)
    {
        Census = SceneCensus.Of(scene);
        Width = width;
        Height = height;
    }

    /// <summary>
    /// This method adds the time a render took to the time rendering has taken.
    /// </summary>
    /// <param name="time">How long the render took.</param>
    public void AddRenderTime(TimeSpan time)
    {
        RenderTime += time;
    }

    /// <summary>
    /// This method writes everything out for a person to read: what the render cost, and what the
    /// scene held, laid out as a short report.
    /// </summary>
    /// <param name="title">What the report is of, usually the scene file's name.</param>
    /// <returns>The report's lines.</returns>
    public List<string> AsReport(string title)
    {
        List<string> lines = [$"Statistics for {title}", ""];
        double seconds = RenderTime.TotalSeconds;

        if (Width > 0)
            AddLine(lines, "Image", $"{Width} x {Height}, {Plural(Pixels, "pixel")}");

        AddLine(lines, "Samples", $"{Number(Samples)}, {Fraction(SamplesPerPixel)} a pixel");
        AddLine(lines, "Rays",
            $"{Number(SceneRays)}, {Fraction(SceneRaysPerSample)} a sample, {Number(PrimaryRays)} of them " +
            "from the camera");

        if (seconds > 0)
        {
            AddLine(lines, "Rendering",
                $"{Duration(RenderTime)}, {Number((long) Math.Round(SceneRays / seconds))} rays a second");
        }

        if (Census is not null)
            AddScene(lines, Census);

        return lines;
    }

    /// <summary>
    /// This method adds what the scene held to a report.
    /// </summary>
    /// <param name="lines">The report so far.</param>
    /// <param name="census">What the scene held.</param>
    private static void AddScene(List<string> lines, SceneCensus census)
    {
        List<KeyValuePair<string, long>> kinds = census.Surfaces
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .ToList();
        long surfaces = kinds.Sum(pair => pair.Value);

        lines.Add("");

        if (surfaces == 0)
            AddLine(lines, "Surfaces", "none");
        else
        {
            AddLine(lines, "Surfaces", $"{Number(surfaces)}, of {Plural(kinds.Count, "kind")}");

            int nameWidth = kinds.Max(pair => pair.Key.Length);
            int countWidth = kinds.Max(pair => Number(pair.Value).Length);

            foreach ((string kind, long count) in kinds)
            {
                // A kind built from pieces of its own says how many, which is what it costs to trace.
                string pieces = census.Pieces.TryGetValue(kind, out long made) && made > count
                    ? $"  ({Plural(made, "piece")})"
                    : "";

                lines.Add($"      {kind.PadRight(nameWidth)}  {Number(count).PadLeft(countWidth)}{pieces}");
            }
        }

        List<string> combinations = [];

        if (census.Unions > 0)
            combinations.Add(Plural(census.Unions, "union"));

        if (census.Intersections > 0)
            combinations.Add(Plural(census.Intersections, "intersection"));

        if (census.Differences > 0)
            combinations.Add(Plural(census.Differences, "difference"));

        if (combinations.Count > 0)
            AddLine(lines, "Combined", Joined(combinations));

        if (census.SharedShapes > 0)
        {
            AddLine(lines, "Shared",
                $"{Plural(census.SharedShapes, "shape")}, shown {Plural(census.SharedUses, "time")}");
        }

        List<string> lights = census.Lights
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => Plural(pair.Value, pair.Key))
            .ToList();

        AddLine(lines, "Lights", lights.Count == 0
            ? "none"
            : $"{Number(census.Lights.Values.Sum())}: {Joined(lights)}");
    }

    /// <summary>
    /// This method adds one labeled line to a report.
    /// </summary>
    private static void AddLine(List<string> lines, string label, string value)
    {
        lines.Add($"  {label,-12}{value}");
    }

    /// <summary>
    /// This method writes a count with its separators, the same way wherever the program runs.
    /// </summary>
    private static string Number(long value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// This method writes a ratio to two places.
    /// </summary>
    private static string Fraction(double value)
    {
        return value.ToString("F2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// This method writes a count of something, with the word for it made plural as it needs.
    /// </summary>
    private static string Plural(long count, string word)
    {
        return $"{Number(count)} {word}{(count == 1 ? "" : "s")}";
    }

    /// <summary>
    /// This method joins a list the way a sentence would: "a, b and c".
    /// </summary>
    private static string Joined(List<string> items)
    {
        return items.Count == 1
            ? items[0]
            : $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}";
    }

    /// <summary>
    /// This method writes a length of time in words, to a tenth of a second once it runs to minutes.
    /// </summary>
    private static string Duration(TimeSpan time)
    {
        if (time.TotalSeconds < 60)
            return $"{time.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture)} seconds";

        double seconds = time.Seconds + time.Milliseconds / 1000.0;
        List<string> parts = [];

        if (time.TotalHours >= 1)
            parts.Add(Plural((long) time.TotalHours, "hour"));

        parts.Add(Plural(time.Minutes, "minute"));
        parts.Add($"{seconds.ToString("F1", CultureInfo.InvariantCulture)} seconds");

        return Joined(parts);
    }

    /// <summary>
    /// This method writes the counts out as one line of key/value text, in the same shape the tool
    /// progress reporter uses so that one reader can make sense of both.
    /// </summary>
    /// <returns>The counts, as a line of text.</returns>
    public string AsText()
    {
        return $"statistics pixels={Pixels} samples={Samples} primaryRays={PrimaryRays} " +
               $"sceneRays={SceneRays} samplesPerPixel={SamplesPerPixel:F2} " +
               $"sceneRaysPerSample={SceneRaysPerSample:F2}";
    }
}
