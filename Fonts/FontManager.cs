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
        ".rayTracer", "Fonts");

    private static readonly string CatalogPath = Path.Combine(FontsDirectory, "fonts.json");

    private static readonly Lazy<FontManager> LazyInstance = new (() => new FontManager());

    /// <summary>
    /// This property provides the singleton instance of the font catalog.
    /// </summary>
    public static FontManager Instance => LazyInstance.Value;

    /// <summary>
    /// This property holds the catalog of known font information.
    /// </summary>
    public FontCatalog Catalog { get; }

    private readonly Dictionary<string, Kerning> _kerning;

    private FontManager()
    {
        if (!Directory.Exists(FontsDirectory))
            Directory.CreateDirectory(FontsDirectory);

        if (File.Exists(CatalogPath))
        {
            string json = File.ReadAllText(CatalogPath);

            Catalog = JsonConvert.DeserializeObject<FontCatalog>(json);
        }
        else
        {
            Catalog = new FontCatalog
            {
                FontFamilies = []
            };
        }

        _kerning = new Dictionary<string, Kerning>();
    }

    /// <summary>
    /// This method is used to retrieve the typeface for the given font face.
    /// It is required that both the family and face come from our catalog.
    /// This guarantees that no unwanted fetching attempt is made.
    /// </summary>
    /// <param name="family">The family the face belongs to.</param>
    /// <param name="face">The desired face.</param>
    /// <returns>The typeface for the requested font face.</returns>
    public Typeface GetExistingTypeFace(FontFamily family, FontFace face)
    {
        FaceIdentifier id = new FaceIdentifier
        {
            FamilyName = family.Name,
            Weight = face.Weight,
            Italic = face.Italic,
        };

        return GetTypeFace(id, false);
    }

    /// <summary>
    /// This method is used to retrieve the typeface for the given font face.
    /// It is required that both the family and face come from our catalog.
    /// This guarantees that no unwanted fetching attempt is made.
    /// </summary>
    /// <param name="id">The ID of the font face.</param>
    /// <returns>The typeface for the requested font face.</returns>
    public Typeface GetExistingTypeFace(FaceIdentifier id)
    {
        return GetTypeFace(id, false);
    }

    /// <summary>
    /// This method determines whether we know of the font face known by the information
    /// given.
    /// </summary>
    /// <param name="id">The ID of the font face.</param>
    /// <returns><c>true</c>, if we have a font face that matches, or <c>false</c>. if not.</returns>
    public bool IsKnownTypeFace(FaceIdentifier id)
    {
        return GetFontFace(id, false) != null;
    }

    /// <summary>
    /// This returns a <c>Typeface</c> object for the given font family, face weight and
    /// normal or italic style.  An appropriate exception will be thrown if the requested
    /// font face cannot be made available.
    /// </summary>
    /// <param name="id">The ID of the font face.</param>
    /// <param name="fetchFromGoogle">A flag noting whether the font should be fetched from
    /// Google if we don't know about it.</param>
    /// <returns>The typeface for the requested font face, or <c>null</c>, if we don't know
    /// the font and were not asked to fetch it.</returns>
    public Typeface GetTypeFace(FaceIdentifier id, bool fetchFromGoogle = true)
    {
        FontFace face = GetFontFace(id, fetchFromGoogle);

        if (face == null)
            return null;

        Typeface typeface = LoadTypeFace(Path.Combine(FontsDirectory, face.FileName));
        
        _kerning[face.FileName] = face.GetKerning();

        typeface.Filename = face.FileName;

        return typeface;
    }

    /// <summary>
    /// This method is used to import a local true type font file into the font catalog.
    /// Once verified to exist as a valid font file, it is copied to the font cache and
    /// registered in the catalog.
    /// </summary>
    /// <param name="id">The ID of the font face.</param>
    /// <param name="path">The path to the true type font file to import.</param>
    public void ImportTypeFace(FaceIdentifier id, string path)
    {
        FontFace face = GetFontFace(id, false);

        if (face != null)
            throw new Exception("The specified font face is already registered.");
        
        if (!File.Exists(path))
            throw new Exception($"The file, '{path}', does not exist.");

        if (LoadTypeFace(path) == null)
            throw new Exception($"The file, '{path}', does not appear to be a true type font file.");

        FontFamily family = Catalog.FontFamilies?
            .FirstOrDefault(id.BelongsTo);
        string fileName = id.AsFileName(Path.GetExtension(path));

        File.Copy(path, Path.Combine(FontsDirectory, fileName));

        face = new FontFace
        {
            Weight = id.Weight,
            Italic = id.Italic,
            FileName = fileName,
            IsLocal = true
        };

        RegisterFontFace(family, face, id.FamilyName);
    }

    /// <summary>
    /// This method is used to load a file as a type face.  If it cannot be loaded, then
    /// <c>null</c> will be returned.
    /// </summary>
    /// <param name="fileName">The name of the file to load.</param>
    /// <returns>The representative type face or <c>null</c>.</returns>
    public static Typeface LoadTypeFace(string fileName)
    {
        try
        {
            using Stream input = File.OpenRead(fileName);
            OpenFontReader reader = new OpenFontReader();

            return reader.Read(input);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// This method is used to ensure that we have a true-type font file available for the
    /// given font family, face weight and normal or italic style.  An appropriate exception
    /// will be thrown if the requested font face cannot be made available.
    /// </summary>
    /// <param name="id">The ID of the font face.</param>
    /// <param name="fetchFromGoogle">A flag noting whether the font should be fetched from
    /// Google if we don't know about it.</param>
    /// <returns>The typeface for the requested font face, or <c>null</c>, if we don't know
    /// the font and were not asked to fetch it.</returns>
    public FontFace GetFontFace(FaceIdentifier id, bool fetchFromGoogle = true)
    {
        FontFamily family = Catalog.FontFamilies?
            .FirstOrDefault(id.BelongsTo);
        FontFace face = family?.Faces
            .FirstOrDefault(id.MatchesFace);

        // If we don't have the specific font face, we need to go fetch it.
        if (face == null && fetchFromGoogle)
        {
            FontFaceLoader loader = new FontFaceLoader(id);

            face = loader.CacheFontFile();

            RegisterFontFace(family, face, id.FamilyName);
        }

        return face;
    }

    /// <summary>
    /// This method is used to register a new font face.
    /// </summary>
    /// <param name="family">The owning font family, if it already exists.</param>
    /// <param name="face">The font face to register.</param>
    /// <param name="name">The font family name, in case we need to create the family.</param>
    private void RegisterFontFace(FontFamily family, FontFace face, string name)
    {
        if (family == null)
        {
            family = new FontFamily
            {
                Name = name,
                Faces = [face]
            };

            Catalog.FontFamilies!.Add(family);
        }
        else
            family.Faces.Add(face);

        RewriteCatalog();
    }

    /// <summary>
    /// This method is used to add a new kerning pair for the indicated font.
    /// If the pair already exists, it will be replaced.
    /// </summary>
    /// <param name="id">The ID of the font face.</param>
    /// <param name="pair">The kerning pair to add.</param>
    public void AddKerningPair(FaceIdentifier id, KerningPair pair)
    {
        Kerning kerning = GetFontFace(id, false)?.GetKerning();

        if (kerning == null)
            throw new ArgumentException("The specified font face is not registered.");

        if (pair == null)
            throw new ArgumentException("A kerning pair must be provided.");

        kerning.AddKerning(pair.Left, pair.Right, pair.Kern);
        kerning.UpdateFontFace();

        RewriteCatalog();
    }

    /// <summary>
    /// This method is used to remove a kerning pair from the indicated font.
    /// </summary>
    /// <param name="id">The ID of the font face.</param>
    /// <param name="pair">The kerning pair to remove.</param>
    public void RemoveKerningPair(FaceIdentifier id, KerningPair pair)
    {
        Kerning kerning = GetFontFace(id, false).GetKerning();

        if (kerning == null)
            throw new ArgumentException("The specified font face is not registered.");

        if (kerning.RemoveKern(pair.Left, pair.Right))
        {
            kerning.UpdateFontFace();

            RewriteCatalog();
        }
    }

    /// <summary>
    /// This method is used to remove a font face from our catalog.
    /// If the face is the last face in its family, the family is also removed.
    /// </summary>
    /// <param name="id">The ID of the font face.</param>
    public void RemoveFontFace(FaceIdentifier id)
    {
        FontFamily family = Catalog.FontFamilies?
            .FirstOrDefault(id.BelongsTo);
        FontFace face = family?.Faces
            .FirstOrDefault(id.MatchesFace);

        if (face != null)
        {
            string path = Path.Combine(FontsDirectory, face.FileName);

            if (File.Exists(path))
                File.Delete(path);

            family.Faces.Remove(face);

            if (family.Faces.Count == 0)
                Catalog.FontFamilies.Remove(family);

            RewriteCatalog();
        }
    }

    /// <summary>
    /// This method is used to determine what the kerning adjustment, if any, there should
    /// be between the two specified code points within the typeface.
    /// </summary>
    /// <param name="typeface">The typeface that gives us our kerning context.</param>
    /// <param name="kerningOverrides">A collection of kerning pairs that may override everything.</param>
    /// <param name="left">The code point of the left glyph the kerning is for.</param>
    /// <param name="right">The code point of the right glyph the kerning is for.</param>
    /// <returns>The amount to adjust the space between the two code points.</returns>
    public short GetKerningFor(Typeface typeface, Kerning kerningOverrides, int left, int right)
    {
        short result = 0;
        
        if (kerningOverrides != null)
            result = kerningOverrides.GetKern(left, right);

        if (result == 0 && _kerning.TryGetValue(typeface.Filename, out Kerning kerning))
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
    /// This method is used to persist the current content of the font catalog.
    /// </summary>
    private void RewriteCatalog()
    {
        string json = JsonConvert.SerializeObject(Catalog);

        File.WriteAllText(CatalogPath, json);
    }
}
