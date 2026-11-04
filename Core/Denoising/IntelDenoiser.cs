using System.Diagnostics;
using RayTracer.Graphics;
using RayTracer.ImageIO;

namespace RayTracer.Core.Denoising;

/// <summary>
/// This class denoises a picture with Intel's Open Image Denoise, by its own command-line tool, <c>oidnDenoise</c>,
/// found on the path as ffmpeg is for a movie (see <see cref="MovieFile.FfmpegOn"/>): a network trained on rendered
/// pictures, the best denoiser freely to be had, handed the picture's light, its colors and its facings as Portable
/// FloatMaps and handing the light back.
/// <para>
/// The light is handed over as it stands, exposed but undeveloped, and the denoiser told it is light of any
/// brightness (<c>--hdr</c>).  The guides are told it they hold no grain of their own (<c>--clean_aux</c>), which
/// is so: nothing in them is drawn by chance (see <see cref="Guides"/>).  A pixel's coverage is the picture's own,
/// and is left as it was.
/// </para>
/// </summary>
internal static class IntelDenoiser
{
    /// <summary>
    /// This method finds Intel's denoising tool on a search path.
    /// </summary>
    /// <param name="searchPath">The directories to look in, as the PATH variable holds them.</param>
    /// <returns>The tool's full path, or <c>null</c> when none of the directories holds it.</returns>
    internal static string ToolOn(string searchPath)
    {
        string[] names = OperatingSystem.IsWindows() ? ["oidnDenoise.exe"] : ["oidnDenoise"];

        return (searchPath ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(directory => names.Select(name => Path.Combine(directory.Trim('"'), name)))
            .FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// This method denoises a picture's light in place.
    /// </summary>
    /// <param name="canvas">The picture, holding light.</param>
    /// <param name="guides">What each of its pixels shows.</param>
    /// <param name="tool">Where Intel's tool is.</param>
    internal static void Apply(Canvas canvas, Guides guides, string tool)
    {
        int width = canvas.Width;
        int height = canvas.Height;
        string folder = Path.Combine(Path.GetTempPath(), $"raytracer-denoise-{Guid.NewGuid():N}");

        Directory.CreateDirectory(folder);

        try
        {
            string light = Path.Combine(folder, "light.pfm");
            string albedo = Path.Combine(folder, "albedo.pfm");
            string normal = Path.Combine(folder, "normal.pfm");
            string output = Path.Combine(folder, "denoised.pfm");
            float[] values = new float[width * height * 3];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color color = canvas.GetPixel(x, y);
                    int at = (y * width + x) * 3;

                    values[at] = (float) Math.Max(0, color.Red);
                    values[at + 1] = (float) Math.Max(0, color.Green);
                    values[at + 2] = (float) Math.Max(0, color.Blue);
                }
            }

            PortableFloatMap.Write(light, width, height, values);
            PortableFloatMap.Write(albedo, width, height, guides.Albedo);
            PortableFloatMap.Write(normal, width, height, guides.Normal);

            Run(tool, "--hdr", light, "--alb", albedo, "--nrm", normal, "--clean_aux", "-o", output);

            (int readWidth, int readHeight, float[] denoised) = PortableFloatMap.Read(output);

            if (readWidth != width || readHeight != height)
                throw new InvalidOperationException("Intel's denoiser handed back a picture of another size.");

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int at = (y * width + x) * 3;

                    canvas.SetColor(
                        new Color(denoised[at], denoised[at + 1], denoised[at + 2], canvas.GetPixel(x, y).Alpha), x, y);
                }
            }
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    /// This method runs the tool and waits for it, refusing when it fails.
    /// </summary>
    private static void Run(string tool, params string[] arguments)
    {
        ProcessStartInfo start = new (tool)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (string argument in arguments)
            start.ArgumentList.Add(argument);

        using Process process = Process.Start(start)!;
        Task<string> said = process.StandardOutput.ReadToEndAsync();
        Task<string> complained = process.StandardError.ReadToEndAsync();

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string why = $"{complained.Result}{said.Result}".Trim();

            throw new InvalidOperationException(
                $"Intel's denoiser failed (exit code {process.ExitCode}){(why.Length > 0 ? $": {why}" : ".")}");
        }
    }
}
