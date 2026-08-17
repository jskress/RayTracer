using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This class gives the color of a real sky, worked out from what the air actually does to sunlight
/// rather than chosen to look about right.
/// <para>
/// A scene says where the sun stands and how hazy the air is, and everything else follows: the blue
/// overhead and the pale band at the horizon, the ring of glare around the sun, the reddening as it
/// sets.  None of those is a setting.  They are what falls out of air scattering short wavelengths
/// some six times more readily than long ones, of haze throwing light forward, and of both thinning
/// with height at quite different rates.
/// </para>
/// <para>
/// Being a pigment is what makes this cheap to fit in, and it was the reason for choosing it.  The
/// background is a pigment asked about a direction, so a camera looking at the sky, a mirror
/// reflecting it, and a sky light gathering from it all arrive at the same place by paths that
/// already existed.  One thing to write, three things that then work.
/// </para>
/// <para>
/// <b>The sun's own disc is not in here.</b>  It subtends about half a degree, so a sky light
/// sampling a sky that contained it would strike it perhaps once in fifty thousand samples, and that
/// one sample would be tens of thousands of times brighter than its neighbours -- speckle that gets
/// worse rather than better as more samples are taken, and light counted twice besides, since the sun
/// arrives as a light of its own.  A scene wanting a visible sun should place one: a sphere of the
/// right size in the right direction, or simply a disc.
/// </para>
/// </summary>
public class PhysicalSkyPigment : Pigment
{
    /// <summary>
    /// This property holds how high the sun stands above the horizon, in degrees.  Ninety is directly
    /// overhead; nothing is on the horizon; below nothing has it set, leaving the sky lit only by what
    /// little still reaches the air overhead.
    /// </summary>
    public double SunElevation { get; set; } = 45;

    /// <summary>
    /// This property holds which way round the sun lies, in degrees, measured from the negative Z
    /// axis and turning toward positive X -- so that nothing puts the sun straight ahead of a camera
    /// looking the way cameras here look by default.
    /// </summary>
    public double SunAzimuth { get; set; }

    /// <summary>
    /// This property holds how hazy the air is.  One is perfectly clean air, which happens nowhere;
    /// two to three is a clear day; six and beyond loses the horizon in white.
    /// </summary>
    public double Turbidity { get; set; } = 2.5;

    /// <summary>
    /// This property holds how far above sea level the scene stands, in meters.  It matters most for
    /// the haze, which is nearly all held in the lowest kilometer or two: a scene set on a mountain
    /// has a deeper and cleaner blue than one at the shore, and this is why.
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// This property holds what the whole sky is multiplied by.
    /// <para>
    /// It is an exposure and nothing more, and it is deliberately one knob rather than several: the
    /// proportions within a sky, and between the sky and its sun, are what the physics settles, and
    /// nothing here should be able to disturb them.  Turning this up brightens the sun by exactly as
    /// much as it brightens the sky.
    /// </para>
    /// </summary>
    public double Brightness { get; set; } = 1;

    /// <summary>
    /// This property holds how many heights in the sky are worked out and kept.  An even count is
    /// raised by one, so that a row falls exactly on the horizon where the sky has a real step in it.
    /// </summary>
    public int Rows { get; set; } = 97;

    /// <summary>
    /// This property holds how many ways round are worked out and kept.
    /// </summary>
    public int Columns { get; set; } = 64;

    /// <summary>
    /// This property reports which way the sun lies, worked out from how high it stands and which way
    /// round it is.
    /// </summary>
    public Vector TowardSun
    {
        get
        {
            double up = SunElevation * Math.PI / 180;
            double around = SunAzimuth * Math.PI / 180;
            double flat = Math.Cos(up);

            return new Vector(flat * Math.Sin(around), Math.Sin(up), -flat * Math.Cos(around)).Unit;
        }
    }

