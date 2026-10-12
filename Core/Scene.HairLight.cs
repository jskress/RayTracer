using System.Collections.Concurrent;
using RayTracer.Basics;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This part of the scene gathers the light scattered among the strands of each head of hair, before the
/// hair is first shaded: see <see cref="HairLight"/>.
/// </summary>
public partial class Scene
{
    // How many paths of light are followed into a head of hair from each light, at the least and for each
    // place the strands lie in, and in how many runs.  A fine grid shares the paths among more places, and
    // too few to a place shows as blotches over a rug's pile, which fills its box; fur over a ball fills a
    // fifth of its box or less, and counting every place in the box sent it eight times the paths it
    // needs.  The runs are fixed, and each starts from its own seed and is added in its turn, so the light
    // gathered is the same however many threads there are to gather it.
    private const int PathsPerLight = 1 << 17;
    private const int PathsPerCell = 8;
    private const int Runs = 16;

    // How many cells to a strand's length the light is held at, and at most how many in all.  The limit is
    // on the whole, not on a side, so that a rug -- wide, and only as deep as its pile -- is held as finely
    // as a head of hair: held at a sixty-fourth of its width, a rug's cells were deeper than the light
    // reaches into dense pile, and the strands on top were lit as if they lay halfway down.
    private const int CellsPerLength = 6;
    private const int MostCells = 1 << 18;

    private readonly ConcurrentDictionary<(Hair, Surface), Lazy<HairLight>> _hairLights = new ();

    /// <summary>
    /// This method gathers the light among every head of hair that stands in the scene itself, before the
    /// first pixel is drawn.
    /// <para>
    /// **Left until a strand is first seen, gathering ran on one thread.**  The pixels are drawn on as
    /// many threads as there are processors and no more, and the first to meet a head of hair gathers its
    /// light while every other one that meets it waits -- which, once all of them have, leaves nothing to
    /// gather it on but the one thread doing it.  A teddy bear's took minutes that way.  Gathered here,
    /// each has the whole machine.  Hair standing behind an instance is gathered as it is met, once for
    /// each place it stands, since only then is the place known.
    /// </para>
    /// </summary>
    public void GatherTheLightAmongHair()
    {
        foreach (Hair hair in HairOutsideInstances(Surfaces))
        {
            if (hair.StrandCount > 0)
                LightAmong(hair, null);
        }
    }

    /// <summary>
    /// This method finds every head of hair among the given surfaces and what they hold, except behind an
    /// instance.
    /// </summary>
    private static IEnumerable<Hair> HairOutsideInstances(IEnumerable<Surface> surfaces)
    {
        foreach (Surface surface in surfaces)
        {
            if (surface.Hair is not null)
                yield return surface.Hair;

            IEnumerable<Surface> held = surface switch
            {
                Group group => group.Surfaces,
                CsgSurface csg => [csg.Left, csg.Right],
                Tube { Root: not null } tube => [tube.Root],
                _ => []
            };

            foreach (Hair hair in HairOutsideInstances(held))
                yield return hair;
        }
    }

    /// <summary>
    /// This method returns the light scattered among the strands of a head of hair, gathering it the first
    /// time it is asked for.  A shared head of hair stands in more than one place, each lit its own way, so
    /// it is gathered for each place it stands.
    /// </summary>
    /// <param name="hair">The hair.</param>
    /// <param name="portal">The instance it was found through, if it was.</param>
    /// <returns>The light among its strands.</returns>
    internal HairLight LightAmong(Hair hair, Surface portal)
    {
        return _hairLights.GetOrAdd(
            (hair, portal), key => new Lazy<HairLight>(() => GatherLightAmong(key.Item1, key.Item2))).Value;
    }

