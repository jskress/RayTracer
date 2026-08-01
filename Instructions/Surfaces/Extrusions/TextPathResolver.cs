using RayTracer.Fonts;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.Instructions.Surfaces.Extrusions;

/// <summary>
/// This class resolves a run of text into a 2D path: the outlines of its glyphs, laid out and
/// folded into one general path, so text can be extruded, spun on the lathe or swept along a
/// spline like any other path.  It carries the same content as the text surface -- string,
/// font and layout -- but none of a surface's own properties, since the shape it feeds owns
/// those.
/// </summary>
public class TextPathResolver : ObjectResolver<GeneralPath>, ITextContentResolver, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the text to lay out.
    /// </summary>
    public Resolver<string> TextResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the font family name.
    /// </summary>
    public Resolver<string> FontFamilyNameResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the font weight.
    /// </summary>
    public Resolver<FontWeight> FontWeightResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for whether the italic face is wanted.
    /// </summary>
    public Resolver<bool> IsItalicResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the text layout settings.
    /// </summary>
    public TextLayoutSettingsResolver LayoutSettingsResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the kerning overrides.
    /// </summary>
    public KerningResolver KerningResolver { get; set; }

    /// <summary>
    /// This method lays the text out and folds every glyph's outline into the path.  Each
    /// glyph is reversed on the way in, exactly as the text surface reverses it before
    /// extruding, so that an extruded (or lathed, or swept) text path has its walls facing the
    /// same way the text surface's do.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The path to fold the glyphs into.</param>
    protected override void SetProperties(RenderContext context, Variables variables, GeneralPath value)
    {
        string text = TextResolver.Resolve(context, variables);
        string family = FontFamilyNameResolver.Resolve(context, variables);
        FontWeight weight = FontWeightResolver?.Resolve(context, variables) ?? FontWeight.Regular;
        bool italic = IsItalicResolver?.Resolve(context, variables) ?? false;
        TextLayoutSettings settings =
            LayoutSettingsResolver?.Resolve(context, variables) ?? new TextLayoutSettings();
        Kerning kerning = KerningResolver?.Resolve(context, variables);

        foreach (GeneralPath glyph in TextOutline.Glyphs(family, weight, italic, settings, kerning, text))
            value.Append(glyph.Reverse());
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        if (TextResolver is null)
            return "The \"text\" property is required.";

        return FontFamilyNameResolver is null ? "The \"font\" property is required." : null;
    }
}
