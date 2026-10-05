using RayTracer.Basics;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This enumeration notes the ways a decal's outline may be carried onto a surface: the same four an
/// image map has.
/// </summary>
public enum DecalProjection
{
    /// <summary>
    /// The outline lies in the decal's X-Z plane and is carried straight along its Y axis onto
    /// whatever lies in its path, the way a slide projector throws a picture onto a wall.
    /// </summary>
    Planar,

    /// <summary>
    /// The outline is wrapped around the decal's Y axis: across it runs around the axis and up it
    /// runs along it.
    /// </summary>
    Cylindrical,

    /// <summary>
    /// The outline is wrapped over a sphere about the decal's origin: across it runs along the
    /// equator and up it runs along a meridian.
    /// </summary>
    Spherical,

    /// <summary>
    /// The outline is wrapped over a ring about the decal's Y axis: across it runs around the ring
    /// and up it runs around the tube.
    /// </summary>
    Toroidal
}

/// <summary>
/// This class represents a decal: a marking painted onto a surface by carrying an outline onto it.
/// Inside the outline the surface takes the decal's color; outside it, it keeps its own.
/// <para>
/// A decal is painted, not built.  It has no thickness and stands on nothing, so it cannot flicker
/// against the surface under it, float off it, throw a shadow or fall out of a combination the way a
/// thin plate laid over the surface would.  It belongs to a material, and is read in the space of the
/// surface that material was written on -- its <see cref="Material.Anchor"/> -- so that a marking
/// written on a whole saucer lands in one place across every part of it.
/// </para>
/// <para>
/// **Two kinds of length are at work, and they answer to different things.**  The outline is drawn in
/// the decal's own units, so a scale written in the decal sizes it.  Where the surface lies -- how far
/// the decal reaches, and the radius of a toroidal decal's ring -- is said in the units of the surface
/// it is on, so that sizing the outline cannot move the decal off the thing it was meant for.
/// </para>
/// <para>
/// Each wrapped projection measures across and up as lengths along the surface, taken at the point's
/// own distance from the axis, center or ring.  So an outline lands at its true size on a cylinder of
/// any radius, without the radius having to be said, as a sticker would.  Each is centered on the
/// decal's +X side, reading left to right as seen from outside, with up toward +Y.
/// </para>
/// </summary>
public class Decal
{
    /// <summary>
    /// This property holds the surface the decal was written on, when that is not the anchor of the
    /// material it is in.  A decal is read in its material's anchor's space (see
    /// <see cref="Material.Anchor"/>), and nearly always that is where it was written; but a material
    /// handed down onto one that inherits comes away carrying decals written on two surfaces, so the
    /// inheriting one's decals are given their own here as they join it.  See
    /// <see cref="Material.HandedDown"/>.
    /// </summary>
    public Surface Anchor { get; set; }

    /// <summary>
    /// This property holds the outline that says where the decal paints.
    /// </summary>
    public FlattenedPath Outline { get; set; }

    /// <summary>
    /// This property holds the color painted inside the outline.  Its alpha, if it has one less
    /// than full, lets the surface under it show through.
    /// </summary>
    public Color Color { get; set; } = Colors.Black;

    /// <summary>
    /// This property holds how the outline is carried onto the surface.
    /// </summary>
    public DecalProjection Projection { get; set; } = DecalProjection.Planar;

    /// <summary>
    /// This property holds the near end of how far the decal reaches: along its projection for a
    /// planar decal, and out from its axis, center or ring for the others.
    /// </summary>
    public double MinimumReach { get; set; }

    /// <summary>
    /// This property holds the far end of how far the decal reaches.
    /// </summary>
    public double MaximumReach { get; set; }

    /// <summary>
    /// This property holds the radius of the ring a toroidal decal is wrapped over, in the units of
    /// the surface it is on.
    /// </summary>
    public double RingRadius { get; set; } = 1;

    /// <summary>
    /// This property notes whether the decal fades out where the surface turns away from it.  See
    /// <see cref="LayerOver"/>.
    /// </summary>
    public bool Fades { get; set; } = true;

    /// <summary>
    /// This property holds the angle, in radians, between the surface's normal and the way the decal
    /// is carried onto it, up to which the decal is at full strength.
    /// </summary>
    public double FadeFrom
    {
        get => _fadeFrom;
        set
        {
            _fadeFrom = value;
            _fullUntil = Math.Cos(value);
        }
    }

    /// <summary>
    /// This property holds the angle, in radians, by which the decal is gone.
    /// </summary>
    public double FadeTo
    {
        get => _fadeTo;
        set
        {
            _fadeTo = value;
            _goneBy = Math.Cos(value);
        }
    }

    /// <summary>
    /// This property holds where the decal is placed, from its own space into its anchor's.
    /// </summary>
    public Matrix Transform
    {
        get => _transform;
        set
        {
            _transform = value;
            _inverse = value.Invert();

            // The rows of the inverse say how fast each of the decal's coordinates changes across
            // the anchor's space, so the one for Y turns a decal-space height into a distance
            // between the planes a planar decal's reach is bounded by.
            double x = _inverse.Entry(1, 0);
            double y = _inverse.Entry(1, 1);
            double z = _inverse.Entry(1, 2);

            _depthScale = 1 / Math.Sqrt(x * x + y * y + z * z);
        }
    }

