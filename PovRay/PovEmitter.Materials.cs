namespace RayTracer.PovRay;

/// <summary>
/// This class turns what was read from POV-Ray's include files into a library.
/// </summary>
public partial class PovEmitter
{
    /// <summary>
    /// This is as sharp a highlight as we will ask for.  POV-Ray's roughness turns into an
    /// exponent that climbs very fast as the roughness falls, and its glassiest finishes would
    /// otherwise ask for one in the hundreds of millions, which is a mirror written as arithmetic
    /// and no better looking for the extra digits.
    /// </summary>
    private const double MaximumShininess = 100000;

    /// <summary>
    /// These are the things POV-Ray may say about a finish that the ray tracer says the same way,
    /// under its own name for them.
    /// </summary>
    private static readonly Dictionary<string, string> FinishProperties = new()
    {
        ["ambient"] = "ambient",
        ["diffuse"] = "diffuse",
        ["specular"] = "specular",
        ["brilliance"] = "brilliance",
        ["crand"] = "grain",
        ["reflection"] = "reflective"
    };

    /// <summary>
    /// These are the things POV-Ray may say about a finish or an interior that the ray tracer has
    /// no answer for, but which only refine how a surface looks rather than deciding it.  They are
    /// left out and noted, since a gold without its iridescence is still recognisably that gold,
    /// where a texture dropped altogether is simply gone.
    /// </summary>
    private static readonly HashSet<string> IgnorableProperties =
    [
        "irid", "conserve_energy", "fresnel", "reflection_exponent", "subsurface", "iridescence",
        "caustics", "dispersion", "dispersion_samples", "fade_power", "fade_color", "fade_colour",
        "phong_albedo", "specular_albedo", "diffuse_albedo", "use_alpha", "no_radiosity"
    ];

    /// <summary>
    /// This class gathers up everything that goes into one material.
    /// </summary>
    private class TextureParts
    {
        public PigmentParts Pigment { get; set; }
        public Dictionary<string, string> Finish { get; } = new();
        public Dictionary<string, string> Interior { get; } = new();
        public List<PovProperty> Transforms { get; } = [];
        public double FilterShare { get; set; }
    }

    /// <summary>
    /// This method writes out a declaration whose value is a texture or a material.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="declaration">The declaration being written.</param>
    /// <param name="block">The block it was set to.</param>
    private void EmitTexture(IglWriter writer, PovDeclaration declaration, PovBlock block)
    {
        TextureParts parts = new TextureParts();

        CollectTexture(block, parts, declaration);

        Note(declaration, "material", "Material");

        WriteMaterial(writer, PovNames.From(declaration.Name, "Material"), parts, block.Line);
    }

    /// <summary>
    /// This method writes out a declaration whose value is a run of textures laid over one another.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="declaration">The declaration being written.</param>
    /// <param name="sequence">The layers it was set to.</param>
    private void EmitLayeredTexture(
        IglWriter writer, PovDeclaration declaration, PovBlockSequence sequence)
    {
        List<TextureParts> layers = [];

        foreach (PovBlock layer in sequence.Blocks)
        {
            TextureParts parts = new TextureParts();

            CollectTexture(layer, parts, declaration);

            layers.Add(parts);
        }

        if (layers.Any(layer => layer.Pigment is null))
            throw new PovEmitException("A layer of this texture has no pigment.", declaration.Line);

        string name = PovNames.From(declaration.Name, "Material");

        // Only the pigments layer.  Each of POV-Ray's layers carries a finish of its own, and the
        // ray tracer has one finish for the surface, so the bottom layer's is kept: it is the one
        // that shows wherever the layers above it let it through, which is most of the surface for
        // the wood textures this is for.
        if (layers.Skip(1).Any(layer => layer.Finish.Count > 0))
        {
            Report(declaration, declaration.Line,
                "Only the bottom layer's finish came across; the ray tracer has one finish for a " +
                "surface where POV-Ray has one for each layer.");
        }

        layers[0].FilterShare = layers.Max(layer => FilterShareOf(layer.Pigment));

        Note(declaration, "material", "Material");

        writer.Open($"{name} = material");
        writer.Open("pigment layer");

        // POV-Ray writes its layers from the bottom up and the ray tracer reads them from the top
        // down, so they go in the other order here.  A layer's entries are pigments already, so
        // none of them says the word, and all but the last are followed by a comma.
        List<TextureParts> topDown = Enumerable.Reverse(layers).ToList();

        for (int index = 0; index < topDown.Count; index++)
        {
            WritePigment(
                writer, string.Empty, index < topDown.Count - 1 ? "," : string.Empty,
                topDown[index].Pigment, declaration.Line);
        }

        writer.Close();

        WriteFinish(writer, layers[0]);
        writer.Close();
        writer.Line();
    }

