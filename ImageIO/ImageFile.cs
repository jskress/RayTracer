using System.Text.RegularExpressions;
using ImageMagick;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Utils;

namespace RayTracer.ImageIO;

/// <summary>
/// This class represents a file that contains an image, either for loading from an
/// existing file or writing an image to the file.
/// </summary>
public class ImageFile
{
    private static readonly IMagickColor<float> Transparent = new MagickColor(0, 0, 0, 0);

    private readonly string _fileName;

    public ImageFile(string name)
    {
        _fileName = name;
    }

    /// <summary>
    /// This method is used to load images from the image file.
    /// </summary>
    /// <returns>An array of the images found in the image file.</returns>
    public Canvas[] Load()
    {
        using Stream stream = GetImageStream();
        using MagickImage image = new MagickImage(stream);
        using IPixelCollection<float> pixels = image.GetPixels();
        Canvas canvas = new Canvas((int) image.Width, (int) image.Height);

        foreach (IPixel<float> pixel in pixels)
        {
            float[] values = pixel.ToArray();
            float red, green, blue, alpha = 0;

            if (pixel.Channels == 1)
                red = green = blue = values[0] / 65535;
            else
            {
                red = values[0] / 65535;
                green = values[1] / 65535;
                blue = values[2] / 65535;

                if (pixel.Channels > 3)
                    alpha = values[3] / 65535;
            }
            
            Color color = pixel.Channels > 3
                ? new Color(red, green, blue, alpha)
                : new Color(red, green, blue);

            canvas.SetColor(color, pixel.X, pixel.Y);
        }

        return [canvas];
    }

    /// <summary>
    /// This method is used to open our source as a stream.
    /// If it looks like an HTTP URL, we'll open it by making an HHTO GET call.
    /// Otherwise, we'll try to open it as a local file.
    /// </summary>
    /// <returns>The stream to read the image from.</returns>
    private Stream GetImageStream()
    {
        if (HttpUtils.LooksLikeUrl(_fileName))
        {
            HttpResponseMessage response = HttpUtils.Get(_fileName);

            response.EnsureSuccessStatusCode();

            return response.Content.ReadAsStreamAsync()
                .GetAwaiter()
                .GetResult();
        }

        return File.OpenRead(_fileName);
    }

    /// <summary>
    /// This method is used to save the given image to the file, in the format
    /// indicated by its extension.
    /// </summary>
    /// <param name="canvas">The image to save.</param>
    /// <param name="info">The information about the image.</param>
    public void Save(Canvas canvas, ImageInformation info = null)
    {
        using MagickImage image = new MagickImage(Transparent, (uint) canvas.Width, (uint) canvas.Height);
        using IPixelCollection<float> pixels = image.GetPixels();

        foreach (IPixel<float> pixel in pixels)
        {
            Color color = canvas.GetPixel(pixel.X, pixel.Y);
            float red = (float) Math.Round(color.Red * 65535);
            float green = (float) Math.Round(color.Green * 65535);
            float blue = (float) Math.Round(color.Blue * 65535);
            float alpha = (float) Math.Round(color.Alpha * 65535);

            pixel.SetValues([red, green, blue, alpha]);
        }

        if (info != null)
            AddInformation(image, info);

        image.Write(_fileName);
    }

    /// <summary>
    /// This method is used to copy all the information we have about an image into the
    /// given image.
    /// </summary>
    /// <param name="image">The image to put the information in.</param>
    /// <param name="info">The info object to pull the information from.</param>
    private static void AddInformation(MagickImage image, ImageInformation info)
    {
        AddAttribute(image, PredefinedTextKeywords.Title, info.Title);
        AddAttribute(image, PredefinedTextKeywords.Author, info.Author);
        AddAttribute(image, PredefinedTextKeywords.Description, info.Description);
        AddAttribute(image, PredefinedTextKeywords.Copyright, info.Copyright);
        AddAttribute(image, PredefinedTextKeywords.CreationTime, info.CreationTime.ToString("r"));
        AddAttribute(image, PredefinedTextKeywords.Software, info.Software);
        AddAttribute(image, PredefinedTextKeywords.Disclaimer, info.Disclaimer);
        AddAttribute(image, PredefinedTextKeywords.Warning, info.Warning);
        AddAttribute(image, PredefinedTextKeywords.Source, info.Source);
        AddAttribute(image, PredefinedTextKeywords.Comment, info.Comment);
    }

    /// <summary>
    /// This is a helper method for conditionally adding an attribute on an image.
    /// </summary>
    /// <param name="image">The image to set an attribute on.</param>
    /// <param name="label">The label for the field.</param>
    /// <param name="value">The value of the field.</param>
    private static void AddAttribute(MagickImage image, string label, string value)
    {
        if (value != null && value.Trim().Length > 0)
            image.SetAttribute(label, value);
    }
}
