using ImageMagick;
using ImageMagick.Formats;
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

        // Which channel is which is read from the image rather than guessed from how many there
        // are.  Counting them is not enough: an opaque eight-bit gray PNG comes back with two
        // channels, and the second of them is `Index` rather than `Alpha` -- so a reader that
        // assumes two channels means gray-plus-alpha takes something that is not alpha for it, and
        // an opaque image loads as very nearly invisible.  That is exactly what happened to the
        // gray images this renderer had only just started writing.
        List<PixelChannel> channels = image.Channels.ToList();
        int alphaChannel = channels.IndexOf(PixelChannel.Alpha);
        int colorChannels = channels.Count(
            channel => channel is not (PixelChannel.Alpha or PixelChannel.Index));

        foreach (IPixel<float> pixel in pixels)
        {
            float[] values = pixel.ToArray();
            float red, green, blue;

            if (colorChannels < 3)
                red = green = blue = values[0] / 65535;
            else
            {
                red = values[0] / 65535;
                green = values[1] / 65535;
                blue = values[2] / 65535;
            }

            Color color = alphaChannel < 0
                ? new Color(red, green, blue)
                : new Color(red, green, blue, values[alphaChannel] / 65535);

            canvas.SetColor(color, pixel.X, pixel.Y);
        }

        canvas.Encoding = EncodingOf(image);

        return [canvas];
    }

    /// <summary>
    /// This method works out the curve an image's values were written with, from what the file says of
    /// itself.
    /// <para>
    /// Radiance and OpenEXR files hold light whatever else they say -- Magick.NET reports an OpenEXR file
    /// as sRGB, but with a gamma of one -- and so does a file whose color space is a linear one.  Anything
    /// else declares a gamma: 0.4545 is how Magick.NET reports sRGB, which is what PNG and JPEG files
    /// carry unless they say otherwise, and any other gamma is taken as a plain power.
    /// </para>
    /// </summary>
    /// <param name="image">The image as read.</param>
    /// <returns>The curve its values were written with.</returns>
    private static TransferFunction EncodingOf(MagickImage image)
    {
        if (image.Format is MagickFormat.Exr or MagickFormat.Hdr or MagickFormat.Pfm ||
            image.ColorSpace is ColorSpace.RGB or ColorSpace.LinearGray)
            return TransferFunction.Linear;

        double gamma = image.Gamma;

        return gamma <= 0 || Math.Abs(gamma - 1 / 2.2) < 0.001
            ? TransferFunction.Srgb
            : TransferFunction.Power(gamma);
    }

    /// <summary>
    /// This method is used to open our source as a stream.
    /// If it looks like an HTTP URL, we'll open it by making an HTTP GET call.
    /// Otherwise, we'll try to open it as a local file.
    /// </summary>
    /// <returns>The stream to read the image from.</returns>
    private Stream GetImageStream()
    {
        if (HttpUtils.LooksLikeUrl(_fileName))
        {
            using HttpResponseMessage response = HttpUtils.Get(_fileName);

            response.EnsureSuccessStatusCode();

            MemoryStream memoryStream = new MemoryStream();

            response.Content.ReadAsStream().CopyTo(memoryStream);
            memoryStream.Position = 0;

            return memoryStream;
        }

        return File.OpenRead(_fileName);
    }

    /// <summary>
    /// This method returns a canvas developed into an image: each pixel's light carried through the film's curve
    /// and the output's, quantized to the context's channel depth, gray if asked for.  It is what a still is
    /// written from, and each frame of a movie.
    /// </summary>
    /// <param name="canvas">The canvas to develop.</param>
    /// <param name="context">The render context that says how.</param>
    /// <param name="anyTransparency">Whether any pixel came out less than opaque.</param>
    /// <returns>The image, the caller's to dispose of.</returns>
    internal static MagickImage Developed(Canvas canvas, RenderContext context, out bool anyTransparency)
    {
        MagickImage image = new MagickImage(Transparent, (uint) canvas.Width, (uint) canvas.Height);
        using IPixelCollection<float> pixels = image.GetPixels();

        // Our channel values are quantized to the context's configured channel depth, so
        // scale them back up to fill out the 16-bit range each Magick.NET pixel expects.
        double scale = 65535.0 / context.MaxColorChannelValue;

        anyTransparency = false;

        foreach (IPixel<float> pixel in pixels)
        {
            Color color = canvas.GetPixel(pixel.X, pixel.Y);
            int red, green, blue, alpha;

            // Asked for a gray image, each color is weighted down to the one value that stands for
            // its brightness here, rather than the canvas being converted in place -- the canvas
            // belongs to the caller and may be wanted again.
            if (context.Grayscale)
            {
                (int gray, int grayAlpha) = color.ToGrayValue(context);

                red = green = blue = gray;
                alpha = grayAlpha;
            }
            else
                (red, green, blue, alpha) = color.ToChannelValues(context);

            if (alpha < context.MaxColorChannelValue)
                anyTransparency = true;

            pixel.SetValues([
                (float) (red * scale), (float) (green * scale),
                (float) (blue * scale), (float) (alpha * scale)
            ]);
        }

        return image;
    }

    /// <summary>
    /// This method is used to save the given image to the file, in the format
    /// indicated by its extension.
    /// </summary>
    /// <param name="canvas">The image to save.</param>
    /// <param name="context">The current render context, which carries the gamma
    /// correction and color channel depth to render the colors with.</param>
    /// <param name="info">The information about the image.</param>
    public void Save(Canvas canvas, RenderContext context, ImageInformation info = null)
    {
        MagickFormat? format = MagickFormatInfo.Create(new FileInfo(_fileName))?.Format;

        if (format is MagickFormat.Exr or MagickFormat.Hdr or MagickFormat.Pfm)
        {
            SaveLight(canvas, context, info);

            return;
        }

        using MagickImage image = Developed(canvas, context, out bool anyTransparency);

        if (info != null)
            AddInformation(image, info);

        // The PNG is written at the channel depth the image was rendered with -- 8 bits by
        // default, 16 when asked for -- so it carries exactly the precision it holds rather than
        // padding 8-bit data into a 16-bit container.  The scaling above fills Magick's 16-bit
        // pixels either way; writing at 8 bits then folds each channel back down losslessly,
        // since the values are already quantized to 8 bits.
        //
        // The color type is always stated rather than left to Magick.NET, and which one is stated
        // depends on whether any pixel in the image is less than fully opaque.  An image that is
        // opaque throughout has no use for a fourth channel and is written without one; an image
        // with any transparency in it keeps the channel that records it.
        //
        // Both halves of that matter.  Stating it is what keeps Magick.NET from choosing for itself:
        // left alone, its PNG writer opportunistically shrinks a low-color-variety image to a palette
        // or gray encoding, and those come back through Load() as something other than the true
        // RGB(A) layout it reads.  And choosing by the image's own content is what keeps an opaque
        // render from carrying a channel of nothing but 255s -- which is what this did for a while,
        // having stated TrueColorAlpha unconditionally to solve the first problem without noticing
        // the second.
        if (format == MagickFormat.Png)
        {
            // A gray image is written in a gray container rather than as three equal channels, which
            // is both smaller and honest about what it holds.  `Load` already reads one- and
            // two-channel images, so this still comes back as what was written -- which is the rule
            // every branch here has to meet.  What is never written is a palette: a palette turns
            // the colors into indices into a table, and an image read back from one is not the
            // image that was written.
            ColorType colorType = context.Grayscale
                ? anyTransparency ? ColorType.GrayscaleAlpha : ColorType.Grayscale
                : anyTransparency ? ColorType.TrueColorAlpha : ColorType.TrueColor;

            image.Write(_fileName, new PngWriteDefines
            {
                ColorType = colorType,
                BitDepth = (uint) context.BitsPerChannel
            });

            // A gamma given outright is a plain power, and the file says which, since a PNG that says
            // nothing is read as sRGB; one written with the sRGB curve need say nothing.
            if (context.ApplyGamma && context.GammaIsSaid)
                PngGamma.Declare(_fileName, 1 / context.Gamma);
        }
        else
            image.Write(_fileName);
    }

    /// <summary>
    /// This method saves the given image to a file that holds light rather than a picture of it -- OpenEXR,
    /// Radiance or a portable float map -- as the light it is: neither curved for a screen, nor clamped at
    /// white, nor cut down to the channel depth.  Until 2026-10-19 such a file was written as every other
    /// was, and held an 8-bit picture under an HDR name.
    /// </summary>
    /// <param name="canvas">The image to save.</param>
    /// <param name="context">The current render context, which says whether the image is wanted gray.</param>
    /// <param name="info">The information about the image.</param>
    private void SaveLight(Canvas canvas, RenderContext context, ImageInformation info)
    {
        using MagickImage image = new MagickImage(Transparent, (uint) canvas.Width, (uint) canvas.Height);

        // The image is said to hold light before anything is put in it.  Left as sRGB, Magick.NET turned
        // its values into light on the way into a Radiance file, as if they were a picture's, and 3 came
        // out as 12.8.
        image.ColorSpace = ColorSpace.RGB;

        using IPixelCollection<float> pixels = image.GetPixels();

        bool anyTransparency = false;

        foreach (IPixel<float> pixel in pixels)
        {
            Color color = canvas.GetPixel(pixel.X, pixel.Y);
            double red = Light(color.Red), green = Light(color.Green), blue = Light(color.Blue);

            if (context.Grayscale)
                red = green = blue = red * 0.299 + green * 0.587 + blue * 0.114;

            double alpha = Math.Clamp(Light(color.Alpha), 0, 1);

            if (alpha < 1)
                anyTransparency = true;

            pixel.SetValues([
                (float) (red * 65535), (float) (green * 65535), (float) (blue * 65535), (float) (alpha * 65535)
            ]);
        }

        if (!anyTransparency)
            image.Alpha(AlphaOption.Off);

        if (info != null)
            AddInformation(image, info);

        image.Write(_fileName);
    }

    // What a file of light can hold of a channel: anything but less than nothing, or nothing at all.
    private static double Light(double value) => double.IsNaN(value) ? 0 : Math.Max(value, 0);

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
