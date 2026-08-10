using CommandLine;

namespace RayTracer.Options;

/// <summary>
/// This class represents the command line options that the user may specify to the ray tracer for
/// managing the libraries a scene may import from.
/// </summary>
[Verb("libraries", HelpText = "This command is used to inspect and manage the libraries of definitions that scenes may import from.")]
// ReSharper disable once ClassNeverInstantiated.Global
public class LibrariesOptions
{
    [Option('l', "list", Required = false, SetName = "list",
        HelpText = "Specifying this will list the libraries the ray tracer knows about.")]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public bool ListLibraries { get; set; }

    [Option('i', "import", Required = false, SetName = "import",
        HelpText = "Imports a library.  Normally the value is an .igl file of definitions, which is copied into the library directory.  With --povray, it is instead the 'include' directory of a POV-Ray distribution, whose texture files are converted into libraries.")]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public string ImportFrom { get; set; }

    [Option('p', "povray", Required = false,
        HelpText = "Used with --import to say the source is a POV-Ray distribution's 'include' directory to convert, rather than a single .igl file to copy.")]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public bool Povray { get; set; }

    [Option("install", Required = false, SetName = "install",
        HelpText = "This option installs the libraries that ship with the ray tracer into your own " +
                   "library set.  An existing file of the same name is left alone unless " +
                   "'--overwrite' is given, so a library you have edited is never quietly replaced.")]
    public bool InstallShipped { get; set; }

    [Option('r', "remove", Required = false, SetName = "remove",
        HelpText = "Removes a library from the ray tracer's library directory.")]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public string RemoveLibrary { get; set; }

    [Option("fa-zip", Required = false, SetName = "fa-zip",
        HelpText = "Installs a FontAwesome zip file, so that scenes may use its icons as 2D paths.  The value is the path to the zip; it is copied in as the ray tracer's own.")]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public string FontAwesomeZip { get; set; }

    [Option('o', "overwrite", Required = false,
        HelpText = "Specifying this will allow existing libraries to be replaced when importing.")]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public bool Replace { get; set; }

    [Option('d', "details", Required = false,
        HelpText = "Specifying this will report every definition that could not be converted, rather than a count of each sort.")]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public bool ShowDetails { get; set; }

    [Option('n', "dry-run", Required = false,
        HelpText = "Specifying this will convert and report, but write nothing.")]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public bool DryRun { get; set; }
}
