using RayTracer.Fonts;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.PovRay;

namespace RayTracer.Commands;

/// <summary>
/// This class provides the implementation of our "libraries" command line verb.
/// </summary>
public static class LibrariesCommand
{
    private static readonly List<string> LibraryHeadings = ["Library", "Definitions", "Source"];
    private static readonly List<TextAlignment> LibraryAlignments =
    [
        TextAlignment.Left, TextAlignment.Right, TextAlignment.Left
    ];
    private static readonly List<string> ConvertedHeadings =
    [
        "Library", "Materials", "Pigments", "Interiors", "Values", "Of"
    ];
    private static readonly List<TextAlignment> ConvertedAlignments =
    [
        TextAlignment.Left, TextAlignment.Right, TextAlignment.Right, TextAlignment.Right,
        TextAlignment.Right, TextAlignment.Right
    ];

    /// <summary>
    /// This method provides the meat of our "libraries" command line verb.
    /// </summary>
    /// <param name="options">The options specified by the user on the command line.</param>
    public static void ManageLibraries(LibrariesOptions options)
    {
        if (options.ListLibraries)
            ShowExistingLibraries();
        else if (options.ImportPovRayFrom != null)
            ImportPovRayLibraries(options);
        else if (options.RemoveLibrary != null)
            RemoveLibrary(options.RemoveLibrary);
        else
            Console.WriteLine("No action was specified.  Use '--help' for a list of options.");
    }

    /// <summary>
    /// This method is used to show the libraries the ray tracer knows about.
    /// </summary>
    private static void ShowExistingLibraries()
    {
        if (!Directory.Exists(LibraryLocator.LibrariesDirectory))
        {
            Terminal.Out($"There are no libraries; {LibraryLocator.LibrariesDirectory} does not exist.");

            return;
        }

        List<string> paths = Directory
            .GetFiles(LibraryLocator.LibrariesDirectory, "*.igl")
            .OrderBy(Path.GetFileName)
            .ToList();

        if (paths.Count == 0)
        {
            Terminal.Out($"There are no libraries in {LibraryLocator.LibrariesDirectory}.");

            return;
        }

        List<List<string>> data = [LibraryHeadings];

        data.AddRange(paths.Select(path => (List<string>)
        [
            Path.GetFileNameWithoutExtension(path),
            CountDefinitions(path).ToString("n0"),
            FirstLineOf(path)
        ]));

        Terminal.Out(LibraryLocator.LibrariesDirectory);
        Terminal.Out(data, alignments: LibraryAlignments, hasHeadings: true);
        Terminal.Out("");
    }

    /// <summary>
    /// This method counts the definitions in a library, which is the count of its top level
    /// assignments.  A line that starts indented is part of the definition above it.
    /// </summary>
    /// <param name="path">The path of the library to count.</param>
    /// <returns>How many things the library defines.</returns>
    private static int CountDefinitions(string path) => File
        .ReadLines(path)
        .Count(line => line.Length > 0 && !char.IsWhiteSpace(line[0]) &&
                       !line.StartsWith("//", StringComparison.Ordinal) && line.Contains(" = "));

    /// <summary>
    /// This method reads the note a generated library carries about where it came from.
    /// </summary>
    /// <param name="path">The path of the library to look at.</param>
    /// <returns>Where it came from, or an empty string when it does not say.</returns>
    private static string FirstLineOf(string path)
    {
        string first = File.ReadLines(path).FirstOrDefault() ?? string.Empty;

        return first.StartsWith("// Converted from ", StringComparison.Ordinal)
            ? first["// Converted from ".Length..].TrimEnd('.')
            : string.Empty;
    }

