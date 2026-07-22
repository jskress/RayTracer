namespace RayTracer.PovRay;

/// <summary>
/// This class turns what was read from POV-Ray's include files into a library the ray tracer can
/// import from.
/// <para>
/// Not everything a POV-Ray file declares can stand on its own here, and that is the interesting
/// part of the job rather than a failing of it.  A color map is the clearest case: POV-Ray declares
/// <c>M_Wood1A</c> as a map and pairs it with a pattern later, where the ray tracer has no such
/// thing apart from the pigment it colors.  So a map, and a pigment with no map of its own, are
/// not emitted at all; instead they are folded into the textures that use them, which is why this
/// keeps every declaration to hand by name rather than working through them one at a time.
/// </para>
/// </summary>
public partial class PovEmitter
{
    /// <summary>
    /// These are the kinds of block that only ever make sense inside something else, so a
    /// declaration that is nothing but one of them is passed over rather than emitted.  What it
    /// holds is not lost: it arrives inside whatever named it.
    /// </summary>
    private static readonly HashSet<string> IngredientBlocks =
    [
        "color_map", "colour_map", "pigment_map", "texture_map", "normal_map", "density_map",
        "slope_map"
    ];

    private readonly Dictionary<string, PovDeclaration> _byName;
    private readonly List<PovIssue> _issues;
    private readonly List<PovEmittedName> _emitted = [];

    private string _currentLibrary;

    /// <summary>
    /// This property provides what was written out, and what sort of thing each one is, so that a
    /// scene may be built that uses every one of them and a name declared twice may be found.
    /// </summary>
    public IReadOnlyList<PovEmittedName> Emitted => _emitted;

    /// <param name="files">Every file that was read, whose declarations are needed here as a whole
    /// so that one may be folded into another that names it.</param>
    /// <param name="issues">Where to note anything that could not be brought across.</param>
    public PovEmitter(IEnumerable<PovFile> files, List<PovIssue> issues)
    {
        _issues = issues;
        _byName = files
            .SelectMany(file => file.Declarations)
            .GroupBy(declaration => declaration.Name)
            .ToDictionary(group => group.Key, group => group.Last());
    }

    /// <summary>
    /// This method writes out one library.
    /// </summary>
    /// <param name="file">The file to write out.</param>
    /// <returns>The text of the generated library.</returns>
    public string Emit(PovFile file)
    {
        IglWriter writer = new IglWriter();

        _currentLibrary = Path.GetFileNameWithoutExtension(file.Name);

        writer.Line($"// Converted from POV-Ray's {file.Name}.");
        writer.Line("//");
        writer.Line("// This file is generated.  Anything changed here will be lost the next time");
        writer.Line("// the library is imported.");
        writer.Line();

        foreach (string include in file.Includes)
        {
            writer.Line($"include '{Path.GetFileNameWithoutExtension(include)}.igl'");
        }

        if (file.Includes.Count > 0)
            writer.Line();

        // POV-Ray may declare the same name twice, and golds.inc does: it works out five diffuse
        // values and then declares each of them again as the greater of itself and zero.  Only the
        // last one counts, and it is the one every value that leans on it was worked out from, so
        // writing the earlier ones would put a line in the library that is wrong and then correct
        // it on the next.
        HashSet<PovDeclaration> lastDeclarations = file.Declarations
            .GroupBy(declaration => declaration.Name)
            .Select(group => group.Last())
            .ToHashSet();

        // A name may only be used after it has been declared, so the order POV-Ray wrote them in
        // is the order they have to be written in here.
        foreach (PovDeclaration declaration in file.Declarations
                     .Where(declaration => lastDeclarations.Contains(declaration))
                     .Where(declaration => !IsSupersededTexture(declaration, file)))
            EmitDeclaration(writer, declaration);

        return writer.ToString();
    }

