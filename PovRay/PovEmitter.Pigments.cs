namespace RayTracer.PovRay;

/// <summary>
/// This class turns what was read from POV-Ray's include files into a library.
/// </summary>
public partial class PovEmitter
{
    /// <summary>
    /// These are the patterns both languages have, under the names each of them uses.  A pattern
    /// missing from here is one the ray tracer has no answer for, and a texture built on one is
    /// passed over rather than quietly turned into something else.
    /// </summary>
    private static readonly Dictionary<string, string> Patterns = new()
    {
        ["agate"] = "agate",
        ["boxed"] = "boxed",
        ["bozo"] = "bozo",
        // POV-Ray's "bumps" is the very same function as its "bozo" -- rendering the two over the
        // same points gives pixel-for-pixel the same picture -- so it comes across as bozo rather
        // than as a pattern of its own saying the same thing twice.
        ["bumps"] = "bozo",
        ["brick"] = "brick",
        ["checker"] = "checker",
        ["crackle"] = "crackle",
        ["cubic"] = "cubic",
        ["dents"] = "dents",
        ["granite"] = "granite",
        ["hexagon"] = "hexagon",
        ["leopard"] = "leopard",
        ["marble"] = "marble",
        ["planar"] = "planar",
        ["radial"] = "radial",
        ["ripples"] = "ripples",
        ["waves"] = "waves",
        ["wood"] = "wood",
        ["wrinkles"] = "wrinkles"
    };

    /// <summary>
    /// These are the things POV-Ray may say about how a pattern is stirred up, under the names the
    /// ray tracer's turbulence block uses.  POV-Ray's <c>lambda</c> is how much finer each octave
    /// is than the one before, and its <c>omega</c> is how much fainter, which is what those two
    /// are called here.
    /// </summary>
    private static readonly Dictionary<string, string> TurbulenceProperties = new()
    {
        ["octaves"] = "octaves",
        ["lambda"] = "finer",
        ["omega"] = "fainter"
    };

    /// <summary>
    /// This class gathers up everything that goes into one pigment.
    /// <para>
    /// It exists because a POV-Ray pigment is very often written in pieces that have to be put
    /// back together: <c>pigment { P_WoodGrain1A color_map { M_Wood1A } }</c> takes its pattern
    /// and its stirring from one declaration and its colors from another, and neither of them is
    /// a pigment the ray tracer could express on its own.
    /// </para>
    /// </summary>
    private class PigmentParts
    {
        public string Pattern { get; set; }
        public PovVector SolidColor { get; set; }
        public List<PovMapEntry> Map { get; } = [];
        public List<PovProperty> Turbulence { get; } = [];
        public List<PovProperty> Shaping { get; } = [];
        public List<PovProperty> Transforms { get; } = [];
    }

    /// <summary>
    /// This method writes out a declaration whose value is a pigment.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="declaration">The declaration being written.</param>
    /// <param name="block">The pigment block it was set to.</param>
    private void EmitPigmentDeclaration(
        IglWriter writer, PovDeclaration declaration, PovBlock block)
    {
        PigmentParts parts = new PigmentParts();

        CollectPigment(block, parts);

        string name = PovNames.From(declaration.Name, "Pigment");

        WritePigment(writer, $"{name} = pigment ", string.Empty, parts, block.Line);

        // Noted only once the pigment is safely written, since writing it is what may fail.
        Note(declaration, "pigment", "Pigment");
    }

    /// <summary>
    /// This method gathers everything one pigment is made of, following any names it is built from
    /// and folding what they hold into the same pile.
    /// </summary>
    /// <param name="value">The pigment, or a name standing for one.</param>
    /// <param name="parts">The pile to add to.</param>
    private void CollectPigment(PovValue value, PigmentParts parts)
    {
        switch (Resolve(value))
        {
            case PovVector color:
                parts.SolidColor = color;

                return;

            case PovBlock block:
                foreach (IPovItem item in block.Items)
                    CollectPigmentItem(item, parts);

                return;

            case PovNumber:
                throw new PovEmitException("A pigment was given a number.", 0);
        }
    }

