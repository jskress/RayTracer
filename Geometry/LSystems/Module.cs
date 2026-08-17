using System.Text;

namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This class represents one module of an L-system word: a letter, and the numbers that travel
/// with it.
/// <para>
/// A module with no numbers is exactly the single character an L-system was made of before any of
/// this existed, which is why there is one kind of word here rather than two.  <c>F</c> and
/// <c>F(1.5)</c> differ only in that the second tells the turtle how far to step; the first leaves
/// it to the <c>length</c> in the rendering controls.
/// </para>
/// </summary>
public class Module
{
    /// <summary>
    /// This property holds the letter this module is written with.
    /// </summary>
    public Rune Letter { get; init; }

    /// <summary>
    /// This property holds the numbers that travel with the letter.  It is empty, never null, for
    /// a module written without any -- which keeps every reader of a word from having to ask.
    /// </summary>
    public double[] Parameters { get; init; } = [];

    /// <summary>
    /// This property reports how many numbers this module carries.  A production only applies to a
    /// module when this agrees with the number of formal parameters the production was written
    /// with, which is what lets <c>F(x)</c> and <c>F(x, t)</c> be two different rules.
    /// </summary>
    public int Arity => Parameters.Length;

    /// <summary>
    /// This property reports the first number, which is the one the turtle reads, or null when the
    /// module carries none and the rendering controls should be used instead.
    /// </summary>
    public double? Primary => Parameters.Length > 0 ? Parameters[0] : null;

    public override string ToString()
    {
        return Parameters.Length == 0
            ? Letter.ToString()
            : $"{Letter}({string.Join(", ", Parameters)})";
    }
}