    /// <summary>
    /// This method gathers the light scattered among the strands of a head of hair by following light from
    /// each light into it.
    /// <para>
    /// **Each light is taken as a beam across the hair**, from the direction it lies in from the hair's
    /// middle, as wide as the hair and as bright as the light is there -- the same light a strand at the
    /// middle is lit by.  Each path of light sets off from a place picked evenly across the beam, and goes
    /// until it meets a strand: what it brings that far is the light the strand is lit by directly, which
    /// the strand's own shading counts.  From there it is scattered as the strand scatters light, in a
    /// direction picked as <see cref="Fiber.Sample{TS}"/> picks them, and everywhere it goes after that it
    /// is recorded, until it leaves the hair or gives out.  What it meets among the hair that is not hair
    /// -- the skin the hair grows from, a rug's backing -- sends it back up among the strands, as a matte
    /// surface would; see <see cref="BouncesOff"/>.
    /// </para>
    /// </summary>
    private HairLight GatherLightAmong(Hair hair, Surface portal)
    {
        (Point low, Point high) = BoxInTheWorld(hair, portal);
        Vector extent = high - low;
        double cell = Math.Max(
            LengthInTheWorld(hair, portal) / CellsPerLength, Math.Cbrt(extent.X * extent.Y * extent.Z / MostCells));

        // Each side also holds a cell's margin at either end, which the volume alone leaves out.
        while ((extent.X / cell + 3) * (extent.Y / cell + 3) * (extent.Z / cell + 3) > MostCells)
            cell *= 1.1;

        Vector margin = new (cell, cell, cell);
        HairLight grid = new (low - margin, high + margin, cell);
        Point center = low + extent * 0.5;
        double radius = extent.Magnitude / 2 + cell;
        List<Beam> beams = [];
        int paths = Math.Max(PathsPerLight, PathsPerCell * PlacesHeldIn(hair, portal, grid));

        foreach (Light light in Lights)
        {
            int count = light.SampleCount;

            for (int index = 0; index < count; index++)
            {
                LightSample sample = light.SampleAround(center, index);
                double intensity = sample.Cone * light.FadingOver(sample.Distance);

                if (intensity <= 0)
                    continue;

                // A light's color is the irradiance it gives a surface square to it, over π.
                RgbSpectrum irradiance = light.EmittedToward<RgbSpectrum>(sample) * (Math.PI * intensity);

                beams.Add(new Beam(sample.Direction, irradiance, sample.Distance, Math.Max(1, paths / count)));
            }
        }

        float[][] tallies = new float[Runs][];

        Parallel.For(0, Runs, run =>
        {
            float[] tally = new float[HairLight.Quantities * grid.Size];
            Random random = new (run + 1);

            foreach (Beam beam in beams)
            {
                for (int path = run; path < beam.Paths; path += Runs)
                    Follow(grid, tally, beam, center, radius, random);
            }

            tallies[run] = tally;
        });

        grid.Settle(tallies);

        return grid;
    }

    /// <summary>
    /// This record holds one beam of light across a head of hair: which way the light lies, how much it
    /// gives a surface square to it there, how far off it is, and how many paths of it to follow.
    /// </summary>
    private sealed record Beam(Vector Toward, RgbSpectrum Irradiance, double Distance, int Paths);