    /// <summary>
    /// This method takes one thing out of a pigment block and puts it where it belongs.
    /// </summary>
    /// <param name="item">The item to place.</param>
    /// <param name="parts">The pile to add to.</param>
    private void CollectPigmentItem(IPovItem item, PigmentParts parts)
    {
        switch (item)
        {
            case PovMapEntry entry:
                parts.Map.Add(entry);

                break;

            case PovBlock block when IngredientBlocks.Contains(block.Kind):
                CollectMap(block, parts);

                break;

            case PovBlock block when block.Kind == "pigment":
                CollectPigment(block, parts);

                break;

            case PovBlock block when block.Kind is "warp" or "turbulence":
                // A warp bends space before the pattern is read.  Turbulence is the one kind of it
                // we have, so that much comes across and the rest does not.
                foreach (IPovItem inner in block.Items)
                    CollectPigmentItem(inner, parts);

                break;

            case PovBlock block:
                throw new PovEmitException(
                    $"We cannot express a \"{block.Kind}\" inside a pigment.", block.Line);

            case PovReference reference:
                CollectPigment(reference, parts);

                break;

            case PovVector color:
                parts.SolidColor = color;

                break;

            case PovProperty property:
                CollectPigmentProperty(property, parts);

                break;
        }
    }

    /// <summary>
    /// This method takes one named property out of a pigment block and puts it where it belongs.
    /// </summary>
    /// <param name="property">The property to place.</param>
    /// <param name="parts">The pile to add to.</param>
    private static void CollectPigmentProperty(PovProperty property, PigmentParts parts)
    {
        if (Patterns.TryGetValue(property.Name, out string pattern))
        {
            parts.Pattern = pattern;

            return;
        }

        switch (property.Name)
        {
            case "gradient":
                // POV-Ray says which way a gradient runs with a vector; the ray tracer names the
                // axis, so only a gradient along one of them can come across.
                parts.Pattern = property.Value is PovVector direction
                    ? $"linear {AxisOf(direction, property.Line)} gradient"
                    : throw new PovEmitException("A gradient was given no direction.", property.Line);

                break;

            case "turbulence":
            case "octaves":
            case "lambda":
            case "omega":
                parts.Turbulence.Add(property);

                break;

            case "scale":
            case "rotate":
            case "translate":
                parts.Transforms.Add(property);

                break;

            case "matrix":
            case "transform":
                throw new PovEmitException(
                    $"We cannot express a \"{property.Name}\" on a pigment.", property.Line);

            case "frequency":
            case "phase":
            case "ramp_wave":
            case "triangle_wave":
            case "sine_wave":
            case "scallop_wave":
            case "cubic_wave":
            case "poly_wave":
                // These shape a pattern's value once the pattern has produced it, and the ray
                // tracer has all of them under the same names.
                parts.Shaping.Add(property);

                break;

            default:
                throw new PovEmitException(
                    $"We cannot express \"{property.Name}\" in a pigment.", property.Line);
        }
    }

    /// <summary>
    /// This method gathers the entries of a color map, following a name where the map is nothing
    /// but one, which is how woods.inc writes every one of its maps.
    /// </summary>
    /// <param name="block">The map block.</param>
    /// <param name="parts">The pile to add to.</param>
    private void CollectMap(PovBlock block, PigmentParts parts)
    {
        foreach (IPovItem item in block.Items)
        {
            switch (item)
            {
                case PovMapEntry entry:
                    parts.Map.Add(entry);

                    break;

                case PovReference reference when Resolve(reference) is PovBlock named:
                    CollectMap(named, parts);

                    break;

                default:
                    throw new PovEmitException("A color map holds something we cannot read.", block.Line);
            }
        }
    }