    /// <summary>
    /// This method notes whether a declaration is a texture that a material in the same file
    /// already says everything about.
    /// <para>
    /// POV-Ray very often declares both, as <c>textures.inc</c> does for its glasses: <c>Glass3</c>
    /// is the texture and <c>M_Glass3</c> is that same texture with an interior around it.  Here
    /// the two are one thing, and both would arrive under the name <c>Glass3Material</c>, leaving
    /// the file declaring it twice with the second quietly winning.  The material is the one to
    /// keep, since it is the texture and more.
    /// </para>
    /// </summary>
    /// <param name="declaration">The declaration to consider.</param>
    /// <param name="file">The file it is in.</param>
    /// <returns><c>true</c> if a material in the same file supersedes it.</returns>
    private bool IsSupersededTexture(PovDeclaration declaration, PovFile file)
    {
        if (declaration.Value is not PovBlock { Kind: "texture" } and not PovBlockSequence)
            return false;

        string name = PovNames.From(declaration.Name, "Material");
        PovDeclaration material = file.Declarations.LastOrDefault(other =>
            other.Value is PovBlock { Kind: "material" } &&
            PovNames.From(other.Name, "Material") == name);

        if (material is null)
            return false;

        Report(declaration, declaration.Line,
            $"\"{material.Name}\" is this and an interior besides, and both would be called " +
            $"{name}, so the material is the one kept.");

        return true;
    }

    /// <summary>
    /// This method writes out one declaration, or notes why it could not be.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="declaration">The declaration to write out.</param>
    private void EmitDeclaration(IglWriter writer, PovDeclaration declaration)
    {
        // Every one of POV-Ray's library files declares one of these to remember the version in
        // force so that it may put it back afterward.  It is bookkeeping rather than something a
        // scene would ever want, and it would only be noise in the library.
        if (declaration.Name.EndsWith("_Inc_Temp", StringComparison.Ordinal))
            return;

        // The declaration is written somewhere of its own and kept only if it works.  Anything we
        // cannot express is found partway through writing it, and half a block left in the library
        // would stop the whole file reading rather than costing us the one declaration.
        IglWriter scratch = new IglWriter();
        int noted = _emitted.Count;

        // The name POV-Ray gave it goes above it.  Anyone reaching for these libraries is reading
        // POV-Ray's own documentation, where every one of them is called something else, and this
        // is what lets them find "T_Stone10" in a file that calls it "Stone10Material".
        scratch.Line($"// {declaration.Name}");

        try
        {
            switch (declaration.Value)
            {
                case PovNumber number:
                    Note(declaration, "number");
                    scratch.Line($"{PovNames.From(declaration.Name, null)} = {IglWriter.Number(number.Value)}");

                    break;

                case PovVector vector:
                    EmitColor(scratch, declaration, vector);

                    break;

                case PovBlockSequence sequence:
                    EmitLayeredTexture(scratch, declaration, sequence);

                    break;

                case PovBlock block:
                    EmitBlock(scratch, declaration, block);

                    break;

                default:
                    throw new PovEmitException(
                        "This is not something a library can hold.", declaration.Line);
            }

            writer.Add(scratch);
        }
        catch (PovEmitException exception)
        {
            // A declaration that failed may have named itself before it did, so the name goes back
            // too; nothing in the library declares it.
            _emitted.RemoveRange(noted, _emitted.Count - noted);

            Report(declaration, exception.Line, exception.Message);
        }
    }

    /// <summary>
    /// This method writes out a declaration whose value is a block.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="declaration">The declaration being written.</param>
    /// <param name="block">The block it was set to.</param>
    private void EmitBlock(IglWriter writer, PovDeclaration declaration, PovBlock block)
    {
        switch (block.Kind)
        {
            case "texture":
            case "material":
                EmitTexture(writer, declaration, block);

                break;

            case "pigment":
                EmitPigmentDeclaration(writer, declaration, block);

                break;

            case "finish":
                EmitFinish(writer, declaration, block);

                break;

            case "interior":
                EmitInterior(writer, declaration, block);

                break;

            case not null when IngredientBlocks.Contains(block.Kind):
                throw new PovEmitException(
                    $"A \"{block.Kind}\" is not something a scene can name on its own; it comes " +
                    "across inside the textures that use it.", block.Line);

            default:
                throw new PovEmitException(
                    $"We cannot express a \"{block.Kind}\" block.", block.Line);
        }
    }