    private static readonly Vector Across = new (1, 0, 0);
    private static readonly Vector Up = new (0, 0, 1);
    private static readonly Vector Straight = new (0, 1, 0);

    private Matrix _transform = Matrix.Identity;
    private Matrix _inverse = Matrix.Identity;
    private double _depthScale = 1;

    // The fade's angles, and their cosines, which are what a facing is compared against.  60 degrees
    // and 75 unless the scene says otherwise.
    private double _fadeFrom = Math.PI / 3;
    private double _fadeTo = Math.PI * 5 / 12;
    private double _fullUntil = Math.Cos(Math.PI / 3);
    private double _goneBy = Math.Cos(Math.PI * 5 / 12);

    /// <summary>
    /// This method lays the decal over the color a surface already has at a point.
    /// <para>
    /// The point is first carried into the decal's own space and from there into the outline's, by
    /// way of the projection.  If it lies outside the decal's reach, the decal is not there at all;
    /// this is what stops a marking on a hull's upper face printing straight through to the lower
    /// one.  Otherwise the decal's color goes on top in proportion to how much of the footprint its
    /// outline covers, and how squarely the surface faces the way the decal is carried onto it.
    /// </para>
    /// <para>
    /// **The facing is what stops a decal smearing down a wall.**  A reach keeps a decal off the far
    /// side of a thing, but a wall standing up inside it -- the side of a bridge rising from a
    /// saucer's top -- lies in the reach too, and a projection carried straight down onto it is
    /// stretched along it without limit.  So the decal is at full strength until the surface turns
    /// 60 degrees away from its projection, and fades out completely by 75, smoothly, so the fade
    /// does not draw a line of its own.  Which side the surface faces from does not matter.  A scene
    /// may move both angles, or turn the fade off -- a decal meant to wrap a sharp corner wants
    /// exactly that.
    /// </para>
    /// </summary>
    /// <param name="under">The color the surface has before this decal is painted on it.</param>
    /// <param name="point">The point being colored, in the anchor's space.</param>
    /// <param name="footprint">The patch of surface the ray covers there, in the anchor's space, or
    /// <c>null</c>, if there is none, in which case the outline has a hard edge.</param>
    /// <param name="normal">The surface's normal there, in the anchor's space and of any length,
    /// or <c>null</c>, if there is none to go on, in which case the decal does not fade.</param>
    /// <returns>The color with this decal painted over it.</returns>
    public Color LayerOver(Color under, Point point, Footprint footprint = null, Vector normal = null)
    {
        Point local = _inverse * point;
        Vector across = null;
        Vector along = null;

        if (footprint is not null && !footprint.IsEmpty)
        {
            across = _inverse * footprint.Across;
            along = _inverse * footprint.Along;
        }

        Vector toward = null;
        double coverage = Projection switch
        {
            DecalProjection.Planar => PlanarCoverage(local, across, along, out toward),
            DecalProjection.Cylindrical => CylindricalCoverage(local, across, along, out toward),
            DecalProjection.Spherical => SphericalCoverage(local, across, along, out toward),
            DecalProjection.Toroidal => ToroidalCoverage(local, across, along, out toward),
            _ => 0
        };

        if (coverage > 0 && Fades && normal is not null)
            coverage *= Facing(toward, normal);

        return coverage <= 0
            ? under
            : Color.WithAlpha(Color.Alpha * coverage).LayerOnTopOf(under);
    }

    /// <summary>
    /// This method works out the coverage for a planar decal.  The outline lies in X-Z, so the point
    /// keeps its X and Z and loses its Y -- which is what makes a decal smear across a face it meets
    /// at a steep slant, and exactly why it does.
    /// </summary>
    private double PlanarCoverage(Point local, Vector across, Vector along, out Vector toward)
    {
        double depth = local.Y * _depthScale;

        toward = Straight;

        return Within(depth)
            ? CoverageAt(local.X, local.Z, Across, Up, across, along)
            : 0;
    }

    /// <summary>
    /// This method works out the coverage for a cylindrical decal: across is the length around the
    /// axis at the point's own distance from it, and up is the height along it.
    /// </summary>
    private double CylindricalCoverage(Point local, Vector across, Vector along, out Vector toward)
    {
        double rho = Math.Sqrt(local.X * local.X + local.Z * local.Z);

        toward = null;

        if (rho == 0 || !Within(Reach(new Vector(local.X, 0, local.Z))))
            return 0;

        toward = new Vector(local.X / rho, 0, local.Z / rho);

        double theta = Math.Atan2(local.Z, local.X);

        // u = rho * theta, so a step changes it by theta times the change in rho plus rho times the
        // change in theta; v is the height.
        Vector gradient = new (
            (theta * local.X - local.Z) / rho, 0, (theta * local.Z + local.X) / rho);

        return CoverageAt(rho * theta, local.Y, gradient, new Vector(0, 1, 0), across, along);
    }

