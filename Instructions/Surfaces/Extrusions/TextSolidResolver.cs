using RayTracer.Fonts;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces.Extrusions;

/// <summary>
/// This class is used to resolve a text solid value.
/// </summary>
public class TextSolidResolver : SurfaceResolver<TextSolid>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the text property on a text solid.
    /// </summary>
    public Resolver<string> TextResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the font family name property on a text solid.
    /// </summary>
    public Resolver<string> FontFamilyNameResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the font weight property on a text solid.
    /// </summary>
    public Resolver<FontWeight> FontWeightResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the "is italic" property on a text solid.
    /// </summary>
    public Resolver<bool> IsItalicResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the text layout settings property on a text
    /// solid.
    /// </summary>
    public TextLayoutSettingsResolver LayoutSettingsResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the closed property on a text solid.
    /// </summary>
    public Resolver<bool> ClosedResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a
    /// text solid surface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, TextSolid value)
    {
        TextResolver.AssignTo(value, target => target.Text, context, variables);
        FontFamilyNameResolver.AssignTo(value, target => target.FontFamilyName, context, variables);
        FontWeightResolver.AssignTo(value, target => target.FontWeight, context, variables);
        IsItalicResolver.AssignTo(value, target => target.IsItalic, context, variables);
        LayoutSettingsResolver.AssignTo(value, target => target.LayoutSettings, context, variables);
        ClosedResolver.AssignTo(value, target => target.Closed, context, variables);

        base.SetProperties(context, variables, value);
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
