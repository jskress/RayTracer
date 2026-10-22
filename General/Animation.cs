namespace RayTracer.General;

/// <summary>
/// This class holds what a scene says of itself as an animation: how long it runs, in seconds or in frames,
/// and at what rate.  A scene that says nothing of the sort is a still.
/// </summary>
public sealed class Animation
{
    /// <summary>
    /// This is the rate an animation runs at when neither it nor the command line says.
    /// </summary>
    public const double DefaultRate = 24;

    /// <summary>
    /// This property holds how long the animation runs, in seconds, if it was said that way.
    /// </summary>
    public double? Length { get; init; }

    /// <summary>
    /// This property holds how many frames the animation runs, if it was said that way.
    /// </summary>
    public long? Frames { get; init; }

    /// <summary>
    /// This property holds the rate the animation asks to run at, in frames a second, if it says.
    /// </summary>
    public double? Rate { get; init; }

    /// <summary>
    /// This property holds the frame that stands for the animation as a still: the one a gallery shows, and that
    /// `--still` renders.
    /// </summary>
    public long Still { get; init; }

    /// <summary>
    /// This method returns how many frames the animation runs at a rate: as many as it said, or as many as fill
    /// its length, and never none.
    /// </summary>
    public long FrameCount(double rate) => Math.Max(1, Frames ?? (long) Math.Round(Length.GetValueOrDefault() * rate));
}
