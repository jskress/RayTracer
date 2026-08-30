namespace RayTracer.General;

/// <summary>
/// This class holds a run of values that a scene wrote down as one thing.
/// <para>
/// The DSL had no way to carry several of anything about.  A tuple holds two to four numbers and is
/// a point or a color by the time it is read, so it cannot stand for a list; and <c>for</c> counts
/// through a span of numbers rather than walking things.  So anything a scene had several of -- the
/// wings of a building, the stops of a ramp, the places to put a thing -- had to be written out one
/// call at a time, and a library could not be handed the set to work over.
/// </para>
/// <para>
/// A sequence is deliberately plain: it holds values, it says how many, and it gives one back by
/// its place.  It holds <em>any</em> value, a sequence included, and that is what lets a scene write
/// a record -- a run of sequences, each one a thing with several fields -- without the language
/// needing a record of its own.
/// </para>
/// </summary>
public class Sequence
{
    /// <summary>
    /// This property holds the values, in the order they were written.
    /// </summary>
    public IReadOnlyList<object> Values { get; }

    /// <summary>
    /// This property holds how many values there are.
    /// </summary>
    public int Count => Values.Count;

    public Sequence(IEnumerable<object> values)
    {
        Values = values.ToList();
    }

    /// <summary>
    /// This method returns the value at the given place, counting from nought.
    /// </summary>
    /// <param name="index">Which one is wanted.</param>
    /// <returns>The value at that place.</returns>
    public object this[int index] => Values[index];

    /// <summary>
    /// This method returns a description of the sequence, which is what a message about it shows.
    /// </summary>
    /// <returns>The sequence, written out.</returns>
    public override string ToString()
    {
        return $"list({string.Join(", ", Values)})";
    }
}