    /// <summary>
    /// This method gathers everything one material is made of, following any names it is built
    /// from and folding what they hold into the same pile.
    /// </summary>
    /// <param name="value">The texture, or a name standing for one.</param>
    /// <param name="parts">The pile to add to.</param>
    /// <param name="declaration">The declaration being written, for reporting.</param>
    private void CollectTexture(PovValue value, TextureParts parts, PovDeclaration declaration)
    {
        if (Resolve(value) is not PovBlock block)
            throw new PovEmitException("A texture is not something we can read.", declaration.Line);

        foreach (IPovItem item in block.Items)
            CollectTextureItem(item, parts, declaration);
    }

    /// <summary>
    /// This method takes one thing out of a texture block and puts it where it belongs.
    /// </summary>
    /// <param name="item">The item to place.</param>
    /// <param name="parts">The pile to add to.</param>
    /// <param name="declaration">The declaration being written, for reporting.</param>
    private void CollectTextureItem(IPovItem item, TextureParts parts, PovDeclaration declaration)
    {
        switch (item)
        {
            case PovBlock block when block.Kind == "pigment":
                parts.Pigment ??= new PigmentParts();

                CollectPigment(block, parts.Pigment);

                break;

            case PovBlock block when block.Kind == "finish":
                CollectFinish(block, parts, declaration);

                break;

            case PovBlock block when block.Kind == "interior":
                CollectInterior(block, parts, declaration);

                break;

            case PovBlock block when block.Kind is "texture" or "material":
                CollectTexture(block, parts, declaration);

                break;

            case PovBlock block when block.Kind == "normal":
                // The ray tracer has no way to disturb a surface normal yet, so a texture's bumps
                // are left behind rather than costing us the rest of it.
                Report(declaration, block.Line,
                    "The surface roughening did not come across; the ray tracer cannot disturb a " +
                    "normal yet.");

                break;

            case PovBlock block:
                throw new PovEmitException(
                    $"We cannot express a \"{block.Kind}\" inside a texture.", block.Line);

            case PovReference reference:
                CollectTexture(reference, parts, declaration);

                break;

            case PovProperty property when property.Name is "scale" or "rotate" or "translate":
                // A transform on a texture moves the pattern under it, which is the pigment's
                // business here, so it goes there.
                parts.Transforms.Add(property);

                break;

            case PovProperty property:
                throw new PovEmitException(
                    $"We cannot express \"{property.Name}\" in a texture.", property.Line);

            case PovVector color:
                parts.Pigment ??= new PigmentParts();
                parts.Pigment.SolidColor = color;

                break;
        }
    }

    /// <summary>
    /// This method reads a finish block into the material properties it stands for.
    /// </summary>
    /// <param name="value">The finish, or a name standing for one.</param>
    /// <param name="parts">The pile to add to.</param>
    /// <param name="declaration">The declaration being written, for reporting.</param>
    private void CollectFinish(PovValue value, TextureParts parts, PovDeclaration declaration)
    {
        if (Resolve(value) is not PovBlock block)
            throw new PovEmitException("A finish is not something we can read.", declaration.Line);

        foreach (IPovItem item in block.Items)
        {
            switch (item)
            {
                case PovReference reference:
                    CollectFinish(reference, parts, declaration);

                    break;

                case PovProperty property:
                    CollectFinishProperty(property, parts, declaration);

                    break;

                case PovBlock inner:
                    throw new PovEmitException(
                        $"We cannot express a \"{inner.Kind}\" inside a finish.", inner.Line);
            }
        }
    }

    /// <summary>
    /// This method reads one property of a finish.
    /// </summary>
    /// <param name="property">The property to read.</param>
    /// <param name="parts">The pile to add to.</param>
    /// <param name="declaration">The declaration being written, for reporting.</param>
    private void CollectFinishProperty(
        PovProperty property, TextureParts parts, PovDeclaration declaration)
    {
        if (IgnorableProperties.Contains(property.Name))
        {
            Report(declaration, property.Line,
                $"\"{property.Name}\" did not come across; the ray tracer has no answer for it.");

            return;
        }

        switch (property.Name)
        {
            case "metallic":
                // Both languages let the amount be left off, meaning fully metallic, but the ray
                // tracer's grammar then takes whatever property comes next as the amount, so the
                // 1 is always said here rather than left to be understood.
                parts.Finish["metallic"] = IglWriter.Number(
                    property.Value is PovNumber amount ? amount.Value : 1);

                break;

            case "roughness":
                parts.Finish["shininess"] = IglWriter.Number(ShininessFor(property, declaration));

                break;

            case "phong":
                parts.Finish["specular"] = IglWriter.Number(AsNumber(property));

                break;

            case "phong_size":
                parts.Finish["shininess"] = IglWriter.Number(AsNumber(property));

                break;

            case not null when FinishProperties.TryGetValue(property.Name, out string name):
                parts.Finish[name] = IglWriter.Number(AsNumber(property));

                break;

            default:
                throw new PovEmitException(
                    $"We cannot express \"{property.Name}\" in a finish.", property.Line);
        }
    }

