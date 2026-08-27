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
    /// What the process reports when it could not produce the picture it was asked for.
    /// <para>
    /// **A failed render used to report success**, and that is worse than it sounds.  The message was
    /// printed, so a person watching saw it; but anything running renders in bulk -- a batch, a
    /// gallery sweep -- decides whether a render worked by its exit code, and was told every time
    /// that it had.  A whole scene went missing from a sweep that way and the sweep said nothing.
    /// </para>
    /// <para>
    /// This is set rather than thrown, and the process is left to end on its own, so that whatever
    /// was already written stays written and the error keeps the place in the output where it
    /// happened.
    /// </para>
    /// </summary>
    private const int Failed = 1;

    /// <summary>
    /// This method provides the meat of our "render" command line verb.
    /// </summary>
    /// <param name="options">The options specified by the user on the command line.</param>
    public static void Render(RenderOptions options)
    {
        Terminal.OutputLevel = options.OutputLevel;

        try
        {
            LanguageParser parser = new LanguageParser(options.InputFileName);
            ImageRenderer renderer = parser.Parse();

            // A scene that would not parse comes back as nothing at all, and the parser has already
            // said why.  There is no picture, so this did not succeed, and it must not report that
            // it did.
            if (renderer is null)
            {
                Environment.ExitCode = Failed;

                return;
            }

            renderer.Render(options);
        }
        catch (Exception exception)
        {
            Terminal.ShowException(exception);

            Environment.ExitCode = Failed;
        }
    }
}
