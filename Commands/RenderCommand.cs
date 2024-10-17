using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace RayTracer.Commands;

/// <summary>
/// This class provides the implementation of our "render" command line verb.
/// </summary>
public static class RenderCommand
{
    /// <summary>
    /// This method provides the meat of our "render" command line verb.
    /// </summary>
    /// <param name="options">The options specified by the user on the command line.</param>
    public static void Render(RenderOptions options)
    {
        Terminal.OutputLevel = options.OutputLevel;

        LanguageParser parser = new LanguageParser(options.InputFileName);
        ImageRenderer renderer = parser.Parse();

        try
        {
            renderer?.Render(options);
        }
        catch (Exception exception)
        {
            Terminal.ShowException(exception);
        }
    }
}
