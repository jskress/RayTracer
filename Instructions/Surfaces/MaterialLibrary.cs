using System.Globalization;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Patterns;
using RayTracer.Pigments;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class reads a Wavefront material library -- an `.mtl` file, which a model's `mtllib` names -- into this
/// engine's materials, one for each `newmtl`.
/// <para>
/// **What a library says is read as nearly as this engine can say it:**
/// </para>
/// <list type="bullet">
/// <item>`Kd` is the color, or `map_Kd` a picture laid by the model's texture coordinates, its alpha a cutout --
/// the leaves of a model of a plant.  `Ks` and `Ns` are the highlight's strength (its brightest channel) and
/// tightness.  `Ke` is light given off.</item>
/// <item>`d` (or `Tr`, one less it) is how opaque, and `Ni` the index of refraction of what is seen through.  `illum`
/// says how the surface is lit: 0 its color alone, 1 no highlight, 3 and 5 mirroring by `Ks` (5 by Fresnel), 4, 6, 7
/// and 9 glass.</item>
/// <item>`bump` or `map_Bump` is a picture of heights (`-bm` how deep), or of normals, as Blender writes them there:
/// a picture whose blue is mostly high and whose red and green sit about the middle is taken for one.  `norm` is
/// always a normal map.</item>
/// <item>The physically based extensions: `Pr` and `Pm` roughness and metalness, `map_Pr` and `map_Pm` pictures of
/// them (`-imfchan` which channel), `Pc` and `Pcr` a clear coat and how rough, `Ps` a sheen, `aniso` brushing.</item>
/// <item>A map's `-o` and `-s` move and stretch where in the picture the model's coordinates fall.</item>
/// </list>
/// <para>
/// `Ka` is not read: a lamp lends this engine's ambient, which is not a color the surface has.  `map_d` of a picture
/// other than `map_Kd`'s is not read, the color picture's own alpha being the cutout; nor are `map_Ks`, `map_Ns` and
/// `-clamp`.  What is not read is said once, as a note.
/// </para>
/// </summary>
public class MaterialLibrary
{
    /// <summary>
    /// This property holds the materials read, by name.
    /// </summary>
    public Dictionary<string, Material> Materials { get; } = [];

    /// <summary>
    /// This property holds what the library said that was not read, for a note.
    /// </summary>
    public List<string> Unread { get; } = [];

    private readonly string _directory;

    // What one `newmtl` has said so far.
    private sealed class Entry
    {
        internal string Name;
        internal Color Diffuse = new (0.8, 0.8, 0.8), Specular, Emission, Filter;
        internal double? Shininess, Dissolve, Transparency, Index, Roughness, Metallic, Coat, CoatRoughness, Sheen;
        internal double? Anisotropy;
        internal int Illumination = 2;
        internal Map DiffuseMap, BumpMap, NormalMap, RoughnessMap, MetallicMap;
    }

    // A picture a library names, with its options.
    private sealed class Map
    {
        internal string File;
        internal double Depth = 1;
        internal ImageChannel Channel = ImageChannel.Gray;
        internal double ScaleU = 1, ScaleV = 1, OffsetU, OffsetV;
    }

    /// <summary>
    /// This constructor reads a library from its text.
    /// </summary>
    /// <param name="lines">The library's lines.</param>
    /// <param name="directory">The folder the library's pictures are found from.</param>
    public MaterialLibrary(IEnumerable<string> lines, string directory)
    {
        _directory = directory;

        Entry entry = null;

        foreach (string line in ObjectFileParser.Joined(lines))
        {
            string[] words = ObjectFileParser.WordsOf(line);

            if (words.Length == 0)
                continue;

            if (words[0] == "newmtl")
            {
                if (entry is not null)
                    Materials[entry.Name] = MaterialOf(entry);

                entry = new Entry { Name = string.Join(' ', words[1..]) };

                continue;
            }

            if (entry is null)
                continue;

            Read(entry, words);
        }

        if (entry is not null)
            Materials[entry.Name] = MaterialOf(entry);
    }

