using System.Collections;
using RayTracer.Extensions;

namespace RayTracer.Basics;

/// <summary>
/// This class provides a collection of values, each associated with a "break" value in
/// the [0, 1] interval.
/// </summary>
public class Spectrum<T> : IEnumerable<T>
{
    /// <summary>
    /// This record defines a record in our collection.  We have this, rather than using
    /// something like <c>KeyValuePair</c>, to provide the sorting we want.
    /// </summary>
    /// <param name="BreakValue"></param>
    /// <param name="Value"></param>
    private record Entry(double BreakValue, T Value) : IComparable<Entry>
    {
        /// <summary>
        /// This method is used to keep entries sorted by their break values.
        /// </summary>
        /// <param name="other">The other entry to compare to.</param>
        /// <returns>The appropriate result of comparing the break values for ordering.</returns>
        public int CompareTo(Entry other)
        {
            return other == null ? 1 : BreakValue.CompareTo(other.BreakValue);
        }
    }

    /// <summary>
    /// This property reports the number of entries we carry.
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// This property notes whether the spectrum is empty.
    /// </summary>
    public bool IsEmpty => _entries.IsEmpty();
    
    private readonly List<Entry> _entries = [];

    /// <summary>
    /// This method is used to add an entry to the spectrum.
    /// </summary>
    /// <param name="value">The value to start using at the given break value.</param>
    /// <param name="breakValue">The break value that indicates where in the [0. 1] range
    /// that the new color takes effect.</param>
    public void AddEntry(T value, double breakValue = 0)
    {
        if (breakValue is < 0 or > 1)
            throw new ArgumentException("Break value must be in the [0, 1] interval.");

        _entries.Add(new Entry(breakValue, value));
        _entries.Sort();
    }

    /// <summary>
    /// This method returns a value from the spectrum based on its index.
    /// </summary>
    /// <param name="index">The index of the desired value.</param>
    /// <returns>The value at the given index.</returns>
    public (double, T) GetByIndex(int index)
    {
        Entry entry = _entries[index];

        return (entry.BreakValue, entry.Value);
    }

    /// <summary>
    /// This method brings a number into the [0, 1) interval the break values live in.
    /// </summary>
    /// <param name="number">The number to normalize.</param>
    /// <returns>The number, brought into the [0, 1) interval.</returns>
    public static double Normalize(double number)
    {
        return number.Fraction();
    }

    /// <summary>
    /// This method returns a value from the spectrum based on a number.
    /// </summary>
    /// <param name="number">The numb to use in determining the desired value.</param>
    /// <returns>The value for the given number and its break value.</returns>
    public (double, T) GetByValue(double number)
    {
        int index = GetIndexByValue(number);

        return index < 0 ? (double.NaN, default) : GetByIndex(index);
    }

    /// <summary>
    /// This method returns the index of the entry that governs the given number: the last one
    /// whose break value the number has reached, or the first entry if it has reached none of
    /// them.
    ///
    /// Callers wanting the entry *after* this one should ask for the next index, rather than
    /// searching back for this entry's value: nothing stops the same value appearing at several
    /// break values (a colour map naming one pigment at every stop, say), and a search by value
    /// can't tell those apart -- it would find the first of them every time, and so report a
    /// break value belonging to some entirely different stop.
    /// </summary>
    /// <param name="number">The number to find the governing entry for.</param>
    /// <returns>The index of the governing entry, or -1 if we carry no entries at all.</returns>
    public int GetIndexByValue(double number)
    {
        if (_entries.IsEmpty())
            return -1;

        number = Normalize(number);

        for (int index = _entries.Count - 1; index >= 0; index--)
        {
            if (number >= _entries[index].BreakValue)
                return index;
        }

        return 0;
    }

    /// <summary>
    /// This method returns an enumeration over the values we carry.  The values are
    /// returned in the order indicated by their break values.
    /// </summary>
    /// <returns>An enumeration over the values we carry.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        return _entries.Select(entry => entry.Value).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