    /// <summary>
    /// This method writes out a declaration whose value is a color.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="declaration">The declaration being written.</param>
    /// <param name="vector">The color it was set to.</param>
    private void EmitColor(IglWriter writer, PovDeclaration declaration, PovVector vector)
    {
        // POV-Ray makes no type distinction between a color and a vector, but it does distinguish
        // them in writing, and what a thing was written as is what it was meant as: golds.inc says
        // "<1.00, 0.875, 0.575>" for the proportions it derives its colors from and "rgb CVect1"
        // for a color made out of them.  Keeping them apart matters because a scene can do things
        // with a vector -- scale by it, move along it -- that it cannot do with a color.
        if (!vector.IsColor)
        {
            Note(declaration, "vector");

            writer.Line(
                $"{PovNames.From(declaration.Name, null)} = " +
                $"[{IglWriter.Number(vector.Red)}, {IglWriter.Number(vector.Green)}, " +
                $"{IglWriter.Number(vector.Blue)}]");

            return;
        }

        (double alpha, _) = Transparency(vector);

        Note(declaration, "color", "Color");

        writer.Line(
            $"{PovNames.From(declaration.Name, "Color")} = color " +
            IglWriter.Color(vector.Red, vector.Green, vector.Blue, alpha));
    }

    /// <summary>
    /// This method notes that a declaration was written out.  The library it is recorded against
    /// is the one being written rather than the file the declaration came from, since a file that
    /// is not becoming a library has its declarations folded into one that is, and it is the
    /// library a scene will import from that matters here.
    /// </summary>
    /// <param name="declaration">The declaration that was written.</param>
    /// <param name="kind">What sort of thing it turned into.</param>
    /// <param name="suffix">The word put on the end of its name, if any.</param>
    private void Note(PovDeclaration declaration, string kind, string suffix = null) =>
        _emitted.Add(new PovEmittedName
        {
            Name = PovNames.From(declaration.Name, suffix),
            PovName = declaration.Name,
            Kind = kind,
            Library = _currentLibrary
        });

    /// <summary>
    /// This method works out how a POV-Ray color's transparency should be split between the two
    /// places the ray tracer keeps it.
    /// <para>
    /// POV-Ray carries two kinds at once.  Its <c>transmit</c> lets light straight through, and
    /// its <c>filter</c> lets light through tinted by the color it passed.  They add up: a color
    /// with a filter of 0.3 and a transmit of 0.2 is half transparent, and of what gets through,
    /// three fifths was tinted.  So the total decides how opaque the color is, and the share of it
    /// that was filter decides how much of what passes is colored, which is what the interior's
    /// filter means here.
    /// </para>
    /// </summary>
    /// <param name="color">The color to consider.</param>
    /// <returns>How opaque the color is, and what share of what it lets through is tinted.</returns>
    private static (double Alpha, double FilterShare) Transparency(PovVector color)
    {
        double total = Math.Clamp(color.Filter + color.Transmit, 0, 1);

        return (1 - total, total > 0 ? color.Filter / (color.Filter + color.Transmit) : 0);
    }

    /// <summary>
    /// This method looks up what a name stands for, following a chain of names to whatever is at
    /// the end of it.
    /// </summary>
    /// <param name="value">The value that may be a name.</param>
    /// <returns>What it stands for.</returns>
    private PovValue Resolve(PovValue value)
    {
        HashSet<string> seen = [];

        while (value is PovReference reference)
        {
            if (!seen.Add(reference.Name))
                throw new PovEmitException($"\"{reference.Name}\" stands for itself.", reference.Line);

            if (!_byName.TryGetValue(reference.Name, out PovDeclaration declaration))
                throw new PovEmitException($"Nothing declares \"{reference.Name}\".", reference.Line);

            value = declaration.Value;
        }

        return value;
    }

    /// <summary>
    /// This method notes something that could not be brought across.
    /// </summary>
    /// <param name="declaration">The declaration it was in.</param>
    /// <param name="line">The line it was on.</param>
    /// <param name="reason">Why it could not be brought across.</param>
    private void Report(PovDeclaration declaration, int line, string reason) =>
        _issues.Add(new PovIssue
        {
            SourceFile = declaration.SourceFile,
            Line = line,
            Name = declaration.Name,
            Reason = reason
        });
}

/// <summary>
/// This class represents something that was read from a POV-Ray file but cannot be said in the ray
/// tracer's own language.  It is caught for each declaration on its own, so that one texture we
/// cannot express costs us only that texture.
/// </summary>
public class PovEmitException : Exception
{
    /// <summary>
    /// This property holds the line the trouble was on.
    /// </summary>
    public int Line { get; }

    public PovEmitException(string message, int line) : base(message)
    {
        Line = line;
    }
}
