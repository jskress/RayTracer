namespace RayTracer.PovRay;

/// <summary>
/// This class represents a named property inside a POV-Ray block: a keyword and whatever followed
/// it, such as <c>turbulence 0.4</c>, <c>scale &lt;0.05, 0.05, 1&gt;</c> or a bare <c>granite</c>.
/// </summary>
public class PovProperty : IPovItem
{
    /// <summary>
    /// This property holds the keyword that named the property.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// This property holds what followed the keyword, which is very often nothing, since many of
    /// POV-Ray's properties are the keyword alone.
    /// </summary>
    public List<PovValue> Values { get; init; } = [];

    /// <summary>
    /// This property holds the line the property was written on, for reporting.
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// This property provides the property's one value, or <c>null</c> when it was given none or
    /// more than one.
    /// </summary>
    public PovValue Value => Values.Count == 1 ? Values[0] : null;

    public override string ToString() => Values.Count == 0
        ? Name
        : $"{Name} {string.Join(" ", Values)}";
}

/// <summary>
/// This class represents one entry in a POV-Ray map.
/// <para>
/// POV-Ray writes map entries two ways.  The modern one gives a single place and a single value,
/// <c>[0.3 color rgb &lt;...&gt;]</c>, and the older one gives a band and the values at each end,
/// <c>[0.0, 0.153 color rgb &lt;...&gt; color rgb &lt;...&gt;]</c>.  The stones and woods files are
/// full of the older form, so both are kept as written and reconciled when they are emitted.
/// </para>
/// </summary>
public class PovMapEntry : IPovItem
{
    /// <summary>
    /// This property holds the places the entry covers: one for the modern form and two for the
    /// older banded one.
    /// </summary>
    public List<double> Stops { get; init; } = [];

    /// <summary>
    /// This property holds the values at those places, in the same order.
    /// </summary>
    public List<PovValue> Values { get; init; } = [];

    /// <summary>
    /// This property holds the line the entry was written on, for reporting.
    /// </summary>
    public int Line { get; init; }

    public override string ToString() =>
        $"[{string.Join(", ", Stops)} {string.Join(" ", Values)}]";
}
