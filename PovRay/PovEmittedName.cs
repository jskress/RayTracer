namespace RayTracer.PovRay;

/// <summary>
/// This class notes one thing a generated library declares.
/// <para>
/// It is worth keeping for two reasons.  A scene can be built that names every one of them, which
/// is the only way to find out whether the libraries really work: a name nothing defines is not a
/// parsing failure, since names are looked up when the image is made, so a library full of broken
/// references reads perfectly and fails much later.  And two libraries that declare the same name
/// collide silently, last one winning, which is worth catching here rather than leaving for
/// whoever imports both.
/// </para>
/// </summary>
public class PovEmittedName
{
    /// <summary>
    /// This property holds the name the library declares.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// This property holds the name POV-Ray gave it, for reporting.
    /// </summary>
    public string PovName { get; init; }

    /// <summary>
    /// This property holds what sort of thing it is: a material, a pigment, an interior or a
    /// plain value.  What a scene has to write to use it depends on which.
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    /// This property holds the library it was declared in, without any extension.
    /// </summary>
    public string Library { get; init; }

    public override string ToString() => $"{Name} ({Kind}, from {Library})";
}
