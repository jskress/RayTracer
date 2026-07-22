namespace RayTracer.PovRay;

/// <summary>
/// This class converts POV-Ray's texture include files into libraries the ray tracer can import
/// from.  It is the whole job in one place: what to read, what to read only for its names, and
/// what comes out.
/// </summary>
public class PovLibraryConverter
{
    /// <summary>
    /// These are the files read for their names alone.
    /// <para>
    /// Neither becomes a library.  <c>colors.inc</c> must be in scope because the stone files use
    /// <c>White</c> and <c>Mica</c> without including it, on the understanding that a scene will
    /// have done so first; it is not emitted because the ray tracer already knows all but a
    /// handful of those colors by the same names, and a color is worked out where it is used, so a
    /// <c>White</c> inside a texture arrives as the number it stands for.  <c>consts.inc</c> is
    /// here for a different reason: <c>glass.inc</c> includes it, and it declares things called
    /// <c>E</c>, <c>O</c> and <c>Xy</c>, which are fine as POV-Ray's own shorthand and would be
    /// very poor names to hand a scene.
    /// </para>
    /// </summary>
    public static readonly string[] PreludeFiles = ["colors.inc", "consts.inc"];

    /// <summary>
    /// These are the files that each become a library.  They are read in this order because a file
    /// has to be read before one that leans on it, and <c>metals.inc</c> leans on <c>golds.inc</c>.
    /// </summary>
    /// <remarks>
    /// <c>ior.inc</c> is deliberately absent.  Its indices of refraction are worth having, but a
    /// library would have to be imported to be used where <see cref="Core.IndicesOfRefraction"/> is
    /// simply in scope, so its gemstones and optical glasses were added there instead.  Two sets of
    /// the same numbers under slightly different names would be a trap rather than a convenience.
    /// </remarks>
    public static readonly string[] LibraryFiles =
    [
        "finish.inc", "golds.inc", "metals.inc", "glass.inc", "stones1.inc", "stones2.inc",
        "woods.inc", "textures.inc", "stars.inc", "skies.inc"
    ];

    /// <summary>
    /// This method reads POV-Ray's include files and works out the libraries they become.  Nothing
    /// is written; see <see cref="Write"/> for that, so that what was converted may be looked over
    /// before any of it lands on disk.
    /// </summary>
    /// <param name="sourceDirectory">The directory POV-Ray's include files live in.</param>
    /// <returns>What the conversion produced.</returns>
    public PovConversion Convert(string sourceDirectory)
    {
        PovSourceReader reader = new PovSourceReader(sourceDirectory, PreludeFiles, LibraryFiles);
        List<PovFile> files = reader.ReadAll();
        List<PovIssue> issues = reader.Issues.ToList();
        PovEmitter emitter = new PovEmitter(files, issues);
        List<PovGeneratedLibrary> libraries = [];

        foreach (PovFile file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file.Name);
            string text = emitter.Emit(file);

            libraries.Add(new PovGeneratedLibrary
            {
                Name = name,
                Text = text,
                Names = emitter.Emitted.Where(emitted => emitted.Library == name).ToList(),
                SourceDeclarations = file.Declarations.Count
            });
        }

        return new PovConversion
        {
            Libraries = libraries, Issues = issues, Names = emitter.Emitted.ToList()
        };
    }

    /// <summary>
    /// This method writes the converted libraries out.
    /// </summary>
    /// <param name="conversion">What the conversion produced.</param>
    /// <param name="directory">The directory to write them to.</param>
    public static void Write(PovConversion conversion, string directory)
    {
        Directory.CreateDirectory(directory);

        foreach (PovGeneratedLibrary library in conversion.Libraries)
            File.WriteAllText(Path.Combine(directory, library.FileName), library.Text);
    }
}

/// <summary>
/// This class holds one library the conversion produced.
/// </summary>
public class PovGeneratedLibrary
{
    /// <summary>
    /// This property holds the library's name, which is what a scene imports it by.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// This property holds the name of the file it is written to.
    /// </summary>
    public string FileName => $"{Name}.igl";

    /// <summary>
    /// This property holds the text of the library.
    /// </summary>
    public string Text { get; init; }

    /// <summary>
    /// This property holds everything the library declares.
    /// </summary>
    public IReadOnlyList<PovEmittedName> Names { get; init; }

    /// <summary>
    /// This property holds how many declarations POV-Ray's own file had, so that what did not come
    /// across can be seen at a glance.
    /// </summary>
    public int SourceDeclarations { get; init; }

    public override string ToString() => $"{FileName} ({Names.Count} names)";
}

/// <summary>
/// This class holds everything one conversion produced.
/// </summary>
public class PovConversion
{
    /// <summary>
    /// This property holds the libraries that were made.
    /// </summary>
    public IReadOnlyList<PovGeneratedLibrary> Libraries { get; init; }

    /// <summary>
    /// This property holds everything that could not be brought across, and why.
    /// </summary>
    public IReadOnlyList<PovIssue> Issues { get; init; }

    /// <summary>
    /// This property holds every name the libraries declare, across all of them.
    /// </summary>
    public IReadOnlyList<PovEmittedName> Names { get; init; }

    /// <summary>
    /// This property provides the names that more than one library declares.
    /// <para>
    /// These are worth saying out loud.  A scene that imports both libraries gets whichever was
    /// read last and no warning that it had a choice, so the only chance to notice is here.
    /// </para>
    /// </summary>
    public IEnumerable<IGrouping<string, PovEmittedName>> Clashes => Names
        .GroupBy(name => name.Name)
        .Where(group => group.Select(name => name.Library).Distinct().Count() > 1);
}