    /// <summary>
    /// This method follows one path of light into the hair.
    /// </summary>
    private void Follow(HairLight grid, float[] tally, Beam beam, Point center, double radius, Random random)
    {
        (Vector across, Vector along) = Microfacets.FrameAround(beam.Toward);
        double reach = radius * Math.Sqrt(random.NextDouble());
        double turn = 2 * Math.PI * random.NextDouble();
        Point start = center + across * (reach * Math.Cos(turn)) + along * (reach * Math.Sin(turn)) +
                      beam.Toward * radius;
        RgbSpectrum power = beam.Irradiance * (Math.PI * radius * radius / beam.Paths);

        // Whatever stands between the light and the hair shades it before it arrives.
        power *= GetLightReaching<RgbSpectrum>(start, beam.Toward, beam.Distance - radius);

        // **Nothing at all, not next to nothing.**  A path carries its share of the beam, which is less
        // the more paths there are, and asked whether it was "black" -- near nought, as a color is -- the
        // paths into a small thing in a big scene all were: a teddy bear by a fire, gathered from a
        // million paths, lost most of them and came out half as bright as from a hundred thousand.
        double first = Math.Max(power[0], Math.Max(power[1], power[2]));

        if (first <= 0)
            return;
        Ray ray = new (start, -beam.Toward);
        bool scattered = false;

        for (int bounce = 0; bounce < 256; bounce++)
        {
            (List<Intersection> hits, Intersection hit) = NearestAlong(ray, grid.Cell);

            if (scattered)
            {
                double distance = hit?.Distance ?? (grid.High - grid.Low).Magnitude * 2;

                grid.Record(tally, ray.Origin, ray.At(distance), power);
            }

            if (hit is null)
                return;

            hit.PrepareUsing(ray, hits);

            Point from;
            Vector onward;

            if (hit is HairIntersection strand)
            {
                StrandFrame frame = FrameOf(strand);
                Dye dye = DyeOf(strand, strand.Point);
                Fiber fiber = Fiber.For(dye.Roughness);
                Fiber.Seen<RgbSpectrum> seen = fiber.Seeing(
                    frame.SineOf(strand.Eye), Math.PI / 2, frame.H, dye.Absorbing<RgbSpectrum>());

                // The strand's scattering, turned round: light arriving from where this came from is sent on
                // as the strand would send light from where it goes toward an eye where it came from.
                (double sinTheta, double phi, double odds) = fiber.Sample(
                    seen, random.NextDouble(), random.NextDouble(), random.NextDouble(), random.NextDouble());

                if (odds <= 0)
                    return;

                power *= fiber.Scattered(seen, sinTheta, phi) * (1 / odds);
                onward = frame.Toward(sinTheta, phi);
                from = frame.Center + onward * frame.Radius;
            }
            else if (!BouncesOff(grid, hit, random, ref power, out from, out onward))
                return;

            scattered = true;

            // Past the first few strands, a path that has given out most of its light is let go, as often
            // as what it still carries is little, and what is kept is counted for the ones let go.
            if (bounce >= 2)
            {
                double survives = Math.Min(0.95, Math.Max(power[0], Math.Max(power[1], power[2])) / first);

                if (random.NextDouble() >= survives)
                    return;

                power *= 1 / survives;
            }

            ray = new Ray(from, onward);
        }
    }

    /// <summary>
    /// This method sends a path of light that has met something other than a strand back among the strands,
    /// as that surface sends back light: as much of it as the surface's color and diffuse say, spread over
    /// the side it met as a matte surface spreads it.
    /// <para>
    /// **Most of the light a pale strand stops goes on forward**, so a head of pale hair passes much of what
    /// falls on it down to the skin beneath; a rug's pile, to its backing.  The skin sends what it does not
    /// absorb back up among the strands, and without it pale fur came out half as bright as a matte surface
    /// of its color, however dense.  Only what lies among the hair -- within its box -- sends light back,
    /// so that this stays the light among the strands and does not become the light bounced round a room.
    /// What lets light through is left out, as anything else beyond the hair is.
    /// </para>
    /// </summary>
    /// <returns><c>true</c>, if the light goes on.</returns>
    private static bool BouncesOff(
        HairLight grid, Intersection hit, Random random, ref RgbSpectrum power, out Point from, out Vector onward)
    {
        Material material = hit.Material;

        from = hit.OverPoint;
        onward = null;

        if (!grid.Holds(hit.Point) || material.Transparency > 0 || material.PigmentMayTransmit)
            return false;

        RgbSpectrum albedo = RgbSpectrum.FromReflectance(material.GetColorFor(hit)) * material.Diffuse;

        // Nothing at all, not next to nothing, for the reason Follow gives.
        if (Math.Max(albedo[0], Math.Max(albedo[1], albedo[2])) <= 0)
            return false;

        power *= albedo;

        (Vector across, Vector along) = Microfacets.FrameAround(hit.Normal);
        double reach = Math.Sqrt(random.NextDouble());
        double turn = 2 * Math.PI * random.NextDouble();

        onward = (across * (reach * Math.Cos(turn)) + along * (reach * Math.Sin(turn)) +
                  hit.Normal * Math.Sqrt(Math.Max(0, 1 - reach * reach))).Unit;

        return true;
    }

