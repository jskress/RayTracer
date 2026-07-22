namespace RayTracer.PovRay;

/// <summary>
/// This class is the base of everything a POV-Ray declaration may be set to.
/// <para>
/// The library files declare two quite different sorts of thing, and the difference runs through
/// the whole converter.  Numbers and colors are worked out as the file is read, since they are
/// arithmetic and nothing else; <c>golds.inc</c>, for one, derives every one of its finishes from
/// a single base color.  Textures and the blocks they are built from are kept as they were written,
/// since translating them is the emitter's business and it needs to see their shape to do it.
/// </para>
/// </summary>
/// <remarks>
/// Every value is also an item, since a block may hold a bare value as readily as it holds a
/// named property: a color map's entries are colors, and a pigment may be nothing but a color.
/// </remarks>
public abstract class PovValue : IPovItem;

/// <summary>
/// This class represents a POV-Ray number.
/// </summary>
public class PovNumber : PovValue
{
    /// <summary>
    /// This property holds the number.
    /// </summary>
    public double Value { get; init; }

    public override string ToString() => Value.ToString("0.######");
}

/// <summary>
/// This class represents a POV-Ray vector or color.  The two are one type in POV-Ray, a color
/// being a vector with five slots rather than three, and a slot never written is zero.  We keep
/// all five always, so that arithmetic between a vector and a color needs no special case.
/// </summary>
public class PovVector : PovValue
{
    /// <summary>
    /// This constant notes how many slots a POV-Ray vector has: red, green, blue, filter and
    /// transmit, which double as x, y and z for the first three.
    /// </summary>
    public const int Slots = 5;

    /// <summary>
    /// This property holds the vector's slots, in POV-Ray's own order.
    /// </summary>
    public double[] Components { get; init; } = new double[Slots];

    /// <summary>
    /// This property notes whether the value was written as a color rather than as a bare vector.
    /// It carries no arithmetic meaning; it decides only how many slots are worth writing out when
    /// the value is emitted.
    /// </summary>
    public bool IsColor { get; init; }

    /// <summary>
    /// This property provides the red, or X, slot.
    /// </summary>
    public double Red => Components[0];

    /// <summary>
    /// This property provides the green, or Y, slot.
    /// </summary>
    public double Green => Components[1];

    /// <summary>
    /// This property provides the blue, or Z, slot.
    /// </summary>
    public double Blue => Components[2];

    /// <summary>
    /// This property provides the filter slot.
    /// </summary>
    public double Filter => Components[3];

    /// <summary>
    /// This property provides the transmit slot.
    /// </summary>
    public double Transmit => Components[4];

    /// <summary>
    /// This method creates a vector from the given slots, filling any it was not given with zero.
    /// </summary>
    /// <param name="isColor">Whether the value was written as a color.</param>
    /// <param name="components">The slots to fill, in POV-Ray's order.</param>
    /// <returns>The vector those slots make.</returns>
    public static PovVector Of(bool isColor, params double[] components)
    {
        double[] slots = new double[Slots];

        for (int index = 0; index < Math.Min(components.Length, Slots); index++)
            slots[index] = components[index];

        return new PovVector { Components = slots, IsColor = isColor };
    }

    public override string ToString() =>
        $"<{string.Join(", ", Components.Select(value => value.ToString("0.######")))}>";
}

/// <summary>
/// This class represents one of POV-Ray's braced blocks: a texture, pigment, finish, material,
/// interior, normal or map.  A block is kept as it was written rather than being made sense of
/// here, since what can be done with it depends on what the ray tracer can express, which is the
/// emitter's concern rather than the parser's.
/// </summary>
public class PovBlock : PovValue
{
    /// <summary>
    /// This property holds the block's kind: the keyword that introduced it, such as
    /// <c>pigment</c> or <c>color_map</c>.
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    /// This property holds what the block was given, in the order it was written.
    /// </summary>
    public List<IPovItem> Items { get; init; } = [];

    /// <summary>
    /// This property holds the line the block started on, for reporting.
    /// </summary>
    public int Line { get; init; }

    public override string ToString() => $"{Kind} {{ ... }}";
}

/// <summary>
/// This class represents a run of blocks written one after another, which is how POV-Ray writes a
/// layered texture: <c>#declare T_Wood1 = texture { ... } texture { ... }</c>.  Each block after
/// the first lies over the one before it.
/// </summary>
public class PovBlockSequence : PovValue
{
    /// <summary>
    /// This property holds the blocks, in the order they were written, which is bottom layer first.
    /// </summary>
    public List<PovBlock> Blocks { get; init; } = [];

    public override string ToString() => $"{Blocks.Count} layers";
}

/// <summary>
/// This class represents a name standing in for something declared earlier.  Names for numbers and
/// colors are resolved as the file is read, so what is left here always names a block: a pigment
/// borrowed by a texture, a color map borrowed by a pigment, and so on.
/// </summary>
public class PovReference : PovValue
{
    /// <summary>
    /// This property holds the name.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// This property holds the line the name was used on, for reporting.
    /// </summary>
    public int Line { get; init; }

    public override string ToString() => Name;
}