    /// <summary>
    /// This method turns POV-Ray's roughness into the ray tracer's shininess.
    /// <para>
    /// The two say the same thing from opposite ends: roughness is how broad a highlight is and
    /// shininess is the exponent that makes it tight.  The factor between them is not obvious and
    /// was measured rather than reasoned out, by rendering a lit sphere at each roughness in both
    /// and counting the pixels the highlight covered.  It comes to a quarter of the reciprocal,
    /// which agrees with the theory: POV-Ray raises the angle at the halfway vector to the power
    /// of the reciprocal of the roughness, and an exponent taken against the reflected ray, which
    /// is what happens here, needs to be about a quarter of one taken at the halfway vector to
    /// draw the same highlight.  Measured against POV-Ray it lands within a tenth over the range
    /// the library finishes use, and within a few hundredths at the tight end where the metals are.
    /// </para>
    /// <para>
    /// Getting this wrong is not subtle.  The obvious-looking <c>2/roughness² - 2</c> asks for a
    /// shininess of 798 where 5 is wanted, and every metal in the library comes out with a hard
    /// pinpoint glint instead of a broad sheen -- or, past about two thousand, with a highlight so
    /// tight that no pixel catches it and the surface has no glint at all.
    /// </para>
    /// </summary>
    /// <param name="property">The roughness property.</param>
    /// <param name="declaration">The declaration being written, for reporting.</param>
    /// <returns>The shininess to use.</returns>
    private double ShininessFor(PovProperty property, PovDeclaration declaration)
    {
        double roughness = AsNumber(property);

        if (roughness <= 0)
            throw new PovEmitException("A roughness of zero says nothing we can use.", property.Line);

        double shininess = 1 / (4 * roughness);

        if (shininess <= MaximumShininess)
            return Math.Max(shininess, 1);

        Report(declaration, property.Line,
            $"A roughness of {IglWriter.Number(roughness)} asks for a highlight sharper than we " +
            $"draw; it was held to a shininess of {IglWriter.Number(MaximumShininess)}.");

        return MaximumShininess;
    }

    /// <summary>
    /// This method reads an interior block into the properties it stands for.
    /// </summary>
    /// <param name="value">The interior, or a name standing for one.</param>
    /// <param name="parts">The pile to add to.</param>
    /// <param name="declaration">The declaration being written, for reporting.</param>
    private void CollectInterior(PovValue value, TextureParts parts, PovDeclaration declaration)
    {
        if (Resolve(value) is not PovBlock block)
            throw new PovEmitException("An interior is not something we can read.", declaration.Line);

        foreach (IPovItem item in block.Items)
        {
            switch (item)
            {
                case PovReference reference:
                    CollectInterior(reference, parts, declaration);

                    break;

                case PovProperty property when IgnorableProperties.Contains(property.Name):
                    Report(declaration, property.Line,
                        $"\"{property.Name}\" did not come across; the ray tracer has no answer for it.");

                    break;

                case PovProperty property when property.Name is "ior" or "index_of_refraction":
                    parts.Interior["ior"] = IglWriter.Number(AsNumber(property));

                    break;

                case PovProperty property when property.Name == "fade_distance":
                    // How far light gets before it is spent is the same idea in both, measured the
                    // same way, so this one carries straight across.
                    parts.Interior["clarity"] = IglWriter.Number(AsNumber(property));

                    break;

                case PovProperty property:
                    throw new PovEmitException(
                        $"We cannot express \"{property.Name}\" in an interior.", property.Line);

                case PovBlock inner:
                    throw new PovEmitException(
                        $"We cannot express a \"{inner.Kind}\" inside an interior.", inner.Line);
            }
        }
    }