    /// <summary>
    /// This method finds the nearest thing a path of light meets, looking only a short way ahead at first
    /// and further only when there is nothing there.
    /// <para>
    /// **Light goes only a little way between strands**, and asking for everything a ray crosses would
    /// hand back every strand all the way through the hair -- hundreds of them, in dense fur, for each
    /// strand a path meets.  Looking a short way ahead first finds the next strand among the few in reach.
    /// </para>
    /// </summary>
    /// <param name="ray">The path's ray.</param>
    /// <param name="reach">How far ahead to look first.</param>
    /// <returns>The crossings found, and the nearest ahead, if there is one.</returns>
    private (List<Intersection> Hits, Intersection Nearest) NearestAlong(Ray ray, double reach)
    {
        for (int tries = 0; tries < 12; tries++, reach *= 4)
        {
            List<Intersection> hits = IntersectWithin(ray, reach);
            Intersection nearest = null;

            foreach (Intersection hit in hits)
            {
                if (hit.Distance > 0 && hit.Distance < reach && (nearest is null || hit.CompareTo(nearest) < 0))
                    nearest = hit;
            }

            if (nearest is not null)
                return (hits, nearest);
        }

        List<Intersection> all = Intersect(ray);

        return (all, all.FirstOrDefault(crossing => crossing.Distance > 0));
    }

    /// <summary>
    /// This method counts the places of the grid that the strands of a head of hair lie in.
    /// </summary>
    private static int PlacesHeldIn(Hair hair, Surface portal, HairLight grid)
    {
        bool[] held = new bool[grid.Size];
        int count = 0;

        hair.VisitPoints(point =>
        {
            int cell = grid.CellOf(hair.SurfaceToWorld(point, 0, portal));

            if (cell >= 0 && !held[cell])
            {
                held[cell] = true;
                count++;
            }
        });

        return count;
    }

    /// <summary>
    /// This method returns how long a strand of a head of hair is in the world, where it stands for the
    /// given instance.  A strand's length is given in the space the hair is grown in, and a rug written in
    /// inches and scaled into a room in meters is forty times shorter in the world than it says.
    /// </summary>
    private static double LengthInTheWorld(Hair hair, Surface portal)
    {
        Point origin = hair.SurfaceToWorld(Point.Zero, 0, portal);
        double across = (hair.SurfaceToWorld(new Point(1, 0, 0), 0, portal) - origin).Magnitude;
        double up = (hair.SurfaceToWorld(new Point(0, 1, 0), 0, portal) - origin).Magnitude;
        double deep = (hair.SurfaceToWorld(new Point(0, 0, 1), 0, portal) - origin).Magnitude;

        return hair.StrandLength * Math.Cbrt(across * up * deep);
    }

    /// <summary>
    /// This method returns the box a head of hair occupies in the world, where it stands for the given
    /// instance.
    /// </summary>
    private static (Point Low, Point High) BoxInTheWorld(Hair hair, Surface portal)
    {
        BoundingBox box = hair.BoundingBox;
        BoundingBox world = new ();

        for (int corner = 0; corner < 8; corner++)
        {
            Point local = new (
                (corner & 1) == 0 ? box.Minimum.X : box.Maximum.X, (corner & 2) == 0 ? box.Minimum.Y : box.Maximum.Y,
                (corner & 4) == 0 ? box.Minimum.Z : box.Maximum.Z);

            world.Add(hair.SurfaceToWorld(local, 0, portal));
        }

        return (world.Minimum, world.Maximum);
    }
}
