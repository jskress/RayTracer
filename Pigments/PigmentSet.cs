using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This class represents a pigment set that carries a list of pigments, mapped by a number
/// from 0 to 1.  The set may be used as a simple list that returns the color for the nth
/// pigment in the set.  Alternatively, it may take a value and find the interval containing
/// that value and return color for that band or a gradient color for the value, based on
/// where it falls in its interval.
/// </summary>
public class PigmentSet
{
    private record Entry(double BreakValue, Pigment Pigment) : IComparable<Entry>
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
    /// This flag notes whether we will produce bands or gradients.
    /// </summary>
    public bool Banded { get; set; }

    private readonly List<Entry> _entries = [];

    /// <summary>
    /// This method is used to add an entry to the color map.
    /// </summary>
    /// <param name="pigment">The pigment to start using at the given break value.</param>
    /// <param name="breakValue">The break value that indicates where in the [0. 1] range
    /// that the new color takes effect.</param>
    public void AddEntry(Pigment pigment, double breakValue = 0)
    {
        if (breakValue is < 0 or > 1)
            throw new ArgumentException("Break value must be in the [0, 1] interval.");

        _entries.Add(new Entry(breakValue, pigment));
        _entries.Sort();
    }

    /// <summary>
    /// This method is used to resolve a given index number to the proper corresponding color.
    /// </summary>
    /// <param name="point">The point to get the color for.</param>
    /// <param name="index">The index of the desired color.</param>
    /// <returns>The appropriate color for the value.</returns>
    public Color GetColorFor(Point point, int index)
    {
        return _entries[index].Pigment.GetTransformedColorFor(point);
    }

    /// <summary>
    /// This method is used to resolve a given number to the proper corresponding color.
    /// </summary>
    /// <param name="point">The point to get the color for.</param>
    /// <param name="value">The value to get the color for.</param>
    /// <returns>The appropriate color for the value.</returns>
    public Color GetColorFor(Point point, double value)
    {
        // If we're empty, just go with black.
        if (_entries.IsEmpty())
            return Colors.Black;

        // Otherwise, let's make sure the value is in the [0, 1] range.
        value = value.Fraction();

        if (value < 0)
            value = 1 + value;

        Entry entry = _entries.LastOrDefault(e => value >= e.BreakValue) ??
                      _entries.FirstOrDefault();
        int index = _entries.IndexOf(entry) + 1;
        Color firstColor = entry!.Pigment.GetTransformedColorFor(point);

        // If we're banded or on the last entry, then we have our color.
        if (Banded || index == _entries.Count)
            return firstColor;

        double start = entry.BreakValue;
        double end = _entries[index].BreakValue;
        double fraction = (end - start) * value;
        Color secondColor = _entries[index].Pigment.GetTransformedColorFor(point);
        double alpha = firstColor.Alpha + (secondColor.Alpha - firstColor.Alpha) * fraction;

        return (firstColor + (secondColor - firstColor) * fraction).WithAlpha(alpha);
    }

    /// <summary>
    /// This method returns whether the given pigment set matches this one.
    /// </summary>
    /// <param name="other">The pigment set to compare to.</param>
    /// <returns><c>true</c>, if the two pigment sets match, or <c>false</c>, if not.</returns>
    public bool Matches(PigmentSet other)
    {
        if (Banded != other.Banded || _entries.Count != other._entries.Count)
            return false;

        for (int index = 0; index < _entries.Count; index++)
        {
            Entry thisEntry = _entries[index];
            Entry otherEntry = other._entries[index];

            if (!thisEntry.BreakValue.Near(otherEntry.BreakValue) ||
                !thisEntry.Pigment.Matches(otherEntry.Pigment))
                return false;
        }

        return true;
    }
}
