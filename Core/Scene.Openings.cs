using RayTracer.Basics;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This part of the scene finds the openings its sky comes into a room through -- every surface marked as a
/// portal, in every place it stands -- and hands them to its sky lights; see <see cref="SkyPortals"/>.
/// </summary>
public partial class Scene
{
    // How many ways out of an opening are looked along, each side, to tell which side the sky is on.
    private const int LooksOutOfAnOpening = 64;

    /// <summary>
    /// This method finds every opening marked in the scene and hands them all to each of its sky lights.  It
    /// runs once the scene is arranged, since which side of an opening the sky lies on is found by looking.
    /// </summary>
    internal void OpenTheSky()
    {
        SkyLight[] skies = Lights.OfType<SkyLight>().ToArray();

        if (skies.Length == 0)
            return;

        List<SkyPortal> portals = [];

        FindTheOpenings(Surfaces, [], portals, new Dictionary<Surface, bool>(ReferenceEqualityComparer.Instance));

        if (portals.Count == 0)
            return;

        foreach (SkyLight sky in skies)
            sky.Openings = portals;
    }

    /// <summary>
    /// This method finds every surface marked as a portal, as it stands in each place, walking down through
    /// groups, combinations and shared shapes -- into a shared shape only where it holds a portal, which is
    /// worked out once for each shape and kept.  A surface marked is an opening as a whole: what it holds is not
    /// looked into for more.
    /// </summary>
    private void FindTheOpenings(
        IEnumerable<Surface> surfaces, List<Surface> through, List<SkyPortal> portals,
        Dictionary<Surface, bool> holding)
    {
        foreach (Surface surface in surfaces)
        {
            if (surface.OpensToTheSky)
            {
                portals.Add(OpeningOf(surface, PortalsOf(through)));

                continue;
            }

            switch (surface)
            {
                case Group group:
                    FindTheOpenings(group.Surfaces, through, portals, holding);
                    break;
                case CsgSurface csg:
                    FindTheOpenings([csg.Left, csg.Right], through, portals, holding);
                    break;
                case IAssembled { Root: not null } assembled:
                    FindTheOpenings([assembled.Root], through, portals, holding);
                    break;
                case Instance instance when HoldsAnOpening(instance.Prototype, holding):
                    through.Add(instance);
                    FindTheOpenings([instance.Prototype], through, portals, holding);
                    through.RemoveAt(through.Count - 1);
                    break;
            }
        }
    }

    /// <summary>
    /// This method reports whether anything in a surface is marked as a portal, remembering the answer for every
    /// shape it asks about.
    /// </summary>
    private static bool HoldsAnOpening(Surface surface, Dictionary<Surface, bool> holding)
    {
        if (holding.TryGetValue(surface, out bool holds))
            return holds;

        holds = surface.OpensToTheSky || surface switch
        {
            Group group => group.Surfaces.Any(child => HoldsAnOpening(child, holding)),
            CsgSurface csg => HoldsAnOpening(csg.Left, holding) || HoldsAnOpening(csg.Right, holding),
            IAssembled { Root: not null } assembled => HoldsAnOpening(assembled.Root, holding),
            Instance instance => HoldsAnOpening(instance.Prototype, holding),
            _ => false
        };
        holding[surface] = holds;

        return holds;
    }

    /// <summary>
    /// This method returns the instances passed through, as a hit found through them would carry them: the
    /// innermost first.
    /// </summary>
    private static Portals PortalsOf(List<Surface> through)
    {
        Portals portals = default;

        for (int index = through.Count - 1; index >= 0; index--)
            portals = portals.And(through[index]);

        return portals;
    }

    /// <summary>
    /// This method returns the opening a surface marked as a portal makes: the rectangle through the middle of
    /// its box, as it stands in the world, between its two widest sides, facing the side the sky is on.
    /// <para>
    /// **Which side that is, is found by looking**, so that no one need say which way a window faces.  From just
    /// beyond each face of the box, a few dozen ways out over the half of the world it faces are each followed as
    /// a shadow ray is; the side more of them escape from is the sky's.  Out of a room, the sky is found by every
    /// way that clears the frame; into it, by none.
    /// </para>
    /// </summary>
    private SkyPortal OpeningOf(Surface surface, Portals portals)
    {
        if (surface.TrueBoundingBox() is not { IsEmpty: false } box)
            throw new InvalidOperationException("A portal must have a size: nothing marked as one may be empty.");

        Point low = box.Minimum;
        Point high = box.Maximum;
        Point corner = surface.SurfaceToWorld(low, 0, portals);
        Vector[] edges =
        [
            surface.SurfaceToWorld(new Point(high.X, low.Y, low.Z), 0, portals) - corner,
            surface.SurfaceToWorld(new Point(low.X, high.Y, low.Z), 0, portals) - corner,
            surface.SurfaceToWorld(new Point(low.X, low.Y, high.Z), 0, portals) - corner
        ];

        if (!double.IsFinite(corner.X + corner.Y + corner.Z) ||
            edges.Any(edge => !double.IsFinite(edge.X + edge.Y + edge.Z)))
            throw new InvalidOperationException("A portal must have an edge to it: nothing endless may be marked as one.");

        Array.Sort(edges, (one, other) => one.Magnitude.CompareTo(other.Magnitude));

        Vector depth = edges[0];
        Vector first = edges[1];
        Vector second = edges[2];

        if (first.Magnitude <= 0 || Math.Abs(first.Unit.Dot(second.Unit)) > 1e-6)
            throw new InvalidOperationException(
                "A portal must be a rectangle: what is marked as one may be turned and sized, but not sheared.");

        Vector normal = first.Cross(second).Unit;
        Point middle = corner + depth * 0.5;
        Point center = middle + first * 0.5 + second * 0.5;
        double clear = Math.Abs(depth.Dot(normal)) / 2 + 1e-4 * second.Magnitude + 1e-9;
        int outward = SkyFoundFrom(center + normal * clear, normal);
        int inward = SkyFoundFrom(center - normal * clear, -normal);

        if (outward == 0 && inward == 0)
            throw new InvalidOperationException("A portal must have the sky on one side of it; this one has none.");

        return SkyPortal.Of(middle, first, second, outward >= inward ? normal : -normal);
    }

    /// <summary>
    /// This method counts how many of a few dozen ways out from a place, spread evenly over the half of the world
    /// one way faces, reach the sky as a shadow ray would.
    /// </summary>
    private int SkyFoundFrom(Point place, Vector facing)
    {
        (Vector across, Vector along) = Microfacets.FrameAround(facing);
        int found = 0;

        for (int look = 0; look < LooksOutOfAnOpening; look++)
        {
            double cosine = 1 - (look + 0.5) / LooksOutOfAnOpening;
            double sine = Math.Sqrt(Math.Max(0, 1 - cosine * cosine));
            double around = look * 2.399963229728653;
            Vector way = (across * (sine * Math.Cos(around)) + along * (sine * Math.Sin(around)) + facing * cosine).Unit;

            if (!GetLightReaching<RgbSpectrum>(place, way, double.PositiveInfinity).IsBlack)
                found++;
        }

        return found;
    }
}
