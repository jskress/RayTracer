namespace RayTracer.Fonts;

/// <summary>
/// This class is used to load the file that Google provides for a font face.
/// </summary>
internal class FontFaceLoader
{
    private const string BaseUrl = "https://fonts.googleapis.com/css2";

    private readonly string _name;
    private readonly int _weight;
    private readonly bool _italic;
    private readonly string _url;

    internal FontFaceLoader(string name, int weight, bool italic)
    {
        string marker = italic ? "1" : "0";

        _name = name;
        _weight = weight;
        _italic = italic;

        name = name.Replace(' ', '+');

        // ReSharper disable once StringLiteralTypo
        _url = $"{BaseUrl}?family={name}:ital,wght@{marker},{weight}";
    }

    /// <summary>
    /// This method is used to load the font face we were created with from Google.
    /// </summary>
    /// <returns>The font face that represents the font we just downloaded.</returns>
    internal FontFace CacheFontFile()
    {
        string content = LoadCssFile();
        (string path, string fileName) = ParseCssContent(content);

        DownloadFontFile(path, fileName);

        return new FontFace
        {
            Weight = _weight,
            Italic = _italic,
            FileName = fileName
        };
    }

    /// <summary>
    /// This method is used to download Google's CSS file that describes the font face we
    /// want.
    /// </summary>
    /// <returns>The content of Google's CSS file for the font face.</returns>
    private string LoadCssFile()
    {
        using HttpResponseMessage message = FontManager.HttpClient.GetAsync(_url)
            .GetAwaiter()
            .GetResult();
        string content = message.Content.ReadAsStringAsync()
            .GetAwaiter()
            .GetResult();

        if (!message.IsSuccessStatusCode)
        {
            int p = content.IndexOf("</ins></p><p>", StringComparison.Ordinal);

            if (p > 0)
            {
                int q = content.IndexOf(", {", p, StringComparison.Ordinal);
                
                content = content[(p + "</ins></p><p>".Length)..q].Replace("<p>", "\n       ");
            }

            throw new Exception($"Error fetching font named, {_name}\nError: {content}");
        }

        return content;
    }

    /// <summary>
    /// This method is used to parse the CSS file that Google provides us about our font
    /// face.  We return the URL path and file name of the actual font file to download.
    /// </summary>
    /// <param name="content">The CSS content to parse.</param>
    /// <returns>A tuple containing the URL path and file name for the font file we need
    /// to download.</returns>
    private (string, string) ParseCssContent(string content)
    {
        string line = content.Split('\n')
            .FirstOrDefault(line => line.Trim().StartsWith("src: url("));

        if (line == null)
            throw new Exception($"Error fetching font named, {_name}; could not find source attribute.");

        // ReSharper disable once StringLiteralTypo
        if (!line.Trim().Contains("format('truetype')"))
            throw new Exception($"Error fetching font named, {_name}; font format is not true-type.");

        int p = line.IndexOf('(') + 1;
        int q = line.IndexOf(')', p);

        line = line[p..q];

        p = line.LastIndexOf('/') + 1;
        
        return (line[..p], line[p..]);
    }

    /// <summary>
    /// This method is used to actually download the indicated true-type font file.
    /// </summary>
    /// <param name="path">The URL path to the file.</param>
    /// <param name="fileName">The file name.</param>
    private void DownloadFontFile(string path, string fileName)
    {
        using HttpResponseMessage message = FontManager.HttpClient.GetAsync(path + fileName)
            .GetAwaiter()
            .GetResult();

        if (message.IsSuccessStatusCode)
        {
            string target = Path.Combine(FontManager.FontsDirectory, fileName);
            using FileStream fileStream = new FileStream(target, FileMode.Create, FileAccess.Write);

            message.Content.ReadAsStream().CopyTo(fileStream);
        }
        else
        {
            string content = message.Content.ReadAsStringAsync()
                .GetAwaiter()
                .GetResult();

            throw new Exception($"Error downloading font file for font named, {_name}\nError: {content}");
        }
    }
}