    /// <summary>
    /// This method reads one statement of a material.
    /// </summary>
    private void Read(Entry entry, string[] words)
    {
        switch (words[0])
        {
            case "Kd": entry.Diffuse = ColorOf(words); break;
            case "Ks": entry.Specular = ColorOf(words); break;
            case "Ke": entry.Emission = ColorOf(words); break;
            case "Tf": entry.Filter = ColorOf(words); break;
            case "Ns": entry.Shininess = Number(words[1]); break;
            case "d": entry.Dissolve = Number(words[^1]); break;
            case "Tr": entry.Transparency = Number(words[1]); break;
            case "Ni": entry.Index = Number(words[1]); break;
            case "illum": entry.Illumination = (int) Number(words[1]); break;
            case "Pr": entry.Roughness = Number(words[1]); break;
            case "Pm": entry.Metallic = Number(words[1]); break;
            case "Pc": entry.Coat = Number(words[1]); break;
            case "Pcr": entry.CoatRoughness = Number(words[1]); break;
            case "Ps": entry.Sheen = Number(words[1]); break;
            case "aniso": entry.Anisotropy = Number(words[1]); break;
            case "map_Kd": entry.DiffuseMap = MapOf(words); break;
            case "bump" or "map_Bump" or "map_bump": entry.BumpMap = MapOf(words); break;
            case "norm" or "map_norm": entry.NormalMap = MapOf(words); break;
            case "map_Pr": entry.RoughnessMap = MapOf(words); break;
            case "map_Pm": entry.MetallicMap = MapOf(words); break;
            case "Ka" or "map_Ka" or "anisor" or "sharpness": break;
            case "map_d":
                // The color picture's own alpha is the cutout; a picture of its own is not read.
                if (entry.DiffuseMap is null || MapOf(words).File != entry.DiffuseMap.File)
                    NoteUnread($"map_d (in {entry.Name}): give map_Kd's picture an alpha channel instead");
                break;
            default:
                NoteUnread(words[0]);
                break;
        }
    }

    /// <summary>
    /// This method turns what a material said into one of this engine's.
    /// </summary>
    private Material MaterialOf(Entry entry)
    {
        double opacity = entry.Dissolve ?? (entry.Transparency is { } transparency ? 1 - transparency : 1);
        bool glass = entry.Illumination is 4 or 6 or 7 or 9;
        double strongest = entry.Specular is null
            ? 0
            : Math.Max(entry.Specular.Red, Math.Max(entry.Specular.Green, entry.Specular.Blue));
        Material material = new ()
        {
            Pigment = entry.DiffuseMap is { } picture ? PictureOf(picture) : new SolidPigment(entry.Diffuse),
            Specular = strongest,
            Shininess = Math.Max(1, entry.Shininess ?? 10),
            Transparency = Math.Clamp(1 - opacity, 0, 1)
        };

        switch (entry.Illumination)
        {
            case 0:
                // The color alone, unlit.
                material.Ambient = 1;
                material.Diffuse = 0;
                material.Specular = 0;
                break;
            case 1:
                material.Specular = 0;
                break;
            case 3 or 5:
                material.Reflective = strongest;
                material.Fresnel = entry.Illumination == 5;
                break;
        }

        if (glass)
        {
            material.Fresnel = true;
            material.Reflective = entry.Illumination == 9 ? 0 : Math.Max(strongest, 0.04);

            if (material.Transparency == 0)
                material.Transparency = 1;
        }

        if (material.Transparency > 0 || glass)
        {
            material.Interior = new Interior { IndexOfRefraction = entry.Index ?? 1 };

            if (entry.Filter is { } filter)
                material.Interior.Filter = Math.Max(filter.Red, Math.Max(filter.Green, filter.Blue));
        }

        if (entry.Emission is { } glow && (glow.Red > 0 || glow.Green > 0 || glow.Blue > 0))
            material.Emission = glow;

        if (entry.Roughness is { } roughness)
            material.Roughness = Math.Clamp(roughness, 0, 1);

        if (entry.Metallic is { } metallic)
            material.Metallic = Math.Clamp(metallic, 0, 1);

        if (entry.RoughnessMap is { } roughnessMap)
            material.RoughnessBy = new MaterialMap { From = 0, To = 1, Pattern = PatternOf(roughnessMap) };

        if (entry.MetallicMap is { } metallicMap)
            material.MetallicBy = new MaterialMap { From = 0, To = 1, Pattern = PatternOf(metallicMap) };

        if (entry.Coat is > 0)
            material.Coat = new Coat { Roughness = Math.Clamp(entry.CoatRoughness ?? 0, 0, 1) };

        if (entry.Sheen is > 0 && material.Coat is null)
            material.Sheen = new Sheen { Color = new Color(entry.Sheen.Value, entry.Sheen.Value, entry.Sheen.Value) };

        if (entry.Anisotropy is { } anisotropy && material.Roughness > 0)
            material.Anisotropy = Math.Clamp(anisotropy, 0, 1);

        if (entry.NormalMap is { } normalMap)
            material.SurfaceNormal = new SurfaceNormal { NormalMap = PatternOf(normalMap), Depth = normalMap.Depth };
        else if (entry.BumpMap is { } bumpMap)
        {
            ImagePattern pattern = PatternOf(bumpMap);

            material.SurfaceNormal = LooksLikeNormals(pattern)
                ? new SurfaceNormal { NormalMap = pattern, Depth = bumpMap.Depth }
                : new SurfaceNormal { Pattern = pattern, Depth = bumpMap.Depth };
        }

        return material;
    }

