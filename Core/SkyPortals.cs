using RayTracer.Basics;

namespace RayTracer.Core;

/// <summary>
/// This class picks directions toward the sky through the openings a scene marks as portals, from any point in
/// the room they open out of, by the sky's light through each.
/// <para>
/// **A sky light in a room finds next to nothing.**  From the middle of a floor a window is a fiftieth of the half
/// of the sky the floor faces, so of the two dozen directions a sky light looks in, half of one goes through it,
/// and the rest end on plaster.  Looked in the same directions from every point, as a sky of patterns is, each one
/// that does go through it throws the window's shape onto the floor, and the room is lit by two dozen hard-edged
/// windows.  Looked at through the openings themselves, every direction goes through one.
/// </para>
/// <para>
/// A point picks an opening by the sky's light through it as the point sees it, then a direction through it by
/// that light (see <see cref="PortalMap"/>; openings in one wall share a map).  The odds of any direction are the
/// sum over the openings it passes through, so a direction is counted fairly beside directions picked any other
/// way -- which is how the sky light uses this: alongside its own ways of looking, never instead of them (see
/// <see cref="SkyLight"/>).  A point on the sky's side of every opening sees none of them, and is looked at from as
/// it always was.
/// </para>
/// </summary>
public sealed class SkyPortals
{
    // The openings, and the map each is looked through, shared by the openings in one wall.
    private readonly SkyPortal[] _portals;
    private readonly PortalMap[] _maps;

    // What a thread worked out last, about the openings as seen from a point: the shading asks about one point
    // many times over -- once for each of the sky's samples, and once more for each one's odds.
    [ThreadStatic] private static View _view;

    private SkyPortals(SkyPortal[] portals, PortalMap[] maps)
    {
        _portals = portals;
        _maps = maps;
    }

    /// <summary>
    /// This method lays out the sky as seen through the given openings, one map for each way a wall faces.
    /// </summary>
    /// <param name="portals">The openings.</param>
    /// <param name="brightness">How bright the sky is over a cell of a map, given the direction of its middle and
    /// the two edges of the patch of sky it covers, across and down.</param>
    /// <param name="size">How many cells each map has each way.</param>
    /// <returns>The openings, ready to be looked through.</returns>
    public static SkyPortals Of(
        IReadOnlyList<SkyPortal> portals, Func<Vector, Vector, Vector, double> brightness, int size)
    {
        PortalMap[] maps = new PortalMap[portals.Count];

        for (int index = 0; index < portals.Count; index++)
        {
            SkyPortal portal = portals[index];
            int same = -1;

            for (int earlier = 0; earlier < index && same < 0; earlier++)
            {
                if (SameFrame(portal, portals[earlier]))
                    same = earlier;
            }

            maps[index] = same >= 0
                ? maps[same]
                : PortalMap.Of(portal.Along, portal.Across, portal.Outward, brightness, size);
        }

        return new SkyPortals(portals.ToArray(), maps);
    }

    /// <summary>
    /// This property holds how many openings there are.
    /// </summary>
    public int Count => _portals.Length;

    /// <summary>
    /// This method reports whether a point sees any of the sky through any of the openings: it stands on the
    /// room's side of one, and there is light beyond it.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns><c>true</c>, if it does.</returns>
    public bool Faces(Point point) => ViewFrom(point).Total > 0;

    /// <summary>
    /// This method returns the sky's light seen through the openings from a point: its brightness over the solid
    /// angle each opening covers, summed.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The light through them.</returns>
    public double LightThrough(Point point) => ViewFrom(point).Total;

    /// <summary>
    /// This method picks a direction from a point through one of the openings, by the sky's light through them.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <param name="first">The first number, from nothing up to one: which opening, and where across it.</param>
    /// <param name="second">The second: where up it.</param>
    /// <returns>The unit direction, or <c>null</c> where none could be picked.</returns>
    public Vector Pick(Point point, double first, double second)
    {
        View view = ViewFrom(point);

        if (view.Total <= 0)
            return null;

        // Which opening: the first number's place among the openings' shares, and its place within that
        // opening's share kept for where across it, so the samples spread over each as evenly as over the whole.
        double left = Math.Clamp(first, 0, 1 - 1e-12) * view.Total;
        int which = -1;

        for (int index = 0; index < _portals.Length; index++)
        {
            if (view.Weights[index] <= 0)
                continue;

            which = index;

            if (left < view.Weights[index])
                break;

            left -= view.Weights[index];
        }

        double within = Math.Clamp(left / view.Weights[which], 0, 1 - 1e-12);
        (double U0, double V0, double U1, double V1) window = view.Windows[which];

        if (_maps[which].Pick(within, second, window.U0, window.V0, window.U1, window.V1) is not { } place)
            return null;

        Vector local = PortalMap.LocalAt(place.U, place.V);
        SkyPortal portal = _portals[which];

        return (portal.Along * local.X + portal.Across * local.Y + portal.Outward * local.Z).Unit;
    }

