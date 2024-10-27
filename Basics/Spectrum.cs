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
    /// This method returns a value from the spectrum based on a number.
    /// </summary>
    /// <param name="number">The numb to use in determining the desired value.</param>
    /// <returns>The value for the given number and its break value.</returns>
    public (double, T) GetByValue(double number)
    {
        // First, let's make sure the number is in the [0, 1] range.
        number = number.Fraction();

        if (number < 0)
            number = 1 + number;

        Entry entry = _entries.LastOrDefault(e => number >= e.BreakValue) ??
                      _entries.FirstOrDefault();

        return entry == null
            ? (double.NaN, default)
            : (entry.BreakValue, entry.Value);
    }

    /// <summary>
    /// This method returns the value that follows the specified one.
    /// </summary>
    /// <param name="value">The value that precedes the desired value.</param>
    /// <returns>The value for the given number and its break value.</returns>
    public (double, T) GetValueFollowing(T value)
    {
        for (int index = 0; index < _entries.Count - 1; index++)
        {
            if (_entries[index].Equals(value))
            {
                Entry entry = _entries[index + 1];

                return (entry.BreakValue, entry.Value);
            }
        }

        return (double.NaN, default);
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
