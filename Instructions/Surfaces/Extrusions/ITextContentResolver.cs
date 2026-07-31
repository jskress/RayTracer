using RayTracer.Fonts;

namespace RayTracer.Instructions.Surfaces.Extrusions;

/// <summary>
/// This interface is implemented by the resolvers that carry the shared content of a run of
/// text -- its string, its font, and its layout -- so the pieces of the parser that fill that
/// content in can serve both the text surface and the text path source without caring which
/// one they are filling.
/// </summary>
public interface ITextContentResolver
{
    /// <summary>
    /// This property holds the resolver for the text to lay out.
    /// </summary>
    Resolver<string> TextResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the font family name.
    /// </summary>
    Resolver<string> FontFamilyNameResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the font weight.
    /// </summary>
    Resolver<FontWeight> FontWeightResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for whether the italic face is wanted.
    /// </summary>
    Resolver<bool> IsItalicResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the text layout settings.
    /// </summary>
    TextLayoutSettingsResolver LayoutSettingsResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the kerning overrides.
    /// </summary>
    KerningResolver KerningResolver { get; set; }
}
