using RayTracer.Fonts;
using RayTracer.General;

namespace RayTracer.Instructions.Surfaces.Extrusions;

/// <summary>
/// This class is used to resolve a text layout settings value.
/// </summary>
public class TextLayoutSettingsResolver : ObjectResolver<TextLayoutSettings>
{
    /// <summary>
    /// This property holds the resolver for the text alignment property of the layout
    /// settings.
    /// </summary>
    public Resolver<TextAlignment> TextAlignmentResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the horizontal position property of the layout
    /// settings.
    /// </summary>
    public Resolver<HorizontalPosition> HorizontalPositionResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the vertical position property of the layout
    /// settings.
    /// </summary>
    public Resolver<VerticalPosition> VerticalPositionResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the line gap property of the layout settings.
    /// </summary>
    public Resolver<double> LineGapResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a text
    /// layout settings.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, TextLayoutSettings value)
    {
        TextAlignmentResolver.AssignTo(value, target => target.TextAlignment, context, variables);
        HorizontalPositionResolver.AssignTo(value, target => target.HorizontalPosition, context, variables);
        VerticalPositionResolver.AssignTo(value, target => target.VerticalPosition, context, variables);
        LineGapResolver.AssignTo(value, target => target.LineGap, context, variables);
    }
}