    /// <summary>
    /// This method writes one pigment out.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="prefix">Everything that comes before the pattern's name.  The word "pigment"
    /// belongs to whatever the pigment is being written into rather than to the pigment itself: a
    /// material says it before its pigment, and a layer, whose entries are already pigments, says
    /// nothing at all.</param>
    /// <param name="suffix">What follows the pigment, which is a comma between the entries of a
    /// layer and nothing anywhere else.</param>
    /// <param name="parts">Everything the pigment is made of.</param>
    /// <param name="line">The line the pigment came from, for reporting.</param>
    private void WritePigment(
        IglWriter writer, string prefix, string suffix, PigmentParts parts, int line)
    {
        if (parts.Pattern is null)
        {
            if (parts.SolidColor is null)
                throw new PovEmitException("A pigment says neither a color nor a pattern.", line);

            (double alpha, _) = Transparency(parts.SolidColor);

            writer.Line(
                $"{prefix}color " +
                IglWriter.Color(parts.SolidColor.Red, parts.SolidColor.Green, parts.SolidColor.Blue, alpha) +
                suffix);

            return;
        }

        List<(double Stop, PovVector Color)> stops = FlattenMap(parts.Map, line);

        // A pattern with nothing to color is exactly what POV-Ray's woodgrain pigments are: they
        // are meant to be handed a map later.  There is nothing to hand one here, so a pigment
        // that reaches us still wanting its colors is one we cannot write.
        if (stops.Count < 2)
        {
            throw new PovEmitException(
                "A pattern needs at least two colors, and this one was left to be given them later.",
                line);
        }

        writer.Open($"{prefix}{parts.Pattern}");

        WriteTurbulence(writer, parts.Turbulence);
        WriteShaping(writer, parts.Shaping);

        writer.Line($"[{string.Join(", ", stops.Select(stop => $"{IglWriter.Number(stop.Stop)}, {ColorText(stop.Color)}"))}]");

        WriteTransforms(writer, parts.Transforms);

        writer.Close(suffix);
    }

    /// <summary>
    /// This method turns POV-Ray's map entries into the run of places and colors the ray tracer
    /// wants.
    /// <para>
    /// The older way of writing a map gives a band and the color at each end of it, and the bands
    /// run into one another, so the color ending one band is the color starting the next and would
    /// otherwise be written twice.  The newer way gives one place and one color.  Both end up as
    /// the same list of places here, with the repeats taken out.
    /// </para>
    /// </summary>
    /// <param name="entries">The map entries as POV-Ray wrote them.</param>
    /// <param name="line">The line the map came from, for reporting.</param>
    /// <returns>The places and the colors at them.</returns>
    private List<(double Stop, PovVector Color)> FlattenMap(List<PovMapEntry> entries, int line)
    {
        List<(double Stop, PovVector Color)> result = [];

        foreach (PovMapEntry entry in entries)
        {
            for (int index = 0; index < entry.Stops.Count && index < entry.Values.Count; index++)
            {
                if (Resolve(entry.Values[index]) is not PovVector color)
                    throw new PovEmitException("A color map holds something that is not a color.", entry.Line);

                // POV-Ray's own maps run a shade past the end -- the stone textures all finish at
                // 1.001 -- where the ray tracer wants a place within the pattern's range.  The
                // overshoot is POV-Ray making sure its last band is not missed rather than anything
                // meant to be seen, so bringing it back to the end loses nothing.
                double stop = Math.Clamp(entry.Stops[index], 0, 1);

                if (result.Count > 0 && Math.Abs(result[^1].Stop - stop) < 1e-9)
                    continue;

                result.Add((stop, color));
            }
        }

        if (result.Count == 0 && entries.Count > 0)
            throw new PovEmitException("A color map gave no colors we could read.", line);

        return result;
    }

    /// <summary>
    /// This method writes a color as a pigment, which is how the ray tracer writes the entries of
    /// a map.
    /// </summary>
    /// <param name="color">The color to write.</param>
    /// <returns>The color, written.</returns>
    private static string ColorText(PovVector color)
    {
        (double alpha, _) = Transparency(color);

        return IglWriter.Color(color.Red, color.Green, color.Blue, alpha);
    }

    /// <summary>
    /// This method writes out how a pattern is stirred up, if it is.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="properties">What POV-Ray said about the stirring.</param>
    private static void WriteTurbulence(IglWriter writer, List<PovProperty> properties)
    {
        List<string> parts = [];

        foreach (PovProperty property in properties)
        {
            if (property.Name == "turbulence")
            {
                // Both languages let turbulence differ by axis, so a vector is carried across as
                // one rather than averaged.  Averaging is not merely less exact here, it is
                // ruinous: the wood pigments ask for turbulence of <0.1, 0.04, 1>, meaning a
                // gentle wobble across the grain and a large one along it, which no single number
                // can say.  Averaged to 0.38 it wobbles across the grain four times too hard and
                // the rings are stirred away into noise.
                parts.Add(property.Value switch
                {
                    PovNumber number => $"amplitude {IglWriter.Number(number.Value)}",
                    PovVector vector =>
                        $"amplitude [{IglWriter.Number(vector.Red)}, " +
                        $"{IglWriter.Number(vector.Green)}, {IglWriter.Number(vector.Blue)}]",
                    _ => throw new PovEmitException("Turbulence was given no amount.", property.Line)
                });
            }
            else if (TurbulenceProperties.TryGetValue(property.Name, out string name) &&
                     property.Value is PovNumber value)
                parts.Add($"{name} {IglWriter.Number(value.Value)}");
        }

        if (parts.Count > 0)
            writer.Line($"turbulence {{ {string.Join(" ", parts)} }}");
    }

