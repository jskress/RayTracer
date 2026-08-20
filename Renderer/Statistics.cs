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
