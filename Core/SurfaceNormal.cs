using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Patterns;

namespace RayTracer.Core;

/// <summary>
/// This class roughens a surface by tilting its normal from point to point, so that a wall reads
/// as stucco and an orange reads as an orange without either of them gaining a single triangle.
/// <para>
/// The trick is an old one and worth stating plainly, since what it does not do is the surprising
/// part.  Nothing about the shape changes: the surface is as smooth as it ever was, and a sphere
/// so treated still has a perfectly round silhouette and casts a perfectly round shadow.  What
/// changes is only which way the surface claims to be facing when it is asked how to take the
/// light, and since that is nearly all of what makes a surface look like anything, the eye is
/// content.  It gives out at the edges, where the flat truth shows.
/// </para>
/// </summary>
public class SurfaceNormal
{
    /// <summary>
    /// This is how far apart the samples are taken when working out which way the pattern is
    /// climbing.  It wants to be small enough that the samples straddle a real slope rather than
    /// a whole feature, and large enough that the difference between them is not lost in the noise
    /// of double arithmetic; a thousandth of a unit sits comfortably between the two for the
    /// patterns here, whose features run from a hundredth of a unit upward.
    /// </summary>
    private const double SampleGap = 0.001;

    /// <summary>
    /// This is what a depth of one comes to once it reaches the normal.
    /// <para>
    /// A pattern's slope is a number with no natural size to it -- how fast granite climbs depends
    /// on nothing but how granite happens to be written -- so the depth needs some scale to be
    /// read against, and the useful one is POV-Ray's, whose normals every scene and every texture
    /// library is already written in terms of. This is that scale, measured rather than derived:
    /// spheres roughened by the same pattern at a range of amounts in both, with the graininess of
    /// each counted, put the two a factor of eight apart.  It is not a perfect match at every
    /// depth -- ours tilts a little less far than POV-Ray's once the depth passes a half, by about
    /// a tenth -- but it is within a few hundredths over the range the texture libraries use.  So a
    /// <c>depth</c> here means what an amount means there, and the converted libraries can hand
    /// their numbers straight over.
    /// </para>
    /// </summary>
    private const double DepthScale = 0.125;

    /// <summary>
    /// This property holds the pattern whose slope the surface is tilted by.
    /// </summary>
    public Pattern Pattern { get; set; }

    /// <summary>
    /// This property holds how far the normal may be tilted, which is what decides how deep the
    /// bumps look.  Zero leaves the surface as it was, and larger numbers roughen it further;
    /// much beyond one the lie stops being believable and the surface reads as noise.
    /// </summary>
    public double Depth { get; set; } = 1;

    /// <summary>
    /// This property holds the transform applied to a point before the pattern is asked about it,
    /// which is how a scene scales and turns the roughening without disturbing the shape or the
    /// colouring.  It is the pigment's arrangement exactly, and deliberately so: the two are
    /// separate patterns over the same surface and each wants its own footing.
    /// </summary>
    public Matrix Transform
    {
        get => field;
        set
        {
            field = value;

            _inverseTransform = new Lazy<Matrix>(() => Transform.Invert());
            _toPattern = new Lazy<Matrix>(() => Transform.Transpose());
            _toSurface = new Lazy<Matrix>(() => Transform.Invert().Transpose());
        }
    } = Matrix.Identity;

    private Lazy<Matrix> _inverseTransform = new (() => Matrix.Identity);
    private Lazy<Matrix> _toPattern = new (() => Matrix.Identity);
    private Lazy<Matrix> _toSurface = new (() => Matrix.Identity);