    /// <summary>
    /// This method returns how likely <see cref="Pick"/> is to pick a direction from a point, per unit of solid
    /// angle: the sum, over the openings the direction passes through, of the opening's share of the light times
    /// the odds of the direction within it.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <param name="direction">The unit direction.</param>
    /// <returns>The odds.</returns>
    public double OddsOf(Point point, Vector direction)
    {
        View view = ViewFrom(point);

        if (view.Total <= 0)
            return 0;

        double odds = 0;

        for (int index = 0; index < _portals.Length; index++)
        {
            if (view.Weights[index] <= 0)
                continue;

            SkyPortal portal = _portals[index];
            Vector local = new (direction.Dot(portal.Along), direction.Dot(portal.Across), direction.Dot(portal.Outward));

            if (local.Z <= 0)
                continue;

            (double u, double v) = PortalMap.PlaceOf(local);
            (double U0, double V0, double U1, double V1) window = view.Windows[index];

            if (u < window.U0 || u > window.U1 || v < window.V0 || v > window.V1)
                continue;

            // The opening's share of the light, times its cell's weight over the light in the window, carried
            // from the grid's area to solid angle: the window's light cancels.
            odds += _maps[index].ValueAt(u, v) / view.Total / PortalMap.Stretch(local);
        }

        return odds;
    }

    /// <summary>
    /// This method returns what the openings look like from a point: each one's window on its map and the sky's
    /// light in it, worked out once for the last point a thread asked about.
    /// </summary>
    private View ViewFrom(Point point)
    {
        View view = _view;

        if (view is not null && ReferenceEquals(view.Owner, this) &&
            view.X == point.X && view.Y == point.Y && view.Z == point.Z)
            return view;

        if (view is null || view.Weights.Length != _portals.Length)
            _view = view = new View(_portals.Length);

        view.Owner = this;
        view.X = point.X;
        view.Y = point.Y;
        view.Z = point.Z;
        view.Total = 0;

        for (int index = 0; index < _portals.Length; index++)
        {
            SkyPortal portal = _portals[index];
            (double x, double y, double z) = portal.Place(point);

            // A point on the sky's side of an opening, or in its plane, sees nothing of the sky through it.
            if (z >= 0)
            {
                view.Weights[index] = 0;

                continue;
            }

            double height = -z;
            (double U0, double V0, double U1, double V1) window = (
                (Math.Atan2(-x, height) + Math.PI / 2) / Math.PI,
                (Math.Atan2(-y, height) + Math.PI / 2) / Math.PI,
                (Math.Atan2(portal.Length - x, height) + Math.PI / 2) / Math.PI,
                (Math.Atan2(portal.Width - y, height) + Math.PI / 2) / Math.PI);
            double light = _maps[index].Integral(window.U0, window.V0, window.U1, window.V1);

            view.Windows[index] = window;
            view.Weights[index] = light;
            view.Total += light;
        }

        return view;
    }

    /// <summary>
    /// This method reports whether two openings stand in the same frame, and so may share a map.
    /// </summary>
    private static bool SameFrame(SkyPortal one, SkyPortal other) =>
        Alike(one.Along, other.Along) && Alike(one.Across, other.Across) && Alike(one.Outward, other.Outward);

    private static bool Alike(Vector one, Vector other) => (one - other).Magnitude < 1e-9;

    /// <summary>
    /// This class holds what the openings look like from one point.
    /// </summary>
    private sealed class View(int count)
    {
        public SkyPortals Owner;
        public double X, Y, Z;
        public double Total;
        public readonly double[] Weights = new double[count];
        public readonly (double U0, double V0, double U1, double V1)[] Windows =
            new (double, double, double, double)[count];
    }
}
