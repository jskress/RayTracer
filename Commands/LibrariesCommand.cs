using System.IO.Compression;
using System.Reflection;
using RayTracer.Fonts;
using RayTracer.Graphics;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.PovRay;
using RayTracer.Renderer;

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
        else if (options.ImportFrom != null)
            ImportLibrary(options);
        else if (options.RemoveLibrary != null)
            RemoveLibrary(options.RemoveLibrary);
        else if (options.FontAwesomeZip != null)
            InstallFontAwesomeZip(options.FontAwesomeZip);
        else if (options.InstallShipped)
            InstallShippedLibraries(options.Replace);
        else if (options.Povray)
            Terminal.ShowError("--povray only makes sense together with --import.");
        else
            Console.WriteLine("No action was specified.  Use '--help' for a list of options.");
    }

    /// <summary>
    /// This method copies the libraries that ship with the ray tracer into the user's own library
    /// set, where scenes can import from them.
    /// <para>
    /// It is a thing to be asked for rather than something that happens the first time the ray tracer
    /// runs.  Writing into somebody's home directory unbidden is the sort of surprise that is hard to
    /// undo and worse to explain, and a verb can be run again after an update, which a once-only step
    /// at first run cannot.
    /// </para>
    /// <para>
    /// A library already there is left alone unless the user says to overwrite it: the shipped ones
    /// are a starting point, and somebody who has tuned a sky to their liking should not lose it to a
    /// new release of the ray tracer.
    /// </para>
    /// </summary>
    /// <param name="overwrite">Whether to replace libraries that are already there.</param>
    private static void InstallShippedLibraries(bool overwrite)
    {
        Assembly assembly = typeof(LibrariesCommand).Assembly;
        string prefix = $"{assembly.GetName().Name}.Libraries.";
        string[] shipped = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix) && name.EndsWith(".igl"))
            .Order()
            .ToArray();

        if (shipped.Length == 0)
        {
            Terminal.ShowError("This build of the ray tracer carries no libraries of its own.");

            return;
        }

        WarnIfTheBuildIsBehindTheSource(assembly, prefix, shipped);

        Directory.CreateDirectory(LibraryLocator.LibrariesDirectory);

        int written = 0;
        int kept = 0;

        foreach (string resource in shipped)
        {
            string name = resource[prefix.Length..];
            string path = Path.Combine(LibraryLocator.LibrariesDirectory, name);

            if (File.Exists(path) && !overwrite)
            {
                Terminal.Out($"Keeping the '{Path.GetFileNameWithoutExtension(name)}' you already " +
                             "have; use '--overwrite' to replace it.");

                kept++;

                continue;
            }

            using Stream source = assembly.GetManifestResourceStream(resource);
            using StreamReader reader = new (source!);

            File.WriteAllText(path, reader.ReadToEnd());

            Terminal.Out($"Installed '{Path.GetFileNameWithoutExtension(name)}'.");

            written++;
        }

        Terminal.Out(written == 0
            ? $"Nothing to do; all {kept} of them were already there."
            : $"{written} installed into {LibraryLocator.LibrariesDirectory}.");
    }

    /// <summary>
    /// This method warns when the libraries carried inside the assembly no longer match the ones in
    /// the repository they were built from.
    /// <para>
    /// A library ships as an <em>embedded resource</em>, so installing one copies it out of the
    /// assembly and never off disk.  Edit <c>Libraries/trees.igl</c>, install without building, and
    /// the old library is written out over your changes while the command cheerfully reports having
    /// installed it.  Nothing is wrong and nothing is said, which is the worst way for a thing to go
    /// wrong: the edit appears to have been applied and the next render quietly disagrees.  Anyone
    /// then measuring the effect of that edit is measuring nothing at all.
    /// </para>
    /// <para>
    /// This only has anything to say when the ray tracer is running from the tree it was built in --
    /// an installed copy has no repository to compare against, and stays silent.
    /// </para>
    /// </summary>
    /// <param name="assembly">The assembly carrying the libraries.</param>
    /// <param name="prefix">The prefix its library resources are named with.</param>
    /// <param name="shipped">The library resources it carries.</param>
    private static void WarnIfTheBuildIsBehindTheSource(
        Assembly assembly, string prefix, string[] shipped)
    {
        string source = FindSourceLibraries();

        if (source is null)
            return;

        List<string> stale = [];

        foreach (string resource in shipped)
        {
            string name = resource[prefix.Length..];
            string path = Path.Combine(source, name);

            if (!File.Exists(path))
                continue;

            using Stream stream = assembly.GetManifestResourceStream(resource);
            using StreamReader reader = new (stream!);

            if (reader.ReadToEnd() != File.ReadAllText(path))
                stale.Add(Path.GetFileNameWithoutExtension(name));
        }

        if (stale.Count == 0)
            return;

        Terminal.ShowWarning(
            $"The build is behind the source for: {string.Join(", ", stale.Order())}.  A library " +
            "is installed from inside the assembly, not from the folder, so what is about to be " +
            "written is the older text.  Build first, then install.");
    }

    /// <summary>
    /// This method finds the repository's own <c>Libraries</c> folder by walking up from wherever
    /// the program is running, or hands back null when there is no repository above it.
    /// </summary>
    /// <returns>The repository's libraries folder, or null.</returns>
    private static string FindSourceLibraries()
    {
        DirectoryInfo directory = new (AppContext.BaseDirectory);

        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "RayTracer.csproj")))
            directory = directory.Parent;

        if (directory is null)
            return null;

        string libraries = Path.Combine(directory.FullName, "Libraries");

        return Directory.Exists(libraries) ? libraries : null;
    }

    /// <summary>
    /// This method installs a FontAwesome zip file, copying it in as the ray tracer's own so that
    /// scenes may use its icons as 2D paths.  The file must look like a FontAwesome zip -- a zip
    /// holding an <c>svgs</c> folder of icons.
    /// </summary>
    /// <param name="path">The path of the zip file to install.</param>
    private static void InstallFontAwesomeZip(string path)
    {
        string source = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));

        if (!File.Exists(source))
            Terminal.ShowError($"The file, '{source}', does not exist.");

        if (!LooksLikeFontAwesomeZip(source))
        {
            Terminal.ShowError(
                $"'{Path.GetFileName(source)}' does not look like a FontAwesome zip; it has no " +
                "'svgs' folder of icons.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(FontAwesomeIcons.ZipPath)!);
        File.Copy(source, FontAwesomeIcons.ZipPath, overwrite: true);

        Terminal.Out($"The FontAwesome zip was installed at {FontAwesomeIcons.ZipPath}.");
    }

    /// <summary>
    /// This method reports whether the given file looks like a FontAwesome zip: a readable zip that
    /// holds a folder of SVG icons.
    /// </summary>
    /// <param name="path">The path of the file to check.</param>
    /// <returns><c>true</c>, if the file looks like a FontAwesome zip.</returns>
    private static bool LooksLikeFontAwesomeZip(string path)
    {
        try
        {
            using ZipArchive archive = ZipFile.OpenRead(path);

            // The download nests its icons under a version-named folder, and carries them as "svgs"
            // (both downloads) and "svgs-full" (the desktop one), so accept either folder wherever
            // it sits rather than only at the very root.
            return archive.Entries.Any(entry => HasIconFolder(entry.FullName));
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    /// <summary>
    /// This method reports whether the given entry name lies within a FontAwesome icon folder --
    /// <c>svgs</c> or <c>svgs-full</c> -- wherever that folder sits in the zip.
    /// </summary>
    /// <param name="entryName">The full name of a zip entry.</param>
    /// <returns><c>true</c>, if the entry lies within an icon folder.</returns>
    private static bool HasIconFolder(string entryName)
    {
        foreach (string folder in new[] { "svgs/", "svgs-full/" })
        {
            if (entryName.StartsWith(folder, StringComparison.Ordinal) ||
                entryName.Contains("/" + folder, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// This method imports a library, either by converting a POV-Ray distribution or by copying
    /// an .igl file of definitions, depending on whether --povray was given.
    /// </summary>
    /// <param name="options">The options specified by the user on the command line.</param>
    private static void ImportLibrary(LibrariesOptions options)
    {
        if (options.Povray)
            ImportPovRayLibraries(options);
        else
            ImportIglLibrary(options);
    }

    /// <summary>
    /// This method imports a single .igl file as a library, after making sure it holds nothing
    /// but definitions.
    /// </summary>
    /// <param name="options">The options specified by the user on the command line.</param>
    private static void ImportIglLibrary(LibrariesOptions options)
    {
        string source = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), options.ImportFrom));

        if (!File.Exists(source))
            Terminal.ShowError($"The file, '{source}', does not exist.");

        // A library is imported by name, and one is better named for what it holds than for how it
        // is stored, so the extension is dropped from the name it lands under.
        string name = Path.GetFileNameWithoutExtension(source);

        // Read it, so a file that will not parse is turned away here rather than when a scene first
        // tries to import from it, and so we can be sure it is only definitions.  A library that
        // carried a surface, a camera or a render command would drag that into every scene that
        // imported it, which is exactly what an import is meant to avoid.
        ImageRenderer renderer = new LanguageParser(source).Parse();

        if (renderer is null)
            return;

        if (!renderer.HoldsOnlyDefinitions)
        {
            Terminal.ShowError(
                $"'{Path.GetFileName(source)}' cannot be a library: a library may hold only " +
                "definitions (name = ...), but this holds other things as well.");
        }

        if (renderer.DefinitionCount == 0)
            Terminal.ShowError($"'{Path.GetFileName(source)}' defines nothing to import.");

        string target = Path.Combine(LibraryLocator.LibrariesDirectory, $"{name}.igl");

        if (File.Exists(target) && !options.Replace)
        {
            Terminal.ShowError(
                $"A library named '{name}' already exists.  Specify --overwrite to replace it.");
        }

        if (options.DryRun)
        {
            Terminal.Out(
                $"'{name}' holds {renderer.DefinitionCount:n0} definitions and would be imported.  " +
                "Nothing was written, since --dry-run was given.");

            return;
        }

        Directory.CreateDirectory(LibraryLocator.LibrariesDirectory);
        File.Copy(source, target, overwrite: true);

        Terminal.Out(
            $"The library '{name}', holding {renderer.DefinitionCount:n0} definitions, was " +
            $"imported into {LibraryLocator.LibrariesDirectory}.");
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
        .Count(IsADefinition);

    /// <summary>
    /// This method reports whether a line of a library begins a definition: a name bound to something,
    /// or a function or a primitive the library wrote for itself.
    /// <para>
    /// The last two are here because a library may now hold them, and counting only the assignments
    /// left a library of trees reporting a handful of definitions when it holds a score of them.
    /// </para>
    /// </summary>
    /// <param name="line">The line to judge.</param>
    /// <returns>Whether it begins a definition.</returns>
    private static bool IsADefinition(string line)
    {
        if (line.Length == 0 || char.IsWhiteSpace(line[0]) ||
            line.StartsWith("//", StringComparison.Ordinal))
            return false;

        return line.Contains(" = ") ||
               line.StartsWith("function ", StringComparison.Ordinal) ||
               line.StartsWith("primitive ", StringComparison.Ordinal);
    }

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
            Path.Combine(Directory.GetCurrentDirectory(), options.ImportFrom));

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
