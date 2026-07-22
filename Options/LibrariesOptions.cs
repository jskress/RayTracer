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

    [Option('p', "import-povray", Required = false, SetName = "import-povray",
        HelpText = "Converts POV-Ray's texture include files into libraries.  The value is the directory those files live in, which is usually the 'include' directory of a POV-Ray distribution.")]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public string ImportPovRayFrom { get; set; }

    [Option('r', "remove", Required = false, SetName = "remove",
        HelpText = "Removes a library from the ray tracer's library directory.")]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public string RemoveLibrary { get; set; }

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
