namespace RayTracer.PovRay;

/// <summary>
/// This class notes one thing the converter could not bring across, so that the user is told what
/// they did not get rather than left to find out when a scene fails to name it.
/// </summary>
public class PovIssue
{
    /// <summary>
    /// This property holds the file the trouble was in.
    /// </summary>
    public string SourceFile { get; init; }

    /// <summary>
    /// This property holds the line it was on.
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// This property holds the name of the declaration that was passed over, or <c>null</c> when
    /// the trouble was not inside one.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// This property holds why it was passed over, in terms the user can act on.
    /// </summary>
    public string Reason { get; init; }

    public override string ToString() => Name is null
        ? $"{SourceFile}({Line}): {Reason}"
        : $"{SourceFile}({Line}): {Name} -- {Reason}";
}
