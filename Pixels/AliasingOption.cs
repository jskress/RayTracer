using RayTracer.Core;

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

    public AliasingOption()
    {
        _type = NoAntiAliasing;
        _adaptiveSuperSamplingDepth = 5;
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
            
        string[] parts = text.Split(':', 2, StringSplitOptions.TrimEntries);

        if (text == NoAntiAliasing)
            _type = NoAntiAliasing;
        else if (parts[0] == AdaptiveSuperSampling)
        {
            if (parts.Length == 2)
            {
                if (!int.TryParse(parts[1], out number) || number < 0)
                    throw new ArgumentException($"\"{text}\" is not a valid anti-aliasing option.");
            }
            else
                number = 5;

            _type = AdaptiveSuperSampling;
            _adaptiveSuperSamplingDepth = number;
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
            AdaptiveSuperSampling => new AdaptiveSuperSamplingPixelRenderer(converter, _adaptiveSuperSamplingDepth),
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
            AdaptiveSuperSampling => $"{AdaptiveSuperSampling}:{_adaptiveSuperSamplingDepth}",
            _ => throw new NotSupportedException($"Unsupported aliasing type: {_type}")
        };
    }
}
