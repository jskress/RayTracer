namespace RayTracer.PovRay;

/// <summary>
/// This class represents one thing a POV-Ray file declared, along with where it came from.
/// </summary>
public class PovDeclaration
{
    /// <summary>
    /// This property holds the name the file gave the thing, as POV-Ray spells it.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// This property holds what the name was set to.
    /// </summary>
    public PovValue Value { get; init; }

    /// <summary>
    /// This property holds the name of the file the declaration was written in, which is not
    /// always the file being converted, since a library file may include another.
    /// </summary>
    public string SourceFile { get; init; }

    /// <summary>
    /// This property holds the line the declaration started on.
    /// </summary>
    public int Line { get; init; }

    public override string ToString() => $"{Name} = {Value}";
}

/// <summary>
/// This class represents a POV-Ray include file once it has been read: what it declares, and which
/// other files being converted it leans on.
/// </summary>
public class PovFile
{
    /// <summary>
    /// This property holds the file's name, without its directory, as it was included.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// This property holds the names of the other files being converted that this one includes.
    /// <para>
    /// Only those get a mention.  A file included that is not itself being converted has its
    /// declarations folded into this one instead, since there would otherwise be nothing for a
    /// scene to import them from.
    /// </para>
    /// </summary>
    public List<string> Includes { get; init; } = [];

    /// <summary>
    /// This property notes whether this file becomes a library of its own.  A file that does not
    /// has its declarations folded into whichever file included it, since a scene would otherwise
    /// have nowhere to import them from.
    /// </summary>
    public bool IsSeparateLibrary { get; init; }

    /// <summary>
    /// This property notes whether the file was read for its names alone.
    /// <para>
    /// Such a file is neither emitted nor folded into whatever included it.  <c>consts.inc</c> is
    /// the reason: <c>glass.inc</c> includes it, and it declares things called <c>E</c>, <c>O</c>
    /// and <c>Xy</c>, which are fine as POV-Ray's own shorthand and would be very poor names for a
    /// library to put into a scene's hands.  Its numbers are worked out where they are used, so
    /// nothing is lost by leaving them behind.
    /// </para>
    /// </summary>
    public bool IsPrelude { get; init; }

    /// <summary>
    /// This property holds everything the file declares, in the order it was written, which is the
    /// order it has to be emitted in: a POV-Ray declaration may lean on any declaration before it.
    /// </summary>
    public List<PovDeclaration> Declarations { get; init; } = [];

    public override string ToString() => $"{Name} ({Declarations.Count} declarations)";
}
