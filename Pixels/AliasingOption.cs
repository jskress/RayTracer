using RayTracer.Core;
using RayTracer.Extensions;

namespace RayTracer.Pixels;

/// <summary>
/// This class represents the aliasing option specified by the end user.
/// </summary>
public class AliasingOption
{
    private const string NoAntiAliasing = "off";
    private const string AdaptiveSuperSampling = "adaptive";

    private string _type;
    private int _adaptiveSuperSamplingDepth;
    private double _adaptiveThreshold;

    public AliasingOption()
    {
        _type = NoAntiAliasing;
        _adaptiveSuperSamplingDepth = 5;
        _adaptiveThreshold = AdaptiveSuperSamplingPixelRenderer.DefaultThreshold;
    }

    /// <summary>
    /// This method is used to configure this option based on what the end user specifies.
    /// </summary>
    /// <param name="text">The text the user specified.</param>
    public void Configure(string text)
    {
        if (string.IsNullOrEmpty(text))
            text = $"{AdaptiveSuperSampling}:5";

        if (!text.Contains(':') && int.TryParse(text, out int number))
            text = $"{AdaptiveSuperSampling}:{number}";

        // Up to three parts: what sort, how deep, and how different two samples must be before the
        // sampler looks closer.  The third is separated by a colon like the second rather than given
        // an option of its own, since it is meaningless without the second and the two are read
        // together.
        string[] parts = text.Split(':', 3, StringSplitOptions.TrimEntries);

        if (text == NoAntiAliasing)
            _type = NoAntiAliasing;
        else if (parts[0] == AdaptiveSuperSampling)
        {
            if (parts.Length > 1)
            {
                if (!int.TryParse(parts[1], out number) || number < 0)
                    throw new ArgumentException($"\"{text}\" is not a valid anti-aliasing option.");
            }
            else
                number = 5;

            double threshold = AdaptiveSuperSamplingPixelRenderer.DefaultThreshold;

            if (parts.Length > 2 &&
                (!double.TryParse(parts[2], out threshold) || threshold <= 0 || threshold > 1))
            {
                throw new ArgumentException(
                    $"\"{text}\" is not a valid anti-aliasing option; the threshold must be more " +
                    "than nought and no more than one.");
            }

            _type = AdaptiveSuperSampling;
            _adaptiveSuperSamplingDepth = number;
            _adaptiveThreshold = threshold;
        }
        else
            throw new ArgumentException($"\"{text}\" is not a valid anti-aliasing option.");
    }

    /// <summary>
    /// This method is used to create the appropriate pixel renderer to support the selected
    /// antialiasing option.
    /// </summary>
    /// <param name="converter">The pixel-to-ray converter to use.</param>
    /// <returns>The appropriate pixel renderer.</returns>
    public PixelRenderer GetRenderer(PixelToRayConverter converter)
    {
        return _type switch
        {
            NoAntiAliasing => new NoAntiAliasingPixelRenderer(converter),
            AdaptiveSuperSampling => new AdaptiveSuperSamplingPixelRenderer(
                converter, _adaptiveSuperSamplingDepth, _adaptiveThreshold),
            _ => throw new NotSupportedException($"Unsupported aliasing type: {_type}")
        };
    }

    /// <summary>
    /// This method returns a string representation of this option.  The string returned
    /// is in a form that can be provided to the <see cref="Configure"/> method.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return _type switch
        {
            NoAntiAliasing => NoAntiAliasing,
            // The threshold is only written out when it is not the usual one, so that the common case
            // reads back as it was written.
            AdaptiveSuperSampling =>
                _adaptiveThreshold.Near(AdaptiveSuperSamplingPixelRenderer.DefaultThreshold)
                    ? $"{AdaptiveSuperSampling}:{_adaptiveSuperSamplingDepth}"
                    : $"{AdaptiveSuperSampling}:{_adaptiveSuperSamplingDepth}:{_adaptiveThreshold}",
            _ => throw new NotSupportedException($"Unsupported aliasing type: {_type}")
        };
    }
}