    /// <summary>
    /// This method writes out how a pattern's value is shaped once the pattern has produced it.
    /// POV-Ray names each wave by joining it to the word, as in <c>sine_wave</c>, where the ray
    /// tracer keeps the two apart; the frequency and phase carry across as they are.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="properties">What POV-Ray said about the shaping.</param>
    private static void WriteShaping(IglWriter writer, List<PovProperty> properties)
    {
        foreach (PovProperty property in properties)
        {
            if (property.Name is "frequency" or "phase")
            {
                writer.Line(
                    $"{property.Name} {IglWriter.Number(AsShapingNumber(property))}");

                continue;
            }

            string wave = property.Name[..^"_wave".Length];

            // Only the polynomial wave takes a degree; the rest are named and nothing more.
            writer.Line(wave == "poly"
                ? $"poly {IglWriter.Number(AsShapingNumber(property))} wave"
                : $"{wave} wave");
        }
    }

    /// <summary>
    /// This method reads the number a shaping property was given.
    /// </summary>
    /// <param name="property">The property to read.</param>
    /// <returns>The number it stands for.</returns>
    private static double AsShapingNumber(PovProperty property) => property.Value is PovNumber number
        ? number.Value
        : throw new PovEmitException(
            $"\"{property.Name}\" was given nothing we can read as a number.", property.Line);

    /// <summary>
    /// This method writes out the transforms applied to a pigment, in the order POV-Ray wrote
    /// them, since each one acts on what the ones before it left.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="properties">The transforms.</param>
    private static void WriteTransforms(IglWriter writer, List<PovProperty> properties)
    {
        foreach (PovProperty property in properties)
        {
            PovValue value = property.Value
                ?? throw new PovEmitException($"A \"{property.Name}\" was given nothing.", property.Line);

            if (property.Name == "rotate")
            {
                // POV-Ray turns about all three axes at once; the ray tracer turns about one at a
                // time, in the order POV-Ray's own documentation says it applies them.
                PovVector angles = value as PovVector
                    ?? throw new PovEmitException("A rotation was given no angles.", property.Line);

                foreach ((string axis, double angle) in new[]
                         {
                             ("X", angles.Red), ("Y", angles.Green), ("Z", angles.Blue)
                         }.Where(pair => Math.Abs(pair.Item2) > 1e-12))
                    writer.Line($"rotate {axis} {IglWriter.Number(angle)}");

                continue;
            }

            writer.Line(value switch
            {
                PovNumber number => $"{property.Name} {IglWriter.Number(number.Value)}",
                PovVector vector =>
                    $"{property.Name} [{IglWriter.Number(vector.Red)}, " +
                    $"{IglWriter.Number(vector.Green)}, {IglWriter.Number(vector.Blue)}]",
                _ => throw new PovEmitException(
                    $"A \"{property.Name}\" was given something we cannot read.", property.Line)
            });
        }
    }

    /// <summary>
    /// This method works out which axis a vector points along, complaining if it points between
    /// them, since the ray tracer's gradients run along one axis and nowhere else.
    /// </summary>
    /// <param name="direction">The direction POV-Ray gave.</param>
    /// <param name="line">The line it was written on, for reporting.</param>
    /// <returns>The name of the axis.</returns>
    private static string AxisOf(PovVector direction, int line)
    {
        (string Name, double Amount)[] axes =
        [
            ("X", direction.Red), ("Y", direction.Green), ("Z", direction.Blue)
        ];
        (string Name, double Amount)[] used = axes
            .Where(axis => Math.Abs(axis.Amount) > 1e-12)
            .ToArray();

        if (used.Length != 1)
            throw new PovEmitException("A gradient runs between axes, which we cannot express.", line);

        return used[0].Name;
    }
}