    /// <summary>
    /// This method tilts the given normal by however the pattern is sloping where it is standing.
    /// <para>
    /// The pattern is a landscape of numbers over the surface, and the direction it climbs fastest
    /// at a point is its gradient, found here by sampling a short way along each axis and taking
    /// the differences.  Tilting the normal toward the downhill direction is what makes a bump: a
    /// point on the near side of a rise faces a little more toward us and catches more light, and
    /// a point on the far side faces away and catches less.
    /// </para>
    /// <para>
    /// Only the part of the gradient lying along the surface is used.  The part pointing along the
    /// normal says the pattern is changing as one moves *through* the surface, which says nothing
    /// about which way the skin is tilted and would only push the normal in or out.
    /// </para>
    /// </summary>
    /// <param name="normal">The surface's own normal, in surface space.</param>
    /// <param name="point">The point being shaded, in surface space.</param>
    /// <returns>The tilted normal.</returns>
    public Vector PerturbAt(Vector normal, Point point)
    {
        if (Pattern is null || Depth == 0)
            return normal;

        Point patternPoint = _inverseTransform.Value * point;
        Vector gradient = GradientAt(patternPoint);

        if (gradient.Magnitude.Near(0))
            return normal;

        // The whole tilt is done in the pattern's own frame, and this is the part that took some
        // finding.  The normal is carried in, made a unit vector *there*, tilted, and carried back
        // out.  Both journeys are a normal's rather than a point's, so both go by the transposed
        // inverse of the map they follow.
        //
        // Making it a unit vector once it is inside is what settles how a scaled pattern behaves,
        // and it settles it two different ways at once, both of them right.  Scale the pattern
        // evenly and the journey in and the journey out cancel, so the bumps grow finer without
        // growing deeper: scale says how fine and depth says how deep, each leaving the other
        // alone.  Squash it along one axis and they no longer cancel, so the slopes across the
        // squashed axis really do steepen -- which is what draws the brush marks on POV-Ray's
        // brushed aluminium, whose pattern is squashed a thousandfold along two axes to make them.
        //
        // Both were measured against POV-Ray rather than assumed: its roughening holds steady from
        // a scale of 1 down to 0.075 when the squashing is even, and grows some thirteenfold over
        // the same range when it is not.
        Vector patternNormal = (_toPattern.Value * normal).Unit;

        // Take out the part that points along the normal, leaving the slope across the surface.
        // The part pointing along it says the pattern is changing as one moves *through* the skin,
        // which says nothing about which way the skin is tilted.
        Vector across = gradient - patternNormal * gradient.Dot(patternNormal);
        Vector tilted = patternNormal - across * (Depth * DepthScale);

        return (_toSurface.Value * tilted).Unit;
    }

    /// <summary>
    /// This method works out which way, and how steeply, the pattern is climbing at a point, by
    /// asking it either side of that point along each axis.  Sampling both sides rather than one
    /// costs three more questions and answers a good deal more accurately, which matters here:
    /// the whole effect is the difference between two nearly equal numbers.
    /// </summary>
    /// <param name="point">The point to find the slope at, in pattern space.</param>
    /// <returns>The pattern's gradient there.</returns>
    private Vector GradientAt(Point point)
    {
        double scale = 1 / (2 * SampleGap);

        return new Vector(
            (ValueAt(point, SampleGap, 0, 0) - ValueAt(point, -SampleGap, 0, 0)) * scale,
            (ValueAt(point, 0, SampleGap, 0) - ValueAt(point, 0, -SampleGap, 0)) * scale,
            (ValueAt(point, 0, 0, SampleGap) - ValueAt(point, 0, 0, -SampleGap)) * scale);
    }

    /// <summary>
    /// This method asks the pattern about a point a short way off from the one given.
    /// </summary>
    /// <param name="point">The point to move from.</param>
    /// <param name="x">How far to move along X.</param>
    /// <param name="y">How far to move along Y.</param>
    /// <param name="z">How far to move along Z.</param>
    /// <returns>The pattern's value there.</returns>
    private double ValueAt(Point point, double x, double y, double z) =>
        Pattern.ValueFor(new Point(point.X + x, point.Y + y, point.Z + z));

    /// <summary>
    /// This method sets the seed for the pattern to use, so that a roughening repeats from render
    /// to render.
    /// </summary>
    /// <param name="seed">The seed to use.</param>
    public void SetSeed(int seed) => Pattern?.SetSeed(seed);

    /// <summary>
    /// This method returns whether the given surface normal matches this one.
    /// </summary>
    /// <param name="other">The surface normal to compare to.</param>
    /// <returns><c>true</c>, if the two match, or <c>false</c>, if not.</returns>
    public bool Matches(SurfaceNormal other)
    {
        if (other is null || !Depth.Near(other.Depth) || !Transform.Matches(other.Transform))
            return false;

        return Pattern is null ? other.Pattern is null : Pattern.Matches(other.Pattern);
    }
}
