using System.Net;
using Newtonsoft.Json;
using RayTracer.Extensions;
using Typography.OpenFont;

namespace RayTracer.Fonts;

/// <summary>
/// This class provides a singleton that manages fonts known by the ray tracer for the
/// text primitive.
/// </summary>
public class FontManager
{
    internal static readonly string FontsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".rayTracerFonts");

    private static readonly string CatalogPath = Path.Combine(FontsDirectory, "fonts.json");

    private static readonly Lazy<FontManager> LazyInstance = new (() => new FontManager());
    private static readonly HttpClientHandler Handler = new ()
    { 
        AutomaticDecompression = DecompressionMethods.All 
    };

    internal static readonly HttpClient HttpClient = new (Handler);

    /// <summary>
    /// This property provides the singleton instance of the font catalog.
    /// </summary>
    public static FontManager Instance => LazyInstance.Value;

    private readonly FontCatalog _catalog;
    private readonly Dictionary<string, Kerning> _kerning;

    private FontManager()
    {
        if (!Directory.Exists(FontsDirectory))
            Directory.CreateDirectory(FontsDirectory);

        if (File.Exists(CatalogPath))
        {
            string json = File.ReadAllText(CatalogPath);

            _catalog = JsonConvert.DeserializeObject<FontCatalog>(json);
        }
        else
        {
            _catalog = new FontCatalog
            {
                FontFamilies = []
            };
        }

        _kerning = new Dictionary<string, Kerning>();
    }

    /// <summary>
    /// This returns a <c>Typeface</c> object for the given font family, face weight and
    /// normal or italic style.  An appropriate exception will be thrown if the requested
    /// font face cannot be made available.
    /// </summary>
    /// <param name="name">The name of the font family.</param>
    /// <param name="weight">The desired weight of the font.</param>
    /// <param name="italic">Whether a normal or italic face is desired.</param>
    /// <returns>The typeface for the requested font face.</returns>
    public Typeface GetGoogleFont(string name, int weight, bool italic)
    {
        FontFace face = GetGoogleFontFace(name, weight, italic);
        using Stream input = File.OpenRead(Path.Combine(FontsDirectory, face.FileName));
        OpenFontReader reader = new OpenFontReader();
        Typeface typeface = reader.Read(input);
        
        _kerning[face.FileName] = face.GetKerning();

        typeface.Filename = face.FileName;

        return typeface;
    }

    /// <summary>
    /// This method is used to determine what the kerning adjustment, if any, there should
    /// be between the two specified code points within the typeface.
    /// </summary>
    /// <param name="typeface">The typeface that gives us our kerning context.</param>
    /// <param name="left">The code point of the left glyph the kerning is for.</param>
    /// <param name="right">The code point of the right glyph the kerning is for.</param>
    /// <returns>The amount to adjust the space between the two code points.</returns>
    public short GetKerningFor(Typeface typeface, int left, int right)
    {
        short result = 0;

        if (_kerning.TryGetValue(typeface.Filename, out Kerning kerning))
            result = kerning.GetKern(left, right);

        if (result == 0 && typeface.HasKernTable())
        {
            ushort leftCp = typeface.LookupIndex(left);
            ushort rightCp = typeface.LookupIndex(right);

            result = typeface.GetKernDistance(leftCp, rightCp);
        }

        return result;
    }

    /// <summary>
    /// This method is used to ensure that we have a true-type font file available for the
    /// given font family, face weight and normal or italic style.  An appropriate exception
    /// will be thrown if the requested font face cannot be made available.
    /// </summary>
    /// <param name="name">The name of the font family.</param>
    /// <param name="weight">The desired weight of the font.</param>
    /// <param name="italic">Whether a normal or italic face is desired.</param>
    /// <returns>The font face that represents the requested font.</returns>
    private FontFace GetGoogleFontFace(string name, int weight, bool italic)
    {
        FontFamily family = _catalog.FontFamilies?
            .FirstOrDefault(family => family.Name == name);
        FontFace face = family?.Faces
            .FirstOrDefault(face => face.Weight == weight && face.Italic == italic);

        // If we don't have the specific font face, we need to go fetch it.
        if (face == null)
        {
            FontFaceLoader loader = new FontFaceLoader(name, weight, italic);

            face = loader.CacheFontFile();

            if (family == null)
            {
                family = new FontFamily
                {
                    Name = name,
                    Faces = [face]
                };

                _catalog.FontFamilies!.Add(family);
            }
            else
                family.Faces.Add(face);

            RewriteCatalog();
        }

        return face;
    }

    /// <summary>
    /// This method is used to persist the current content of the font catalog.
    /// </summary>
    private void RewriteCatalog()
    {
        string json = JsonConvert.SerializeObject(_catalog);

        File.WriteAllText(CatalogPath, json);
    }
}
