using RayTracer.Graphics;
using RayTracer.Scanners;

namespace RayTracer.Core.Denoising;

/// <summary>
/// This enumeration names the ways a picture's grain may be smoothed out once it is rendered.
/// </summary>
public enum DenoiseMethod
{
    /// <summary>
    /// The picture is left as it was rendered.
    /// </summary>
    Off,

    /// <summary>
    /// Intel's denoiser, when its tool is on the path, else the renderer's own filter.
    /// </summary>
    Best,

    /// <summary>
    /// Intel's denoiser; refused, before anything is rendered, when its tool is not on the path.
    /// </summary>
    Intel,

    /// <summary>
    /// The renderer's own filter.
    /// </summary>
    Filter
}

/// <summary>
/// This class smooths the grain out of a rendered picture by whichever denoiser was asked for (see
/// <see cref="DenoiseMethod"/>): Intel's (<see cref="IntelDenoiser"/>) or the renderer's own
/// (<see cref="WaveletFilter"/>), each guided by what every pixel shows (<see cref="Guides"/>).
/// </summary>
internal sealed class Denoiser
{
    // Said once in a run, however many frames ask: that the best was asked for and Intel's could not be found.
    private static int _fellBack;

    /// <summary>
    /// This property holds the denoiser this one is.
    /// </summary>
    internal DenoiseMethod Method { get; }

    private readonly string _tool;

    private Denoiser(DenoiseMethod method, string tool)
    {
        Method = method;
        _tool = tool;
    }

    /// <summary>
    /// This method settles which denoiser is to smooth a picture, before it is rendered, so that one that cannot be
    /// had is refused before the render's time is spent rather than after.
    /// </summary>
    /// <param name="asked">The denoising asked for.</param>
    /// <param name="searchPath">Where to look for Intel's tool: the PATH unless said.</param>
    /// <returns>The denoiser, or <c>null</c> for none.</returns>
    internal static Denoiser For(DenoiseMethod asked, string searchPath = null)
    {
        if (asked == DenoiseMethod.Off)
            return null;

        if (asked == DenoiseMethod.Filter)
            return new Denoiser(DenoiseMethod.Filter, null);

        string tool = IntelDenoiser.ToolOn(searchPath ?? Environment.GetEnvironmentVariable("PATH"));

        if (tool is not null)
            return new Denoiser(DenoiseMethod.Intel, tool);

        if (asked == DenoiseMethod.Intel)
        {
            throw new InvalidOperationException(
                "Denoising by Intel's Open Image Denoise needs its oidnDenoise tool on the path, and there is none: " +
                "install it (\"brew install open-image-denoise\" on a Mac) or say \"denoise filter\" for the " +
                "renderer's own.");
        }

        if (Interlocked.Exchange(ref _fellBack, 1) == 0)
        {
            Terminal.Out(
                "Note: Intel's denoiser (oidnDenoise) is not on the path, so the renderer's own filter is used.");
        }

        return new Denoiser(DenoiseMethod.Filter, null);
    }

    /// <summary>
    /// This method smooths the grain out of a picture's light in place.
    /// </summary>
    /// <param name="canvas">The picture, holding light.</param>
    /// <param name="guides">What each of its pixels shows.</param>
    /// <param name="scanner">What to go over its pixels with.</param>
    internal void Denoise(Canvas canvas, Guides guides, IScanner scanner)
    {
        if (Method == DenoiseMethod.Intel)
            IntelDenoiser.Apply(canvas, guides, _tool);
        else
            WaveletFilter.Apply(canvas, guides, scanner);
    }
}