    /// <summary>
    /// This method works out the coverage for a spherical decal: across is the length along the
    /// equator and up the length along a meridian, both at the point's own distance from the center.
    /// </summary>
    private double SphericalCoverage(Point local, Vector across, Vector along, out Vector toward)
    {
        Vector offset = new (local.X, local.Y, local.Z);
        double radius = offset.Magnitude;
        double rho = Math.Sqrt(local.X * local.X + local.Z * local.Z);

        toward = null;

        if (rho == 0 || !Within(Reach(offset)))
            return 0;

        toward = offset / radius;

        double longitude = Math.Atan2(local.Z, local.X);
        double latitude = Math.Atan2(local.Y, rho);
        Vector towardRadius = toward;
        Vector towardLongitude = new (-local.Z / (rho * rho), 0, local.X / (rho * rho));
        Vector towardRho = new (local.X / rho, 0, local.Z / rho);
        Vector towardLatitude = (new Vector(0, rho, 0) - towardRho * local.Y) / (radius * radius);

        return CoverageAt(
            radius * longitude, radius * latitude,
            towardRadius * longitude + towardLongitude * radius,
            towardRadius * latitude + towardLatitude * radius,
            across, along);
    }

    /// <summary>
    /// This method works out the coverage for a toroidal decal: across is the length around the ring
    /// at the point's own distance from the axis, and up the length around the tube at its own
    /// distance from the ring, starting from the outer equator and rising toward +Y.
    /// </summary>
    private double ToroidalCoverage(Point local, Vector across, Vector along, out Vector toward)
    {
        double rho = Math.Sqrt(local.X * local.X + local.Z * local.Z);

        toward = null;

        if (rho == 0)
            return 0;

        Vector outward = new (local.X / rho, 0, local.Z / rho);

        // The ring's radius is said in the anchor's units, so it is found in the decal's own by how
        // far a step outward in the decal's space reaches in the anchor's.
        double ring = RingRadius / (_transform * outward).Magnitude;
        Vector offset = new Vector(local.X, local.Y, local.Z) - outward * ring;
        double tube = offset.Magnitude;

        if (tube == 0 || !Within(Reach(offset)))
            return 0;

        toward = offset / tube;

        double theta = Math.Atan2(local.Z, local.X);
        double s = rho - ring;
        double t = local.Y;
        double phi = Math.Atan2(t, s);
        Vector towardRho = outward;
        Vector towardTheta = new (-local.Z / (rho * rho), 0, local.X / (rho * rho));
        Vector towardTube = (towardRho * s + new Vector(0, t, 0)) / tube;
        Vector towardPhi = (new Vector(0, s, 0) - towardRho * t) / (tube * tube);

        return CoverageAt(
            rho * theta, tube * phi,
            towardRho * theta + towardTheta * rho,
            towardTube * phi + towardPhi * tube,
            across, along);
    }

    /// <summary>
    /// This method returns how much of the decal survives the way the surface faces: all of it while
    /// the surface is within <see cref="FadeFrom"/> of square to the projection, none past
    /// <see cref="FadeTo"/>, and a smooth fade between.  Two equal angles make a clean cut there.
    /// The angle is taken in the anchor's space, as everything else about a decal is.
    /// </summary>
    /// <param name="toward">The way the decal is carried onto the surface at the point, in the
    /// decal's own space.</param>
    /// <param name="normal">The surface's normal at the point, in the anchor's space.</param>
    /// <returns>The share of the decal to keep, from 0 to 1.</returns>
    private double Facing(Vector toward, Vector normal)
    {
        Vector carried = _transform * toward;
        double cosine = Math.Abs(normal.Dot(carried)) / (normal.Magnitude * carried.Magnitude);

        if (cosine >= _fullUntil)
            return 1;

        if (cosine <= _goneBy)
            return 0;

        double t = (cosine - _goneBy) / (_fullUntil - _goneBy);

        return t * t * (3 - 2 * t);
    }

    /// <summary>
    /// This method returns how far a decal-space offset reaches in the anchor's units.
    /// </summary>
    private double Reach(Vector offset)
    {
        return (_transform * offset).Magnitude;
    }

    /// <summary>
    /// This method reports whether a reach lies within the decal's.
    /// </summary>
    private bool Within(double reach)
    {
        return reach >= MinimumReach && reach <= MaximumReach;
    }

    /// <summary>
    /// This method asks the outline how much of the footprint it covers, given where the point lands
    /// in the outline's plane and how fast that changes across the decal's space.  The footprint's
    /// edges are carried into the outline's plane by those rates of change, which is all a footprint
    /// so small needs.
    /// </summary>
    private double CoverageAt(
        double u, double v, Vector towardU, Vector towardV, Vector across, Vector along)
    {
        return across is null
            ? Outline.CoverageAt(u, v)
            : Outline.CoverageAt(
                u, v,
                towardU.Dot(across), towardV.Dot(across),
                towardU.Dot(along), towardV.Dot(along));
    }
}
