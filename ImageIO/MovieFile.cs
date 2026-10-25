using System.Diagnostics;
using ImageMagick;
using ImageMagick.Formats;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.ImageIO;

/// <summary>
/// This class gathers the frames of an animation into one movie file: an animated WebP or GIF, which the renderer
/// writes itself, looping for ever, or an MP4, which it hands to ffmpeg.  Each frame is developed exactly as a still
/// would be.  A WebP or GIF is written once the last frame is in; an MP4's frames go to ffmpeg as they come, so a
/// long one is never held whole.
/// </summary>
public sealed class MovieFile
{
    private readonly string _fileName;
    private readonly double _rate;
    private readonly string _kind;
    private readonly string _ffmpeg;
    private readonly MagickImageCollection _frames = [];
    private Process _encoder;
    private readonly List<string> _complaints = [];

    /// <summary>
    /// This method reports whether a file name names a movie: a WebP, a GIF or an MP4.
    /// </summary>
    public static bool IsAMovie(string fileName) => KindOf(fileName) is ".webp" or ".gif" or ".mp4";

    /// <summary>
    /// This method reports whether a file name names a video, which is a movie even of one frame: there is no
    /// still to write an MP4 as.
    /// </summary>
    public static bool IsAVideo(string fileName) => KindOf(fileName) == ".mp4";

    private static string KindOf(string fileName) => Path.GetExtension(fileName ?? "").ToLowerInvariant();

    /// <summary>
    /// This method returns where ffmpeg is on a search path, or null if it is nowhere on it.
    /// </summary>
    /// <param name="searchPath">The directories to look in, as the PATH variable holds them.</param>
    public static string FfmpegOn(string searchPath)
    {
        string[] names = OperatingSystem.IsWindows() ? ["ffmpeg.exe"] : ["ffmpeg"];

        return (searchPath ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(directory => names.Select(name => Path.Combine(directory.Trim('"'), name)))
            .FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// This constructor makes a movie to be written to a file at a rate.  An MP4 needs ffmpeg, and is refused here,
    /// before anything is rendered, when there is none on the path.
    /// </summary>
    /// <param name="fileName">The file to write.</param>
    /// <param name="rate">The frames a second it plays at.</param>
    /// <param name="searchPath">Where to look for ffmpeg: the PATH unless said.</param>
    public MovieFile(string fileName, double rate, string searchPath = null)
    {
        _fileName = fileName;
        _rate = rate;
        _kind = KindOf(fileName);

        if (_kind != ".mp4")
            return;

        _ffmpeg = FfmpegOn(searchPath ?? Environment.GetEnvironmentVariable("PATH"));

        if (_ffmpeg is null)
        {
            throw new InvalidOperationException(
                "An MP4 is written by ffmpeg, and there is no ffmpeg on the path.  Install it, or ask for a " +
                ".webp or a .gif, which need nothing more.");
        }
    }

    /// <summary>
    /// This method adds a frame, developed as a still would be.
    /// </summary>
    /// <param name="canvas">The frame.</param>
    /// <param name="context">The render context that says how to develop it.</param>
    public void Add(Canvas canvas, RenderContext context)
    {
        MagickImage image = ImageFile.Developed(canvas, context, out _);

        if (_kind == ".mp4")
        {
            using (image)
                Encode(image);

            return;
        }

        // A GIF says how long a frame lasts in hundredths of a second, so 24 frames a second plays at 25; a WebP
        // says it in thousandths.
        uint ticks = _kind == ".gif" ? 100u : 1000u;

        image.AnimationTicksPerSecond = (int) ticks;
        image.AnimationDelay = (uint) Math.Max(1, Math.Round(ticks / _rate));

        if (_kind == ".webp")
            image.Quality = 90;

        _frames.Add(image);
    }

    // An MP4's frame goes straight to ffmpeg as eight-bit RGB, ffmpeg being started at the first, once the size
    // of a frame is known.
    private void Encode(MagickImage image)
    {
        _encoder ??= Started((int) image.Width, (int) image.Height);

        image.Depth = 8;

        byte[] pixels = image.ToByteArray(MagickFormat.Rgb);

        try
        {
            _encoder.StandardInput.BaseStream.Write(pixels);
        }
        catch (IOException)
        {
            throw Failed();
        }
    }

    private Process Started(int width, int height)
    {
        // H.264 in the pixel format every player takes, which wants even sides, so an odd one is padded by a row
        // or column; the quality is high, since it is a render and not a camera's noise being kept.
        ProcessStartInfo start = new (_ffmpeg)
        {
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (string argument in new[]
                 {
                     "-y", "-loglevel", "error",
                     "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", $"{width}x{height}",
                     "-framerate", _rate.ToString(System.Globalization.CultureInfo.InvariantCulture), "-i", "-",
                     "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2",
                     "-c:v", "libx264", "-preset", "slow", "-crf", "16", "-pix_fmt", "yuv420p",
                     "-movflags", "+faststart", _fileName
                 })
        {
            start.ArgumentList.Add(argument);
        }

        Process process = Process.Start(start)!;

        process.ErrorDataReceived += (_, line) =>
        {
            if (!string.IsNullOrWhiteSpace(line.Data))
            {
                lock (_complaints)
                    _complaints.Add(line.Data);
            }
        };
        process.BeginErrorReadLine();

        return process;
    }

    private InvalidOperationException Failed()
    {
        _encoder.WaitForExit();

        string said;

        lock (_complaints)
            said = _complaints.Count == 0 ? $"it stopped with code {_encoder.ExitCode}" : string.Join("  ", _complaints);

        return new InvalidOperationException($"ffmpeg could not write the MP4: {said}");
    }

    /// <summary>
    /// This method writes the movie.
    /// </summary>
    public void Save()
    {
        if (_kind == ".mp4")
        {
            if (_encoder is null)
                return;

            _encoder.StandardInput.Close();
            _encoder.WaitForExit();

            if (_encoder.ExitCode != 0)
                throw Failed();

            _encoder.Dispose();

            return;
        }

        if (_frames.Count == 0)
            return;

        _frames[0].AnimationIterations = 0;

        if (_kind == ".gif")
        {
            // A GIF has 256 colors, quantized here as one palette for the whole movie; dithering keeps the gradients of
            // a render from banding, and laying each frame over the last where they agree keeps the file small.
            _frames.Quantize(new QuantizeSettings { Colors = 256, DitherMethod = DitherMethod.FloydSteinberg });
            _frames.Optimize();
            _frames.Write(_fileName);
        }
        else
        {
            // A WebP keeps every color, lossy at a high quality, its encoder taking the time to make it small.
            _frames.Write(_fileName, new WebPWriteDefines { Lossless = false, Method = 6 });
        }

        _frames.Dispose();
    }
}
