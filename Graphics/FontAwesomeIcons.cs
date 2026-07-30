using System.IO.Compression;
using System.Xml.Linq;

namespace RayTracer.Graphics;

/// <summary>
/// This class reads the outline of a FontAwesome icon out of the FontAwesome zip file kept under
/// the ray tracer's own directory, so that an icon may be used as a 2D path.  The zip is put there
/// with the <c>libraries --fa-zip</c> command.
/// <para>
/// An icon is named as <c>style:name</c> -- for example <c>solid:heart</c> -- or as just
/// <c>name</c>, in which case the style is taken to be <c>regular</c>.  The pair names the entry
/// <c>svgs/{style}/{name}.svg</c> in the zip, whose <c>d</c> attribute is the outline.
/// </para>
/// </summary>
public static class FontAwesomeIcons
{
    /// <summary>
    /// This property holds where the FontAwesome zip is kept, beside the library and font
    /// directories under the ray tracer's own directory in the user's profile.
    /// </summary>
    public static readonly string ZipPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".rayTracer", "fontawesome.zip");

    /// <summary>
    /// This method reads the SVG path outline of the named icon from the installed FontAwesome zip.
    /// </summary>
    /// <param name="specification">The icon, as <c>style:name</c> or just <c>name</c>.</param>
    /// <returns>The value of the icon's <c>d</c> attribute.</returns>
    public static string ReadPathData(string specification) =>
        ReadPathData(specification, ZipPath);

    /// <summary>
    /// This method reads the SVG path outline of the named icon from the given FontAwesome zip.  It
    /// is the workhorse behind <see cref="ReadPathData(string)"/>, split out so it can be pointed at
    /// a zip other than the installed one.
    /// </summary>
    /// <param name="specification">The icon, as <c>style:name</c> or just <c>name</c>.</param>
    /// <param name="zipPath">The path of the FontAwesome zip to read from.</param>
    /// <returns>The value of the icon's <c>d</c> attribute.</returns>
    public static string ReadPathData(string specification, string zipPath)
    {
        (string style, string name) = ParseSpecification(specification);

        if (!File.Exists(zipPath))
        {
            throw new Exception(
                $"No FontAwesome zip is installed at '{zipPath}'; add one with " +
                "'libraries --fa-zip'.");
        }

        using ZipArchive archive = ZipFile.OpenRead(zipPath);

        // The desktop download carries both the fuller "svgs-full" outlines and the trimmed "svgs"
        // ones; the web download has only "svgs".  Favor the fuller outline for the most detail, and
        // fall back to the trimmed one so either download works.  The download also wraps everything
        // in a folder named for its version, so the entry is found by the tail of its name wherever
        // it sits, rather than only at the very root of the zip.
        foreach (string folder in new[] { "svgs-full", "svgs" })
        {
            string entryName = $"{folder}/{style}/{name}.svg";
            ZipArchiveEntry entry = archive.Entries.FirstOrDefault(candidate =>
                candidate.FullName == entryName ||
                candidate.FullName.EndsWith("/" + entryName, StringComparison.Ordinal));

            if (entry is not null)
            {
                using Stream stream = entry.Open();

                return ReadPathDataFrom(stream, entryName);
            }
        }

        throw new Exception(
            $"The FontAwesome zip has no icon '{style}/{name}'; check the style and name.");
    }

    /// <summary>
    /// This method splits an icon specification into its style and name, defaulting the style to
    /// <c>regular</c> when only a name is given.
    /// </summary>
    /// <param name="specification">The icon, as <c>style:name</c> or just <c>name</c>.</param>
    /// <returns>The icon's style and name.</returns>
    private static (string style, string name) ParseSpecification(string specification)
    {
        string[] parts = (specification ?? string.Empty).Split(':');

        if (parts.Length is < 1 or > 2 || parts.Any(string.IsNullOrEmpty))
        {
            throw new Exception(
                $"'{specification}' is not a valid icon; name it as 'style:name' or just 'name'.");
        }

        return (parts.Length == 2 ? parts[0] : "regular", parts[^1]);
    }

    /// <summary>
    /// This method reads the value of the <c>d</c> attribute from the icon's SVG.
    /// </summary>
    /// <param name="stream">The stream holding the icon's SVG.</param>
    /// <param name="entryName">The entry the stream came from, for errors.</param>
    /// <returns>The value of the SVG's <c>d</c> attribute.</returns>
    private static string ReadPathDataFrom(Stream stream, string entryName)
    {
        XElement path = XDocument.Load(stream)
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "path");

        string data = path?.Attribute("d")?.Value;

        if (string.IsNullOrEmpty(data))
            throw new Exception($"The icon '{entryName}' has no path outline to read.");

        return data;
    }
}
