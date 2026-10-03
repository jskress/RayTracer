using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Instructions.Transforms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a decal, one of the markings a material paints over its pigment.
/// </summary>
public class DecalResolver : ObjectResolver<Decal>, IValidatable, ICloneable
{
    /// <summary>
    /// This property holds the resolver for the outline that says where the decal paints.
    /// </summary>
    public Resolver<GeneralPath> PathResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the color painted inside the outline.
    /// </summary>
    public Resolver<Color> ColorResolver { get; set; }

    /// <summary>
    /// This property holds how the outline is carried onto the surface.
    /// </summary>
    public DecalProjection Projection { get; set; } = DecalProjection.Planar;

    /// <summary>
    /// This property holds the resolver for the radius of a toroidal decal's ring.
    /// </summary>
    public Resolver<double> RingRadiusResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the near end of the decal's reach.
    /// </summary>
    public Resolver<double> MinimumResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the far end of the decal's reach.
    /// </summary>
    public Resolver<double> MaximumResolver { get; set; }

    /// <summary>
    /// This property holds the word each end of the reach was said with, <c>Y</c> or
    /// <c>radius</c>, so that a reach said in the wrong one for the projection can be caught.
    /// </summary>
    public string MinimumWord { get; set; }

    /// <summary>
    /// The same, for the far end.
    /// </summary>
    public string MaximumWord { get; set; }

    /// <summary>
    /// This property holds the resolver for the angle up to which the decal is at full strength.
    /// </summary>
    public Resolver<double> FadeFromResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the angle by which the decal is gone.
    /// </summary>
    public Resolver<double> FadeToResolver { get; set; }

    /// <summary>
    /// This property notes whether the scene said the decal is not to fade.
    /// </summary>
    public bool NoFade { get; set; }

    /// <summary>
    /// This property holds the resolver for where the decal is placed.
    /// </summary>
    public TransformResolver TransformResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a decal.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Decal value)
    {
        double near = MinimumResolver.Resolve(context, variables);
        double far = MaximumResolver.Resolve(context, variables);

        value.Outline = new FlattenedPath(PathResolver.Resolve(context, variables));
        value.Color = ColorResolver.Resolve(context, variables);
        value.Projection = Projection;
        value.MinimumReach = Math.Min(near, far);
        value.MaximumReach = Math.Max(near, far);

        if (RingRadiusResolver is not null)
            value.RingRadius = RingRadiusResolver.Resolve(context, variables);

        value.Fades = !NoFade;

        // Put in order, as the reach is, since a fade between two angles has only the one meaning.
        if (FadeFromResolver is not null)
        {
            double from = FadeFromResolver.Resolve(context, variables);
            double to = FadeToResolver.Resolve(context, variables);

            value.FadeFrom = Math.Min(from, to);
            value.FadeTo = Math.Max(from, to);
        }
        value.Transform = TransformResolver?.Resolve(context, variables) ?? Matrix.Identity;
    }

    /// <summary>
    /// This method makes sure the decal says everything a decal must.
    /// <para>
    /// The reach is the one that is easy to leave off and costly to: a planar decal with no limit to
    /// how far it reaches prints straight through whatever it is on and comes out on the far side,
    /// backwards.  So it must be said rather than defaulted.
    /// </para>
    /// </summary>
    /// <returns>The text of an error message, or <c>null</c>, if all is well.</returns>
    public string Validate()
    {
        if (PathResolver is null)
            return "A decal needs an outline to paint inside; give it a path.";

        if (ColorResolver is null)
            return "A decal needs a color.";

        if (NoFade && FadeFromResolver is not null)
            return "A decal cannot both fade and not fade; say \"no fade\" or how it fades, not both.";

        // A planar decal reaches along its projection, and the others reach out from what they are
        // wrapped about, so each has its own word for it.  Saying one where the other belongs is
        // almost certainly a projection left off or mistyped, and a reach read the wrong way would
        // paint somewhere else entirely without a word.
        string word = Projection == DecalProjection.Planar ? "Y" : "radius";

        if (MinimumResolver is null || MaximumResolver is null)
        {
            return Projection == DecalProjection.Planar
                ? "A planar decal needs \"min Y\" and \"max Y\" to say how far it reaches, or it " +
                  "prints straight through whatever it is on."
                : $"A {Projection.ToString().ToLowerInvariant()} decal needs \"min radius\" and " +
                  "\"max radius\" to say how far out it reaches, or it paints everything in its " +
                  "path, however near or far.";
        }

        if (MinimumWord != word || MaximumWord != word)
        {
            return Projection == DecalProjection.Planar
                ? "A planar decal reaches along its Y axis, so its reach is \"min Y\" and \"max Y\"; " +
                  "\"radius\" is for a decal wrapped about something."
                : $"A {Projection.ToString().ToLowerInvariant()} decal reaches out from what it is " +
                  "wrapped about, so its reach is \"min radius\" and \"max radius\".";
        }

        return null;
    }

    /// <summary>
    /// This method creates a copy of this resolver.  The transforms are copied rather than shared,
    /// since a run of transform clauses is added to the resolver already there rather than replacing
    /// it.
    /// </summary>
    /// <returns>A clone of this resolver.</returns>
    public object Clone()
    {
        DecalResolver resolver = (DecalResolver) MemberwiseClone();

        resolver.TransformResolver = (TransformResolver) TransformResolver?.Clone();

        return resolver;
    }
}
