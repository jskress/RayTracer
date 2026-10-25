using CommandLine.Text;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Instructions;
using RayTracer.Options;

namespace RayTracer.Renderer;

/// <summary>
/// This class provides the work of actually rendering images.
/// </summary>
public class ImageRenderer
{
    private readonly InstructionContext _instructionContext;
    private readonly Variables _globals;

    public ImageRenderer(InstructionContext instructionsContext)
    {
        string software = HeadingInfo.Default.ToString();

        _instructionContext = instructionsContext;
        _globals = new Variables();

        _globals.SetValue("__software__", software);
        _globals.SetValue("π", Math.PI);
        _globals.SetValue("Identity", Matrix.Identity);
        _globals.SetValue("NegativeInfinity", double.NegativeInfinity);
        _globals.SetValue("PositiveInfinity", double.PositiveInfinity);

        Colors.AddToVariables(_globals);
        IndicesOfRefraction.AddToVariables(_globals);
        Directions.AddToVariables(_globals);
    }

    /// <summary>
    /// This property reports whether the parsed source holds only definitions, as a file offered
    /// as a library must.  See <see cref="Commands.LibrariesCommand"/>.
    /// </summary>
    public bool HoldsOnlyDefinitions => _instructionContext.HoldsOnlyDefinitions;

    /// <summary>
    /// This property reports how many definitions the parsed source holds.
    /// </summary>
    public int DefinitionCount => _instructionContext.DefinitionCount;

    /// <summary>
    /// This method is used to render all images called for in our input files.
    /// </summary>
    /// <param name="options">The command line options supplied by the user.</param>
    public void Render(RenderOptions options)
    {
        Animation animation = _instructionContext.AnimationOf(FrameScope(0, 0));

        bool video = MovieFile.IsAVideo(options.OutputFileName);

        // A scene that is no animation, rendered with no frame asked for, is a still, as it always was -- and so
        // is one asked for as a still, which is how a gallery of stills is rendered whatever each scene is.  A
        // video has no still to be, and is a movie of the one frame.
        if (animation is null && options.Frame is null && options.FrameRun is null && !video)
        {
            RenderImage(options, 0, 0, 0, null);

            return;
        }

        double rate = options.FrameRate ?? animation?.Rate ?? Animation.DefaultRate;
        // One frame alone: the one asked for, the still asked for, or -- a video of a scene that is no animation,
        // with no run of frames asked for -- the only one there is.
        bool stillAsked = options.Still || (animation is null && options.FrameRun is null);

        if (animation is not null && (animation.Still < 0 || animation.Still >= animation.FrameCount(rate)))
        {
            throw new ArgumentException(
                $"The animation names frame {animation.Still} as its still, but its frames run from 0 to " +
                $"{animation.FrameCount(rate) - 1}.");
        }
        long? alone = options.Frame ?? (stillAsked ? animation?.Still ?? 0 : null);
        (long first, long last) = alone is { } one
            ? (one, one)
            : options.FrameRun ?? (0, animation!.FrameCount(rate) - 1);
        bool several = last > first;
        // A run of frames is numbered however many it holds: `--frames 7` is the run of frame 7 alone, and writes
        // the very file a run of 0-143 writes for it.
        bool run = alone is null;

        // Made before the first frame is rendered, so that an MP4 with no ffmpeg to write it is refused at once
        // rather than after the whole animation has been rendered for nothing.
        MovieFile movie = (run && MovieFile.IsAMovie(options.OutputFileName)) || video
            ? new MovieFile(options.OutputFileName, rate)
            : null;
        FrameOutput output = new (run && movie is null, movie);

        for (long frame = first; frame <= last; frame++)
        {
            if (several)
                Terminal.Out($"Frame {frame} ({frame - first + 1} of {last - first + 1})");

            RenderImage(options, frame, frame / rate, 1 / rate, output);
        }

        movie?.Save();
    }

    /// <summary>
    /// This method returns the scope a frame is carried out in: the renderer's own names, and the frame's number
    /// and time, which a scene may write anything in terms of.  The scene's own names go in a scope of their own
    /// within it, so a scene that names something `time` or `frame` for itself is free to.
    /// </summary>
    private Variables FrameScope(long frame, double time)
    {
        Variables scope = new (_globals);

        scope.SetValue("time", time);
        scope.SetValue("frame", (double) frame);

        return scope;
    }

    /// <summary>
    /// This method is used to render one particular frame.
    /// </summary>
    /// <param name="options">The command line options supplied by the user.</param>
    /// <param name="frame">The frame to render.</param>
    /// <param name="time">The frame's time, in seconds.</param>
    /// <param name="interval">How long a frame lasts, in seconds, or nought for a still.</param>
    /// <param name="output">Where a frame of an animation goes, or null for a still.</param>
    private void RenderImage(RenderOptions options, long frame, double time, double interval, FrameOutput output)
    {
        // Counted afresh for every frame, so that what a frame reports is that frame's alone rather than the sum of
        // every frame so far.
        Statistics statistics = new ();
        RenderContext context = new ()
        {
            Progress = ProgressReporterFor(options, statistics),
            Statistics = statistics,
            Time = time,
            FrameInterval = interval
        };
        Variables variables = new (FrameScope(frame, time));

        _instructionContext.Execute(options, context, variables, frame, output);

        // The tool style's reader is a program, and gets the counts as the one line of key/value text
        // it has always had.  A person asks for them with `--stats`, and gets them laid out to read,
        // with what the scene held, however progress was reported.
        if (options.ProgressStyle == ProgressStyle.Tool)
            Terminal.OutLine(statistics.AsText());

        if (options.ReportStatistics)
        {
            Terminal.OutLine("");

            foreach (string line in statistics.AsReport(Path.GetFileName(options.InputFileName)))
                Terminal.OutLine(line);
        }
    }

    /// <summary>
    /// This method creates the thing the render will report its progress through.  This is the only
    /// place the choice is made: everything downstream of here knows nothing but the interface.
    /// <para>
    /// **`quiet` keeps the bar back.**  It says nothing until the render is done, and the bar, being
    /// the default, is not something anybody asked for in particular; it used to draw regardless, once
    /// a render ran past the bar's two seconds.  The tool style is asked for by name, by a program that
    /// wants its lines, so it reports whatever the output level, as `--stats` does.
    /// </para>
    /// </summary>
    /// <param name="options">The command line options supplied by the user.</param>
    /// <param name="statistics">The counts the tool style reports from.</param>
    /// <returns>The progress reporter to use, or null for no reporting at all.</returns>
    public static IProgressReporter ProgressReporterFor(RenderOptions options, Statistics statistics)
    {
        return options.ProgressStyle switch
        {
            ProgressStyle.Bar when options.OutputLevel == OutputLevel.Quiet => null,
            ProgressStyle.Bar => new ProgressBar(),
            ProgressStyle.Tool => new ToolProgressReporter(statistics),
            _ => null
        };
    }
}