    /// <summary>
    /// This property notes whether the sky also supplies the sun as a light of its own.
    /// <para>
    /// It does by default, and that is the whole point of it: a scene says where the sun stands, and
    /// what color it is at that height follows from the air it has just come through.  Saying it
    /// outright is how a white sun ends up hanging in a red sky, which is the commonest way to get one
    /// of these wrong.
    /// </para>
    /// <para>
    /// A scene wanting the sky without its sun writes <c>no light</c>.  A scene merely wanting to add
    /// a lamp of its own need do nothing here -- as many lights may stand beside this one as the scene
    /// likes.
    /// </para>
    /// </summary>
    public bool MakesItsOwnLight { get; set; } = true;

    /// <summary>
    /// This method returns the sun that goes with this sky, or <c>null</c> if the scene asked for
    /// none.
    /// <para>
    /// The color is what is left of the sunlight after the air it crossed to reach the ground, which
    /// is why a low sun comes back orange and a very low one nearly gone.  The brightness applies here
    /// exactly as it does to the sky itself: it is an exposure, and moving it must not change what the
    /// sun is worth against the sky it stands in.
    /// </para>
    /// </summary>
    /// <returns>The sun as a light, or <c>null</c>.</returns>
    public DistantLight SunAsALight()
    {
        if (!MakesItsOwnLight)
            return null;

        Vector toward = TowardSun;
        Color color = SpectralColor.ToColor(
            new Atmosphere { Turbidity = Turbidity }.SunlightAfterAir(toward, Height));

        // Divided by pi, and it is worth saying why, because getting this wrong makes a sky look three
        // times too dark and sends you hunting through the physics for the missing light.
        //
        // The sun's color here is how much light falls on a square meter, while the sky's is how
        // bright it looks -- two different quantities that happen to be written in the same units.  A
        // surface facing light of strength E does not glow at E; it spreads what it caught over every
        // direction and so glows at E over pi.  This renderer has no such division in its shading -- a
        // white surface under a light of one comes back at one -- which is exactly right for the sky,
        // whose colors are brightnesses already, and wrong by pi for a sun, whose color is not.
        double spread = Brightness / Math.PI;

        return new DistantLight
        {
            // A light's direction is the way its rays travel, which is away from where the sun is.
            Direction = new Vector(-toward.X, -toward.Y, -toward.Z),
            Color = new Color(color.Red * spread, color.Green * spread, color.Blue * spread)
        };
    }

    private SkyTable _sky;

    /// <summary>
    /// This method works the whole sky out, once, before any ray is fired.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="surface">The surface this pigment is set on, which for a sky is none.</param>
    protected override void PrepareForRendering(RenderContext context, Surface surface)
    {
        _sky = new SkyTable(
            new Atmosphere { Turbidity = Turbidity }, TowardSun, Height, Rows, Columns);
    }

    /// <summary>
    /// This method returns the color of the sky in the given direction.
    /// <para>
    /// The point handed in is a direction rather than a place, the background being read as a sphere
    /// infinitely far off.  That is exactly what a sky is, so nothing needs converting.
    /// </para>
    /// </summary>
    /// <param name="point">The direction to look in.</param>
    /// <returns>The color of the sky that way.</returns>
    public override Color GetColorFor(Point point)
    {
        // A scene that never had its chance to get ready still has to answer something rather than
        // fall over, and working it out on the spot is right even if it is slow.
        _sky ??= new SkyTable(
            new Atmosphere { Turbidity = Turbidity }, TowardSun, Height, Rows, Columns);

        Color found = _sky.Toward(new Vector(point.X, point.Y, point.Z));

        return Brightness == 1
            ? found
            : new Color(found.Red * Brightness, found.Green * Brightness, found.Blue * Brightness);
    }

    /// <summary>
    /// This method reports whether this sky is the same as another pigment.
    /// </summary>
    /// <param name="other">The pigment to compare to.</param>
    /// <returns><c>true</c>, if the two are the same, or <c>false</c>, if not.</returns>
    public override bool Matches(Pigment other)
    {
        return other is PhysicalSkyPigment sky &&
               SunElevation.Equals(sky.SunElevation) &&
               SunAzimuth.Equals(sky.SunAzimuth) &&
               Turbidity.Equals(sky.Turbidity) &&
               Height.Equals(sky.Height) &&
               Brightness.Equals(sky.Brightness);
    }
}
