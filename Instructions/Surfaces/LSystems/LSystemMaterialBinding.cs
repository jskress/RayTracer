using System.Text;

namespace RayTracer.Instructions.Surfaces.LSystems;

/// <summary>
/// This class ties one character to the material an L-system should draw with once its production
/// reaches that character -- so a rune may turn a stem from bark to green where a new shoot starts.
/// <para>
/// It differs from <see cref="LSystemSurfaceBinding"/> in what it binds to and in how long it
/// lasts.  A surface binding stamps one thing down and is done; a material binding changes the
/// state the turtle draws in, and holds until something changes it again or the branch it was made
/// in closes.  It holds a resolver rather than a material because resolving a named material needs
/// a render context that is only in hand at resolve time.
/// </para>
/// </summary>
public class LSystemMaterialBinding
{
    /// <summary>
    /// This property holds the character that names the material in a production.  It is unused
    /// when <see cref="IsByDepth"/> reports that this binding is keyed on depth instead.
    /// </summary>
    public Rune Character { get; init; }

    /// <summary>
    /// This property holds the branching depth the material stands for, when the binding is by
    /// depth rather than by character, and <c>-1</c> otherwise.
    /// </summary>
    public int Depth { get; init; } = -1;

    /// <summary>
    /// This property holds the resolver for the material being bound.
    /// </summary>
    public MaterialResolver Resolver { get; init; }

    /// <summary>
    /// This property reports whether this binding is keyed on branching depth rather than on a
    /// character in the production.
    /// </summary>
    public bool IsByDepth => Depth >= 0;
}
