using System.Diagnostics.CodeAnalysis;
using CommandLine;

namespace RayTracer;

/// <summary>
/// This class represents the command line arguments that the user may specify.
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class Arguments
{
    /// <summary>
    /// This property makes available our single instance of the command line arguments
    /// that were specified to the application.
    /// </summary>
    public static Arguments Instance { get; private set; } = null!;

    /// <summary>
    /// This method is used to set the instance of the program's arguments.
    /// </summary>
    /// <param name="arguments">The arguments to start using.</param>
    public static void Init(Arguments arguments)
    {
        Instance = arguments;
    }

    [Option('w', "width", Default = 800,
        HelpText = "The width (in pixels) of the image to render.")]
    public int Width { get; set; }

    [Option('h', "height", Default = 600,
        HelpText = "The height (in pixels) of the image to render.")]
    public int Height { get; set; }

    [Option('o', "output", Required = true,
        HelpText = "The file to write the rendered image to.  The extension will " +
                   "determine the format.")]
    public string OutputFile { get; set; } = null!;
}