    /// <summary>
    /// This method decides whether a picture named as a bump map holds normals rather than heights: a picture of
    /// normals is mostly blue-violet, its blue high and its red and green about the middle.
    /// </summary>
    private static bool LooksLikeNormals(ImagePattern pattern)
    {
        Canvas canvas = pattern.ImageReference.Canvas;
        int step = Math.Max(1, Math.Max(canvas.Width, canvas.Height) / 64);
        double red = 0, green = 0, blue = 0;
        int count = 0;

        for (int y = 0; y < canvas.Height; y += step)
        {
            for (int x = 0; x < canvas.Width; x += step)
            {
                Color color = canvas.GetPixel(x, y);

                red += color.Red;
                green += color.Green;
                blue += color.Blue;
                count++;
            }
        }

        (red, green, blue) = (red / count, green / count, blue / count);

        return blue > 0.7 && Math.Abs(red - 0.5) < 0.15 && Math.Abs(green - 0.5) < 0.15;
    }

    private ImagePigment PictureOf(Map map) => new ()
    {
        ImageReference = ReferenceTo(map),
        Coordinates = CoordinatesOf(map)
    };

    private ImagePattern PatternOf(Map map) => new ()
    {
        ImageReference = ReferenceTo(map),
        Channel = map.Channel,
        Coordinates = CoordinatesOf(map)
    };

    private ImageReference ReferenceTo(Map map) => new () { ImageName = map.File, SourceDirectory = _directory };

    private static TextureCoordinates CoordinatesOf(Map map) =>
        map.ScaleU == 1 && map.ScaleV == 1 && map.OffsetU == 0 && map.OffsetV == 0
            ? null
            : new TextureCoordinates(map.ScaleU, map.ScaleV, map.OffsetU, map.OffsetV);

    /// <summary>
    /// This method reads a map statement: its options, then the picture's file, which may have spaces in it.
    /// </summary>
    private Map MapOf(string[] words)
    {
        Map map = new ();
        int at = 1;

        while (at < words.Length - 1 && words[at].StartsWith('-'))
        {
            string option = words[at++];

            // How many numbers follow each option: -o and -s take up to three, the last two optional.
            switch (option)
            {
                case "-bm":
                    map.Depth = Number(words[at++]);
                    break;
                case "-imfchan":
                    map.Channel = words[at++] switch
                    {
                        "r" => ImageChannel.Red, "g" => ImageChannel.Green, "b" => ImageChannel.Blue,
                        "m" or "l" or "z" => ImageChannel.Gray, _ => ImageChannel.Gray
                    };
                    break;
                case "-o" or "-s" or "-t":
                    List<double> numbers = [];

                    while (numbers.Count < 3 && at < words.Length - 1 && IsNumber(words[at]))
                        numbers.Add(Number(words[at++]));

                    if (option == "-o")
                        (map.OffsetU, map.OffsetV) = (numbers.ElementAtOrDefault(0), numbers.ElementAtOrDefault(1));
                    else if (option == "-s")
                    {
                        map.ScaleU = numbers.Count > 0 ? numbers[0] : 1;
                        map.ScaleV = numbers.Count > 1 ? numbers[1] : map.ScaleU;
                    }
                    else
                        NoteUnread("-t");
                    break;
                default:
                    // -clamp, -blendu, -blendv, -mm, -texres, -cc: each takes one word after it.
                    NoteUnread(option);
                    at++;
                    break;
            }
        }

        map.File = string.Join(' ', words[at..]);

        return map;
    }

    private void NoteUnread(string what)
    {
        if (!Unread.Contains(what))
            Unread.Add(what);
    }

    private static Color ColorOf(string[] words)
    {
        double red = Number(words[1]);

        return words.Length >= 4 ? new Color(red, Number(words[2]), Number(words[3])) : new Color(red, red, red);
    }

    private static bool IsNumber(string word) =>
        double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    private static double Number(string word) => double.Parse(word, NumberStyles.Float, CultureInfo.InvariantCulture);
}