    /// <summary>
    /// This method converts POV-Ray's texture include files into libraries.
    /// </summary>
    /// <param name="options">The options specified by the user on the command line.</param>
    private static void ImportPovRayLibraries(LibrariesOptions options)
    {
        string source = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), options.ImportPovRayFrom));

        if (!Directory.Exists(source))
            Terminal.ShowError($"The directory, '{source}', does not exist.");

        List<string> missing = PovLibraryConverter.LibraryFiles
            .Concat(PovLibraryConverter.PreludeFiles)
            .Where(name => !File.Exists(Path.Combine(source, name)))
            .ToList();

        // Naming what is missing beats saying the directory is wrong: someone who pointed at the
        // wrong one of two POV-Ray installs needs to know which files were looked for.
        if (missing.Count > 0)
        {
            Terminal.ShowError(
                $"'{source}' does not look like a POV-Ray include directory; it has no " +
                $"{string.Join(", ", missing.Order())}.");
        }

        PovConversion conversion = new PovLibraryConverter().Convert(source);

        ReportConversion(conversion, options);

        if (options.DryRun)
        {
            Terminal.Out("");
            Terminal.Out("Nothing was written, since --dry-run was given.");

            return;
        }

        List<string> existing = conversion.Libraries
            .Where(library => File.Exists(
                Path.Combine(LibraryLocator.LibrariesDirectory, library.FileName)))
            .Select(library => library.Name)
            .ToList();

        if (existing.Count > 0 && !options.Replace)
        {
            Terminal.ShowError(
                $"These libraries already exist: {string.Join(", ", existing.Order())}.  " +
                "Specify --overwrite if you want to replace them.");
        }

        PovLibraryConverter.Write(conversion, LibraryLocator.LibrariesDirectory);

        Terminal.Out("");
        Terminal.Out(
            $"{conversion.Libraries.Count} libraries holding {conversion.Names.Count:n0} " +
            $"definitions were written to {LibraryLocator.LibrariesDirectory}.");
    }

    /// <summary>
    /// This method tells the user what the conversion produced and what it could not.
    /// </summary>
    /// <param name="conversion">What the conversion produced.</param>
    /// <param name="options">The options specified by the user on the command line.</param>
    private static void ReportConversion(PovConversion conversion, LibrariesOptions options)
    {
        List<List<string>> data = [ConvertedHeadings];

        data.AddRange(conversion.Libraries.Select(library => (List<string>)
        [
            library.Name,
            Count(library, "material"),
            Count(library, "pigment"),
            Count(library, "interior"),
            (library.Names.Count(name => name.Kind is "color" or "number" or "vector")).ToString("n0"),
            $"of {library.SourceDeclarations:n0}"
        ]));

        Terminal.Out(data, alignments: ConvertedAlignments, hasHeadings: true);

        ReportIssues(conversion, options);
        ReportClashes(conversion);
    }

    /// <summary>
    /// This method counts how many of one sort of thing a library declares.
    /// </summary>
    /// <param name="library">The library to count in.</param>
    /// <param name="kind">The sort of thing to count.</param>
    /// <returns>How many there are.</returns>
    private static string Count(PovGeneratedLibrary library, string kind) =>
        library.Names.Count(name => name.Kind == kind).ToString("n0");

    /// <summary>
    /// This method tells the user what could not be brought across.
    /// <para>
    /// The reasons are gathered rather than listed one by one, since the same one very often
    /// stands for dozens of definitions and a list of ninety lines says less than a count of
    /// eight things.  The whole list is there for the asking.
    /// </para>
    /// </summary>
    /// <param name="conversion">What the conversion produced.</param>
    /// <param name="options">The options specified by the user on the command line.</param>
    private static void ReportIssues(PovConversion conversion, LibrariesOptions options)
    {
        if (conversion.Issues.Count == 0)
            return;

        Terminal.Out("");
        Terminal.Out($"{conversion.Issues.Count:n0} definitions did not come across whole:");

        if (options.ShowDetails)
        {
            foreach (PovIssue issue in conversion.Issues)
                Terminal.Out($"  {issue}");

            return;
        }

        foreach (IGrouping<string, PovIssue> group in conversion.Issues
                     .GroupBy(issue => issue.Reason)
                     .OrderByDescending(group => group.Count()))
        {
            Terminal.Out(
                $"  {group.Count(),4}  {group.Key} " +
                $"(e.g. {group.First().Name ?? group.First().SourceFile})");
        }

        Terminal.Out("");
        Terminal.Out("  Use --details to see each one.");
    }

    /// <summary>
    /// This method tells the user about any name that more than one library declares.
    /// </summary>
    /// <param name="conversion">What the conversion produced.</param>
    private static void ReportClashes(PovConversion conversion)
    {
        List<IGrouping<string, PovEmittedName>> clashes = conversion.Clashes.ToList();

        if (clashes.Count == 0)
            return;

        Terminal.Out("");
        Terminal.Out(
            $"{clashes.Count:n0} names are declared by more than one library.  A scene that " +
            "imports both gets the one read last:");

        foreach (IGrouping<string, PovEmittedName> clash in clashes)
        {
            Terminal.Out(
                $"  {clash.Key}: " +
                string.Join(", ", clash.Select(name => $"{name.PovName} in {name.Library}")));
        }
    }

    /// <summary>
    /// This method removes a library.
    /// </summary>
    /// <param name="name">The name of the library to remove.</param>
    private static void RemoveLibrary(string name)
    {
        string path = Path.Combine(
            LibraryLocator.LibrariesDirectory,
            Path.HasExtension(name) ? name : $"{name}.igl");

        if (!File.Exists(path))
            Terminal.ShowError($"There is no library named '{name}' in {LibraryLocator.LibrariesDirectory}.");

        File.Delete(path);

        Terminal.Out($"The library, {Path.GetFileNameWithoutExtension(path)}, has been removed.");
    }
}
