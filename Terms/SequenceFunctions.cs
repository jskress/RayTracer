using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class holds the functions a scene uses to make and read a <see cref="Sequence"/>.
/// <para>
/// They are functions rather than syntax of their own, and deliberately: a call already parses with
/// any number of values between its parentheses, so <c>list(a, b, c)</c> needed nothing added to the
/// grammar.  Brackets could not have served -- <c>[x, y, z]</c> is a point by the time it is read,
/// and giving the same brackets a second meaning at five values and up would have made the meaning
/// of a three-value one depend on where it was written.
/// </para>
/// </summary>
public static class SequenceFunctions
{
    /// <summary>
    /// This function gathers its values into a sequence.  Any value will do, a sequence included,
    /// which is how a scene writes a run of things that each have several parts.
    /// </summary>
    /// <param name="values">The values to gather.</param>
    /// <returns>The values, as one thing.</returns>
    [Function("list")]
    public static Sequence List(params object[] values)
    {
        return new Sequence(values);
    }

    /// <summary>
    /// This function returns how many values a sequence holds.
    /// </summary>
    /// <param name="sequence">The sequence to measure.</param>
    /// <returns>How many values it holds.</returns>
    [Function("count")]
    public static double Count(Sequence sequence)
    {
        return sequence.Count;
    }

    /// <summary>
    /// This function returns one value out of a sequence, by its place, counting from nought.
    /// </summary>
    /// <param name="sequence">The sequence to read.</param>
    /// <param name="index">Which value is wanted.</param>
    /// <returns>The value at that place.</returns>
    [Function("item")]
    public static object Item(Sequence sequence, double index)
    {
        int place = (int) Math.Floor(index);

        if (place < 0 || place >= sequence.Count)
        {
            throw new ArgumentException(
                $"There is no item {place} in a list of {sequence.Count}; " +
                $"they run from 0 to {sequence.Count - 1}.");
        }

        return sequence[place];
    }
}
