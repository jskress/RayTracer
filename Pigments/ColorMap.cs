using RayTracer.Extensions;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This class represents a color map that maps a number from 0 to 1 (or, more specifically,
/// the fractional part of the number) to a color.
/// </summary>
public class ColorMap
{
    private record Entry(double BreakValue, Color Color) : IComparable<Entry>
    {
        /// <summary>
        /// This method is used to keep entries sorted by their break values.
        /// </summary>
        /// <param name="other">The other entry to compare to.</param>
        /// <returns></returns>
        public int CompareTo(Entry other)
        {
            return other == null ? 1 : BreakValue.CompareTo(other.BreakValue);
        }
    }

    private readonly List<Entry> _entries = [];

    /// <summary>
    /// This method is used to add an entry to the color map.
    /// </summary>
    /// <param name="breakValue">The break value that indicates where in the [0. 1] range
    /// that the new color takes effect.</param>
    /// <param name="color">The color to start using at the given break value.</param>
    public void AddEntry(double breakValue, Color color)
    {
        if (breakValue is < 0 or > 1)
            throw new ArgumentException("Break value must be in the [0, 1] interval.");

        _entries.Add(new Entry(breakValue, color));
        _entries.Sort();
    }

    /// <summary>
    /// This method is used to resolve a given number to the proper corresponding color.
    /// </summary>
    /// <param name="value">The value to get the color for.</param>
    /// <returns>The appropriate color for the value.</returns>
    public Color GetColorFor(double value)
    {
        // If we're empty, just go with black.
        if (_entries.IsEmpty())
            return Colors.Black;

        // Otherwise, let's make sure the value is in the [0, 1] range.
        value = value.Fraction();

        if (value < 0)
            value = 1 + value;

        Entry entry = _entries.LastOrDefault(e => value > e.BreakValue) ??
                      _entries.FirstOrDefault();
        int index = _entries.IndexOf(entry) + 1;
        Color firstColor = entry!.Color;

        // If we're on the last entry, that's our color.
        if (index == _entries.Count)
            return firstColor;

        double start = entry.BreakValue;
        double end = _entries[index].BreakValue;
        double fraction = (end - start) * value;
        Color secondColor = _entries[index].Color;

        return firstColor + (secondColor - firstColor) * fraction;
    }
}
