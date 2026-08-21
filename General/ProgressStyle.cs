namespace RayTracer.General;

/// <summary>
/// This enum names the ways a render can report how far along it is.
/// </summary>
public enum ProgressStyle
{
    /// <summary>
    /// Report with the coloured bar, for a person watching a terminal.  This is what the ray tracer
    /// has always done and remains the default.
    /// </summary>
    Bar,

    /// <summary>
    /// Report with whole lines of key/value text, for a program watching the render.
    /// </summary>
    Tool,

    /// <summary>
    /// Report nothing at all.
    /// </summary>
    None
}