    /// <summary>
    /// This method writes out a declaration whose value is a finish on its own.  There is no such
    /// thing here, a finish being part of a material, so it becomes a material with nothing but
    /// its finish set, which a texture may then be built on.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="declaration">The declaration being written.</param>
    /// <param name="block">The finish block it was set to.</param>
    private void EmitFinish(IglWriter writer, PovDeclaration declaration, PovBlock block)
    {
        TextureParts parts = new TextureParts();

        CollectFinish(block, parts, declaration);

        Note(declaration, "material", "Finish");

        writer.Open($"{PovNames.From(declaration.Name, "Finish")} = material");

        WriteFinish(writer, parts);

        writer.Close();
        writer.Line();
    }

    /// <summary>
    /// This method writes out a declaration whose value is an interior.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="declaration">The declaration being written.</param>
    /// <param name="block">The interior block it was set to.</param>
    private void EmitInterior(IglWriter writer, PovDeclaration declaration, PovBlock block)
    {
        TextureParts parts = new TextureParts();

        CollectInterior(block, parts, declaration);

        if (parts.Interior.Count == 0)
            throw new PovEmitException("This interior says nothing we can express.", block.Line);

        Note(declaration, "interior", "Interior");

        writer.Open($"{PovNames.From(declaration.Name, "Interior")} = interior");

        foreach ((string name, string value) in parts.Interior)
            writer.Line($"{name} {value}");

        writer.Close();
        writer.Line();
    }

    /// <summary>
    /// This method writes one material out.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="name">The name to declare it under.</param>
    /// <param name="parts">Everything the material is made of.</param>
    /// <param name="line">The line it came from, for reporting.</param>
    private void WriteMaterial(IglWriter writer, string name, TextureParts parts, int line)
    {
        if (parts.Pigment is null)
            throw new PovEmitException("This texture says nothing about how it is colored.", line);

        // A transform written on the texture belongs to the pattern under it, and the pattern is
        // the pigment's, so it is applied after whatever the pigment already had.
        parts.Pigment.Transforms.AddRange(parts.Transforms);
        parts.FilterShare = FilterShareOf(parts.Pigment);

        writer.Open($"{name} = material");

        WritePigment(writer, "pigment ", string.Empty, parts.Pigment, line);
        WriteFinish(writer, parts);

        writer.Close();
        writer.Line();
    }

    /// <summary>
    /// This method writes out a material's finish properties and its interior.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="parts">Everything the material is made of.</param>
    private static void WriteFinish(IglWriter writer, TextureParts parts)
    {
        foreach ((string name, string value) in parts.Finish)
            writer.Line(value.Length == 0 ? name : $"{name} {value}");

        Dictionary<string, string> interior = new Dictionary<string, string>(parts.Interior);

        // Where a color said that what passes through it should be tinted, that is the interior's
        // business rather than the pigment's, since it is about the stuff behind the surface.
        if (parts.FilterShare > 0)
            interior["filter"] = IglWriter.Number(parts.FilterShare);

        if (interior.Count == 0)
            return;

        writer.Open("interior");

        foreach ((string name, string value) in interior)
            writer.Line($"{name} {value}");

        writer.Close();
    }

    /// <summary>
    /// This method works out how much of what passes through a material should be tinted by it.
    /// <para>
    /// A pigment may be many colors rather than one, and they need not agree about this, but the
    /// interior is one thing for the whole surface.  The largest share is taken: a texture that is
    /// stained glass anywhere is stained glass, and a color that was clear rather than tinted
    /// still passes its light through by way of its own alpha.
    /// </para>
    /// </summary>
    /// <param name="pigment">The pigment to consider.</param>
    /// <returns>The share of transmitted light that should be tinted.</returns>
    private static double FilterShareOf(PigmentParts pigment)
    {
        IEnumerable<PovVector> colors = pigment.Map
            .SelectMany(entry => entry.Values)
            .OfType<PovVector>();

        if (pigment.SolidColor is not null)
            colors = colors.Append(pigment.SolidColor);

        return colors
            .Select(color => Transparency(color).FilterShare)
            .DefaultIfEmpty(0)
            .Max();
    }

    /// <summary>
    /// This method reads a property's value as a plain number.  A color is taken at its average,
    /// since POV-Ray lets several of the finish properties be given one where the ray tracer wants
    /// an amount.
    /// </summary>
    /// <param name="property">The property to read.</param>
    /// <returns>The number it stands for.</returns>
    private static double AsNumber(PovProperty property) => property.Value switch
    {
        PovNumber number => number.Value,
        PovVector vector => (vector.Red + vector.Green + vector.Blue) / 3,
        _ => throw new PovEmitException(
            $"\"{property.Name}\" was given nothing we can read as an amount.", property.Line)
    };
}
