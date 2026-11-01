using System.Diagnostics.CodeAnalysis;
using RayTracer.Basics;

namespace RayTracer.Core;

/// <summary>
/// This class represents a point light source in a rendered scene: a lamp at a place, shining
/// equally in every direction.
/// </summary>
public class PointLight : Light
{
    /// <summary>
    /// This property notes the location of the light source.
    /// </summary>
    [SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
    public Point Location { get; set; } = Point.Zero;

    /// <summary>
    /// This property holds the light's profile, if it has one: how bright it is each way, as a real fitting is
    /// measured to be; see <see cref="LightProfile"/>.  <c>null</c>, for a light as bright every way.
    /// </summary>
    public LightProfile Profile { get; set; }

    /// <summary>
    /// This property holds where the profile's straight down points, or <c>null</c> for straight down.
    /// </summary>
    public Point ProfileAim { get; set; }

    /// <summary>
    /// This property holds how far the profile is turned about its straight down, in radians: its horizontal angle
    /// nought lies along +X unturned (along +Z, for one aimed along X), and 90 along +Z, counterclockwise seen from
    /// above.
    /// </summary>
    public double ProfileTurn { get; set; }

    // The profile's straight down and its horizontal angle nought, each as a place one unit off the light, so that
    // placing the light carries them with it; worked out the first time they are wanted.
    private sealed record ProfileFrame(Point Down, Point Along);

    private ProfileFrame _frame;

    /// <summary>
    /// This property returns where the profile's straight down points when nothing says otherwise: straight down.  A
    /// spotlight points it where its cone does.
    /// </summary>
    protected virtual Point ProfileAimed => ProfileAim;

    /// <summary>
    /// This method returns how bright the light is toward a point, as a share of its brightest: what its profile says
    /// that way, or all of it for a light with none.
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <returns>The share.</returns>
    public override double IntensityToward(Point point)
    {
        if (Profile is null)
            return 1;

        ProfileFrame frame = _frame ??= FrameOf();
        Vector down = (frame.Down - Location).Unit;
        Vector along = frame.Along - Location;

        along = (along - down * along.Dot(down)).Unit;

        return Profile.Toward(point - Location, down, along);
    }

    /// <summary>
    /// This method works out the profile's frame where the light was written: its straight down where it is aimed,
    /// and its horizontal angle nought along +X laid square to that, or along +Z for a profile aimed along X, turned.
    /// </summary>
    private ProfileFrame FrameOf()
    {
        Vector down = ProfileAimed is { } aim && !aim.Matches(Location) ? (aim - Location).Unit : new Vector(0, -1, 0);
        Vector start = Math.Abs(down.X) < 0.9 ? new Vector(1, 0, 0) : new Vector(0, 0, 1);
        Vector along = (start - down * start.Dot(down)).Unit;
        Vector across = down.Cross(along);
        Vector turned = along * Math.Cos(ProfileTurn) + across * Math.Sin(ProfileTurn);

        return new ProfileFrame(Location + down, Location + turned);
    }

    /// <summary>
    /// This method works out which way the light lies from the given point and how far off it is,
    /// which for a point light is simply the direction and distance to where it stands.
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <returns>The unit direction from the point toward the light, and the distance to it.</returns>
    public override (Vector Direction, double Distance) TowardFrom(Point point)
    {
        Vector vector = Location - point;

        return (vector.Unit, vector.Magnitude);
    }

    /// <summary>
    /// This method carries where a copy of this light stands through the given placing.
    /// </summary>
    /// <param name="placing">What carries a point from the surface's space into the world.</param>
    protected override void PlaceBy(Func<Point, Point> placing)
    {
        if (Profile is not null)
        {
            ProfileFrame frame = _frame ??= FrameOf();

            _frame = new ProfileFrame(placing(frame.Down), placing(frame.Along));
        }

        Location = placing(Location);
    }
}
