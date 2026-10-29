using System.Collections.Concurrent;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This part of the scene follows light forward from the lamps through mirrors and glass to where it lands
/// on something matte: the bright spot a glass ball gathers in its shadow, the patch of sun a mirror throws
/// on a wall, the ring of light in the bottom of a cup.
/// <para>
/// **That light cannot be found from the eye.**  A matte surface looks for its lamps along straight lines,
/// and the light a lens brought to a focus came along no straight line: it was bent on its way, at places
/// no ray from the surface knows to look through.  So it is followed from the other end -- from the lamps,
/// aimed at whatever can mirror or bend light, through as many turns as it takes -- and left as photons
/// where it lands (see <see cref="PhotonMap"/>), to be read back by whatever looks at that place.
/// </para>
/// <para>
/// **What turns light, and only that, is followed so.**  A photon that meets something matte first is
/// light the lamp's own shadow ray already counts, and is let go; one that has only passed straight through
/// clear things has gone where a shadow ray goes, and is not kept either.  And once caustics are asked for,
/// glass that bends light stops a shadow ray dead (see <see cref="StopsTheShadowRay"/>): the light that gets
/// through it is the photons', focused or spread as the glass bends it, and a straight line through it would
/// count it a second time, unbent.  A surface may say `no caustics`, and then it turns no photons and lets
/// shadow rays through as it always did -- which is exact for a flat pane, whose two faces bend light back
/// the way it came.
/// </para>
/// <para>
/// **A photon is shaded as a ray from the eye is**, in reverse: a mirror sends on what it mirrors, metal
/// tinted by its color; glass shares it by the same Fresnel share between what it mirrors and what it lets
/// through, bends it by Snell's law, tints it by its filter and fades it over the way through.  Which way
/// each photon goes at a fork is picked by chance, in proportion to the shares, and what it carries is
/// scaled so that the photons on each way add up to that way's share.
/// </para>
/// </summary>
public partial class Scene
{
    /// <summary>
    /// This property holds how many photons are sent from the lamps for the scene's caustics, or none for
    /// no caustics at all; see <see cref="RayTracer.General.RenderContext.CausticPhotons"/>.
    /// </summary>
    public int CausticPhotons { get; set; }

    /// <summary>
    /// This property holds whether the render the photons are sent for carries light band by band, in
    /// which case glass that spreads colors splits each photon that goes through it into one of them.
    /// </summary>
    public bool CausticsSpectral { get; set; }

    /// <summary>
    /// This property holds the photons of the scene's caustics, once they have been sent, or <c>null</c>
    /// where it has none.
    /// </summary>
    public PhotonMap Caustics => _caustics;

    private PhotonMap _caustics;

    // What turns light but could not be aimed at, having no edges -- a plane of water, an endless sea -- and so
    // was sent no photons: a shadow ray crossing it is let through as it would be without caustics, or what lay
    // beneath it would get no light at all.
    private HashSet<Surface> _unaimed;

    // Whether the scene's sky is a light, and so sends photons of its own.
    private bool _skyIsALight;

    // What the photons are aimed at, while they are being sent.
    private List<Target> _targets = [];

    // How many photons a place gathers its light from, how far it looks for them at most as a share of the
    // size of the things that cast them, and how many turns a photon is followed through.  A sun's photons
    // land in an even spread and eighty gave a smooth caustic; a sky's, an area light's or a rough mirror's,
    // set off by more than two numbers, land as if at random, and gathered from eighty, the light through a
    // frosted block went to grain.  Two hundred settles it and still keeps a ball's focus sharp.
    private const int NearestPhotons = 200;
    private const double FarthestShare = 0.25;
    private const int MostTurns = 24;

    // How many photons go to a block, the unit the work is shared among threads in; and at most how many
    // places photons are aimed at, things near one another being aimed at together beyond that.
    private const int PhotonsPerBlock = 4096;
    private const int MostTargets = 64;

    /// <summary>
    /// This record holds one place photons are aimed at: a ball round something that turns light, and how
    /// strongly the strongest thing in it does.
    /// </summary>
    private readonly record struct Target(Point Center, double Radius, double Strength);

    /// <summary>
    /// This record holds one beam of photons: those sent from a light at one target, each from its own
    /// place on the light.
    /// </summary>
    private sealed record PhotonBeam(Light Light, Target Target, int Aim, double Weight)
    {
        public int Photons { get; set; }

        public int FirstBlock { get; set; }
    }

    /// <summary>
    /// This method sends the scene's photons, if it asks for caustics, and keeps where they land.  It is
    /// done before the first pixel, the same way every time, however the work is shared among threads.
    /// </summary>
    public void ShootCausticPhotons()
    {
        _caustics = null;
        _unaimed = null;
        _skyIsALight = Lights.Any(light => light is SkyLight);

        if (CausticPhotons <= 0)
            return;

        // The table colors are turned into spectra by is built now, not by the first photon to split.
        if (CausticsSpectral)
            SpectralBasis.Shared.Prepare();

        List<Target> targets = CausticTargets();

        _targets = targets;

        _reach = ReachOfTheScene();
        List<PhotonBeam> beams = targets.Count == 0 ? [] : BeamsAt(targets);
        double total = beams.Sum(beam => beam.Weight);
        int blocks = 0;

        // The photons are shared among the beams by how much light each brings to what it is aimed at.
        foreach (PhotonBeam beam in beams)
        {
            beam.Photons = total > 0 ? (int) Math.Round(CausticPhotons * beam.Weight / total) : 0;
            beam.FirstBlock = blocks;
            blocks += (beam.Photons + PhotonsPerBlock - 1) / PhotonsPerBlock;
        }

        List<PhotonMap.Photon>[] landed = new List<PhotonMap.Photon>[blocks];
        PhotonBeam[] owners = new PhotonBeam[blocks];

        foreach (PhotonBeam beam in beams)
        {
            int count = (beam.Photons + PhotonsPerBlock - 1) / PhotonsPerBlock;

            for (int block = 0; block < count; block++)
                owners[beam.FirstBlock + block] = beam;
        }

        Parallel.For(0, blocks, new ParallelOptions { MaxDegreeOfParallelism = System.Environment.ProcessorCount },
            block =>
            {
                PhotonBeam beam = owners[block];
                int first = (block - beam.FirstBlock) * PhotonsPerBlock;
                int last = Math.Min(beam.Photons, first + PhotonsPerBlock);
                List<PhotonMap.Photon> here = [];
                // A pigment that looks about a place looks about in this scene, at the instant photons go at.
                (Scene, int) was = ShadeHere(0);

                for (int index = first; index < last; index++)
                    Send(beam, index, here);

                ShadeAgain(was);

                landed[block] = here;
            });

        double farthest = targets.Count == 0
            ? 0
            : FarthestShare * targets.Sum(target => target.Radius * target.Strength) /
              Math.Max(1e-12, targets.Sum(target => target.Strength));

        _caustics = new PhotonMap(landed.SelectMany(list => list), NearestPhotons, farthest);

        Terminal.Out(
            $"Caustics: {_caustics.Count:N0} photons landed of {beams.Sum(beam => beam.Photons):N0} sent at " +
            $"{targets.Count} {(targets.Count == 1 ? "target" : "targets")}.", OutputLevel.Chatty);
    }

    /// <summary>
    /// This method finds what photons should be aimed at: everything that turns light, as balls round it
    /// in the world -- things near one another taken together, where there are many.  Anything without
    /// edges, a plane mirror or an endless sea, cannot be aimed at and casts no caustics.
    /// </summary>
    private List<Target> CausticTargets()
    {
        List<(BoundingBox Box, double Strength)> found = [];

        _unaimed = new HashSet<Surface>(ReferenceEqualityComparer.Instance);

        Dictionary<Surface, bool> mayTurn = new (ReferenceEqualityComparer.Instance);

        foreach (Surface surface in Surfaces)
            CollectTargets(surface, Matrix.Identity, false, found, _unaimed, default, mayTurn);

        if (found.Count <= MostTargets)
            return found.Select(each => TargetOf(each.Box, each.Strength)).ToList();

        // Too many to aim at one by one: gathered by the cells of a grid over them all, coarser until few
        // enough cells hold any.
        BoundingBox all = new ();

        foreach ((BoundingBox box, _) in found)
            all.Add(box);

        Vector extent = all.Maximum - all.Minimum;
        double size = Math.Max(extent.X, Math.Max(extent.Y, extent.Z)) / 16;

        while (true)
        {
            SortedDictionary<(long, long, long), (BoundingBox Box, double Strength)> cells = new ();

            foreach ((BoundingBox box, double strength) in found)
            {
                Point middle = box.Minimum + (box.Maximum - box.Minimum) * 0.5;
                (long, long, long) key = (
                    (long) Math.Floor((middle.X - all.Minimum.X) / size),
                    (long) Math.Floor((middle.Y - all.Minimum.Y) / size),
                    (long) Math.Floor((middle.Z - all.Minimum.Z) / size));

                if (!cells.TryGetValue(key, out (BoundingBox Box, double Strength) cell))
                    cell = (new BoundingBox(), 0);

                cell.Box.Add(box);
                cells[key] = (cell.Box, Math.Max(cell.Strength, strength));
            }

            if (cells.Count <= MostTargets)
                return cells.Values.Select(cell => TargetOf(cell.Box, cell.Strength)).ToList();

            size *= 2;
        }
    }

    private static Target TargetOf(BoundingBox box, double strength)
    {
        Vector extent = box.Maximum - box.Minimum;

        return new Target(box.Minimum + extent * 0.5, extent.Magnitude / 2, strength);
    }

    /// <summary>
    /// This method collects the boxes in the world round the things a surface holds that turn light.  A
    /// solid built of others -- a union, a tube -- is taken whole, since what it turns light with is its
    /// whole shape.  A shared shape is walked once for each place it stands, with the instances it is
    /// standing in, since what it is dressed in may differ from place to place -- but only one that could hold
    /// something turning light (see <see cref="MayTurnLight"/>): a wood of trees sharing their twigs and leaves inside
    /// them is billions of places, none of them glass.
    /// </summary>
    private static void CollectTargets(
        Surface surface, Matrix outer, bool noCaustics, List<(BoundingBox Box, double Strength)> found,
        HashSet<Surface> unaimed, Portals portals, Dictionary<Surface, bool> mayTurn)
    {
        Matrix toWorld = outer * surface.Transform;

        noCaustics |= surface.NoCaustics;

        switch (surface)
        {
            case Instance instance:
                if (!noCaustics && MayTurnLight(instance.Prototype, mayTurn))
                {
                    CollectTargets(
                        instance.Prototype, toWorld, noCaustics, found, unaimed, Into(instance, portals), mayTurn);
                }

                return;
            case Group group:
                foreach (Surface child in group.Surfaces)
                    CollectTargets(child, toWorld, noCaustics, found, unaimed, portals, mayTurn);
                return;
        }

        if (noCaustics)
            return;

        double strength = StrengthWithin(surface, portals);
        BoundingBox box = surface.TrueBoundingBox();

        if (strength <= 0 || (box is not null && box.IsEmpty))
            return;

        BoundingBox world = box?.TransformedBy(toWorld);
        Vector extent = world is null ? null : world.Maximum - world.Minimum;

        if (extent is not null && double.IsFinite(extent.X) && double.IsFinite(extent.Y) && double.IsFinite(extent.Z))
            found.Add((world, strength));
        else
            unaimed.Add(surface);
    }

    /// <summary>
    /// This method returns how strongly the strongest thing within a surface turns light: what it mirrors,
    /// or what it lets through where it bends it.
    /// </summary>
    private static double StrengthWithin(Surface surface, Portals portals)
    {
        double strength = surface.NoCaustics ? 0 : StrengthOf(surface.MaterialThrough(portals) ?? Material.Default);

        return surface switch
        {
            CsgSurface csg => Math.Max(
                strength, Math.Max(StrengthWithin(csg.Left, portals), StrengthWithin(csg.Right, portals))),
            IAssembled { Root: not null } assembled => Math.Max(strength, StrengthWithin(assembled.Root, portals)),
            Instance instance => Math.Max(strength, StrengthWithin(instance.Prototype, Into(instance, portals))),
            // A string's glint is too fine a target to aim photons at.
            StrandSurface => 0,
            Group group => group.Surfaces.Aggregate(
                strength, (most, child) => Math.Max(most, StrengthWithin(child, portals))),
            _ => strength
        };
    }

    /// <summary>
    /// This method returns the instances a shape stands in once it is entered through one more, walking
    /// in from the world: the new one is the innermost.
    /// </summary>
    private static Portals Into(Surface instance, Portals portals)
    {
        return new Portals(instance, portals.IsEmpty ? null : new PortalChain(portals.Through, portals.Beyond));
    }

    /// <summary>
    /// This method reports whether anything in a shape could turn light wherever the shape stands: a part whose own
    /// material does, or a part left to be dressed by what holds the shape, which might be dressed in glass.  It is
    /// worked out once for each shape and remembered.
    /// </summary>
    private static bool MayTurnLight(Surface surface, Dictionary<Surface, bool> known)
    {
        if (known.TryGetValue(surface, out bool may))
            return may;

        may = !surface.NoCaustics && surface switch
        {
            StrandSurface => false,
            Instance instance => MayTurnLight(instance.Prototype, known),
            Group group => group.Surfaces.Any(child => MayTurnLight(child, known)),
            CsgSurface csg => surface.WearsTheDefault || TurnsLight(surface.Material ?? Material.Default) ||
                              MayTurnLight(csg.Left, known) || MayTurnLight(csg.Right, known),
            IAssembled { Root: not null } assembled => surface.WearsTheDefault ||
                                                      TurnsLight(surface.Material ?? Material.Default) ||
                                                      MayTurnLight(assembled.Root, known),
            _ => surface.WearsTheDefault || TurnsLight(surface.Material ?? Material.Default)
        };

        return known[surface] = may;
    }

    private static double StrengthOf(Material material)
    {
        if (!TurnsLight(material))
            return 0;

        bool transmits = material.Transparency > 0 || material.PigmentMayTransmit;

        // A metal turns what it mirrors square-on, on average over the colors, where it is bare.
        double metal = material.MetalApplies ? material.Metal.AverageAt(1, 1) * Math.Min(material.Metallic, 1) : 0;

        return Math.Max(
            Math.Max(
                Math.Max(Math.Max(material.Reflective, metal), material.Film is null ? 0 : 0.25),
                material.CoatApplies ? material.Coat.AverageReflectance(1) : 0),
            transmits && material.Interior.Refracts ? Math.Max(material.Transparency, 0.5) : 0);
    }

    /// <summary>
    /// This method reports whether a material turns light a photon is followed through: a mirror, smooth
    /// or rough; glass that bends what it lets through; or a film or a coat, which always mirror some of it.
    /// </summary>
    private static bool TurnsLight(Material material)
    {
        if (material.Film is not null || material.CoatApplies)
            return true;

        bool transmits = material.Transparency > 0 || material.PigmentMayTransmit;

        return material.Reflective > 0 || material.MetalApplies || (transmits && material.Interior.Refracts);
    }

    /// <summary>
    /// This method reports whether a surface met turns the photons that meet it, rather than letting them by
    /// as it lets a shadow ray by.
    /// </summary>
    private static bool TurnsPhotons(Intersection hit, Material material)
    {
        return !hit.Surface.NoCaustics && !hit.Portals.MakesNoCaustics && TurnsLight(material);
    }

    /// <summary>
    /// This method reports whether, with caustics asked for, a shadow ray crossing a surface stops there:
    /// it is glass that bends light, and the light through it is the photons'.
    /// </summary>
    /// <param name="intersection">The crossing.</param>
    /// <param name="material">The surface's material.</param>
    /// <returns><c>true</c>, if no light comes straight through.</returns>
    private bool StopsTheShadowRay(Intersection intersection, Material material)
    {
        if (_caustics is null || !material.Interior.Refracts || !TurnsPhotons(intersection, material))
            return false;

        for (Surface surface = intersection.Surface; surface is not null; surface = surface.Parent)
        {
            if (_unaimed?.Contains(surface) ?? false)
                return false;
        }

        return true;
    }

    /// <summary>
    /// This method aims the scene's lights at its targets, a beam from each light at each target, weighed by
    /// how much light it brings there -- reckoned over the light's samples, each of which stands for its share
    /// of the light.  The sky sends a beam as a sun from every direction would, and a glowing volume as a lamp
    /// at every place in it would.  The glowing surfaces send none: their light found in mirrors and glass is
    /// found by bounced light (see <see cref="EmissionLight"/>).
    /// </summary>
    private List<PhotonBeam> BeamsAt(List<Target> targets)
    {
        List<PhotonBeam> beams = [];

        foreach (Light light in Lights)
        {
            if (light is EmissionLight)
                continue;

            int count = light.SampleCount;

            for (int aim = 0; aim < targets.Count; aim++)
            {
                Target target = targets[aim];
                double weight = 0;

                for (int index = 0; index < count; index++)
                {
                    LightSample sample = light.SampleAround(target.Center, index);
                    Color color = light.ColorFor(sample);
                    double brightness = (color.Red + color.Green + color.Blue) / 3;
                    bool distant = double.IsPositiveInfinity(sample.Distance);

                    // How much of the light is aimed at the target: the most of it anywhere over its ball,
                    // since a spotlight's cone may take in only its edge.
                    double aimed = sample.Cone;

                    if (!distant)
                    {
                        foreach (Vector offset in Offsets)
                            aimed = Math.Max(aimed, light.SampleToward(target.Center + offset * target.Radius, index).Cone);
                    }

                    weight += Math.PI * brightness * aimed *
                              (distant ? 1 : light.FadingOver(Math.Max(sample.Distance - target.Radius, 0))) *
                              Math.PI * target.Radius * target.Radius * target.Strength / count;
                }

                if (weight > 0)
                    // By its place in the list: two targets alike in every number are still two, and found by
                    // their value both beams took the first's, so the light under the pair was counted twice.
                    beams.Add(new PhotonBeam(light, target, aim, weight));
            }
        }

        return beams;
    }

    /// <summary>
    /// This method returns which target a place belongs to: the first whose ball holds it, or -1 for none.
    /// </summary>
    private int OwnerOf(Point place)
    {
        for (int index = 0; index < _targets.Count; index++)
        {
            Target target = _targets[index];

            if ((place - target.Center).Magnitude <= target.Radius * (1 + 1e-9))
                return index;
        }

        return -1;
    }

    private static readonly Vector[] Offsets =
    [
        new (1, 0, 0), new (-1, 0, 0), new (0, 1, 0), new (0, -1, 0), new (0, 0, 1), new (0, 0, -1)
    ];

    /// <summary>
    /// This method sends one photon of a beam and follows it, keeping it wherever it lands on something
    /// matte having been turned on its way.
    /// <para>
    /// **It sets off from its own place on the light**, picked anywhere over it (see
    /// <see cref="Light.PhotonSample"/>), and in its own direction within what the target fills seen from
    /// there -- or, from a light with no place, from its own place across the target as seen from that
    /// direction.
    /// </para>
    /// <para>
    /// **What a photon carries is set where it first meets something it does not pass straight by**: the
    /// light the lamp gives there -- its irradiance, which is as this renderer's lamps say, with no falling
    /// off unless they fade -- times the photon's share of the beam's cross-section there.  From there it is
    /// the photon's to lose.
    /// </para>
    /// </summary>
    private void Send(PhotonBeam beam, int index, List<PhotonMap.Photon> landed)
    {
        double first = Emission(beam, index, 0);
        double around = 2 * Math.PI * Emission(beam, index, 1);
        Emitted setOff = new (Emission(beam, index, 2), Emission(beam, index, 3), Emission(beam, index, 4), null);
        LightSample sample = beam.Light.PhotonSample(beam.Target.Center, setOff.First, setOff.Second, setOff.Third);
        Ray ray;
        double share;
        RgbSpectrum carried = RgbSpectrum.White * Math.PI;

        if (double.IsPositiveInfinity(sample.Distance))
        {
            // A light with no place: an even spread of places over the disc the target's ball shows it, set
            // off from beyond everything in the scene, carrying the light along the photon's own direction.
            Vector axis = -sample.Direction;
            (Vector across, Vector along) = Microfacets.FrameAround(axis);
            double radius = beam.Target.Radius * Math.Sqrt(first);
            Point onDisc = beam.Target.Center + across * (radius * Math.Cos(around)) +
                           along * (radius * Math.Sin(around));

            ray = new Ray(onDisc - axis * (beam.Target.Radius + _reach), axis);
            carried *= RgbSpectrum.From(beam.Light.ColorFor(sample)) *
                       (sample.Cone * Math.PI * beam.Target.Radius * beam.Target.Radius / beam.Photons);
            share = 0;
        }
        else
        {
            // A light with a place: an even spread of directions over the cone the target's ball fills.
            Point origin = beam.Target.Center + sample.Direction * sample.Distance;
            Vector axis = -sample.Direction;
            (Vector across, Vector along) = Microfacets.FrameAround(axis);
            double ratio = beam.Target.Radius / sample.Distance;
            double limit = ratio >= 1 ? -1 : Math.Sqrt(1 - ratio * ratio);
            double cosine = 1 - first * (1 - limit);
            double sine = Math.Sqrt(Math.Max(0, 1 - cosine * cosine));
            Vector direction = (axis * cosine + across * (sine * Math.Cos(around)) +
                                along * (sine * Math.Sin(around))).Unit;

            ray = new Ray(origin, direction);
            share = 2 * Math.PI * (1 - limit) / beam.Photons;
            setOff = setOff with { Origin = origin };
        }

        Follow(beam, index, setOff, Hue.Every, ray, carried, share, 0, 0, 1, landed);
    }

    /// <summary>
    /// This record holds which colors a photon carries: every one, as a color, or -- once glass that spreads
    /// colors has split it -- one band alone, at a wavelength within it, the light it carries in that band
    /// being every channel of its color alike.
    /// </summary>
    private readonly record struct Hue(int Band, double Wavelength)
    {
        public static readonly Hue Every = new (-1, double.NaN);

        public bool IsEvery => Band < 0;

        /// <summary>
        /// This method returns what a share of light, as a color, comes to for a photon of this hue: the
        /// share itself, or its share of the one band.
        /// </summary>
        public RgbSpectrum Of(RgbSpectrum share) =>
            IsEvery ? share : RgbSpectrum.White * BandSpectrum.FromReflectance(share.ToColor())[Band];
    }

    /// <summary>
    /// This record holds where on its light a photon set off from: the numbers that picked the place, so that
    /// it can be asked about again from where the photon first meets something, and the place itself.
    /// </summary>
    private readonly record struct Emitted(double First, double Second, double Third, Point Origin);

    /// <summary>
    /// This method returns what is left of a photon's light over a way through the insides of something:
    /// its fade, and what any medium filling it takes out.
    /// </summary>
    private static RgbSpectrum Across(
        Interior interior, Surface container, Portals portals, Ray ray, double distance, Hue hue)
    {
        RgbSpectrum left = RgbSpectrum.White * interior.GetFadeOver(distance);

        return interior.Medium is null ? left : left * Across(interior.Medium, container, portals, ray, distance, hue);
    }

    /// <summary>
    /// This method returns what is left of a photon's light over a way through a medium filling a shape, walked as a
    /// shadow ray walks it where its density varies: charged at its even density, a photon crossed a wisp of smoke
    /// as though the smoke filled the whole of its shape.
    /// </summary>
    private static RgbSpectrum Across(
        Medium medium, Surface container, Portals portals, Ray ray, double distance, Hue hue)
    {
        if (!medium.HasShape || container is null)
            return Across(medium, distance, hue);

        const int count = 16;
        double step = distance / count;
        RgbSpectrum depth = RgbSpectrum.Black;
        double bandDepth = 0;

        for (int index = 0; index < count; index++)
        {
            Point at = ray.Origin + ray.Direction * ((index + 0.5) * step);
            double density = medium.DensityAt(container.WorldToSurface(at, ray.TimeIndex, portals));

            if (density <= 0)
                continue;

            if (hue.IsEvery)
                depth += medium.ExtinctionAt<RgbSpectrum>(density) * step;
            else
                bandDepth += medium.ExtinctionAt<BandSpectrum>(density)[hue.Band] * step;
        }

        return hue.IsEvery ? Transmitted(depth, 1) : RgbSpectrum.White * Math.Exp(-bandDepth);
    }

    /// <summary>
    /// This method returns what is left of a photon's light over a way through a medium, in the colors it
    /// carries.
    /// </summary>
    private static RgbSpectrum Across(Medium medium, double distance, Hue hue)
    {
        return hue.IsEvery
            ? medium.GetTransmittanceOver<RgbSpectrum>(distance)
            : RgbSpectrum.White * medium.GetTransmittanceOver<BandSpectrum>(distance)[hue.Band];
    }

    /// <summary>
    /// This method returns one of the numbers a photon sets off by: where across its target, then where on
    /// its light.
    /// </summary>
    private static double Emission(PhotonBeam beam, int index, int which) => Pick(beam, index, -1, which, 1);

    /// <summary>
    /// This method follows a photon on from where it is, keeping it wherever it lands on something matte
    /// having been turned on its way.
    /// <para>
    /// **Where a surface both mirrors and lets light through, the photon goes on both ways** -- the way most
    /// of the light goes always, carrying that share, and the other way by chance, as a photon of its own
    /// carrying its share over the chance of being followed.  So a pane, which mirrors a twenty-fifth of
    /// the light at each face, lets every photon through carrying the rest, and only now and then follows
    /// what it mirrored; picking one way or the other for each photon made the light through it grainy.
    /// </para>
    /// </summary>
    /// <param name="beam">The beam the photon was sent in.</param>
    /// <param name="index">Which photon of the beam it is.</param>
    /// <param name="setOff">Where on its light it set off from.</param>
    /// <param name="hue">Which colors it carries.</param>
    /// <param name="ray">Where it is and which way it is going.</param>
    /// <param name="carried">What it carries: pi, until it first meets something.</param>
    /// <param name="share">Its share of the beam's cross-section, until it first meets something; nought
    /// after.</param>
    /// <param name="turns">How many times it has been turned.</param>
    /// <param name="step">How many surfaces it has met.</param>
    /// <param name="path">Which of the ways it might have gone it is on, for its chances.</param>
    /// <param name="landed">Where to keep it.</param>
    private void Follow(
        PhotonBeam beam, int index, Emitted setOff, Hue hue, Ray ray, RgbSpectrum carried, double share, int turns,
        int step, ulong path, List<PhotonMap.Photon> landed)
    {
        for (; step < MostTurns; step++)
        {
            List<Intersection> hits = Intersect(ray);

            hits.Sort();

            Intersection hit = FirstMetBy(hits, beam.Light);

            if (hit is null)
                return;

            hit.PrepareUsing(ray, hits, Environment.IndexOfRefraction, hue.Wavelength);

            Material material = hit.SurfaceMaterial;
            bool turnsHere = TurnsPhotons(hit, material);

            // **A photon is the light of the target it was aimed at, and of no other.**  Beams aimed at things
            // that overlap, seen from the light, both carry the light that reaches either -- the beam at a pane
            // went on through the glass below it, and the beam at that glass came through the pane -- and a
            // floor beneath both took their light twice.  So the first thing a photon turns at must be its own
            // target's: the first of the targets, in their order, whose ball holds that place.  Every way the
            // light can go through a target's ball is in that target's beam, so each is counted once.
            if (turnsHere && turns == 0 && OwnerOf(hit.Point) != beam.Aim)
                return;

            // The photon's light is set where it first meets anything it does not pass straight by: the
            // light's there, its share of it.  A light with a place is asked from there, as anything it lights
            // would ask it -- how much of a spotlight's cone points that way, and, for a glowing volume, what
            // its place gives that far off.  Not at what it passes straight by, which only dims it, as it dims
            // a shadow ray: a glow's own place is often just outside the ball it fills, which its light is
            // passed straight through, and set there, a hair from the glow, it came to nothing.
            if (share > 0 && (turnsHere || !LetsLightBy(material)))
            {
                LightSample there = beam.Light.PhotonSample(hit.Point, setOff.First, setOff.Second, setOff.Third);
                double distance = (hit.Point - setOff.Origin).Magnitude;

                carried *= RgbSpectrum.From(beam.Light.ColorFor(there)) *
                           (there.Cone * beam.Light.FadingOver(distance) * distance * distance * share);

                share = 0;

                if (carried.Sum <= 0)
                    return;
            }

            // The way here is charged as an eye ray's is: going through something that fades light or is
            // filled with a medium charges for the way through it, whether the photon is leaving it or has met
            // something standing inside it -- a pool's floor is lit through the water above it, as it is seen
            // through it -- and anything else crossed the surroundings.
            if (hit.Inside)
                carried *= Across(material.Interior, hit.Surface, hit.Portals, ray, hit.Distance, hue);
            else if (HasInsides(hit.InteriorBefore))
            {
                carried *= Across(
                    hit.InteriorBefore, hit.ContainerBefore, hit.ContainerPortals,
                    ray, hit.Distance, hue);
            }
            else if (Environment.Medium is not null)
                carried *= Across(Environment.Medium, hit.Distance, hue);

            // What meets matte light before anything turned it is the lamp's own, counted by its shadow rays.
            if (turns == 0 && !turnsHere && !LetsLightBy(material))
                return;

            // Where a turned photon lands on something matte, it is kept: not on bare metal, which has nothing of
            // its own for it to light.
            if (turns > 0 && material.Diffuse > 0 && material.Transparency < 1 && !material.IsBareMetal)
            {
                landed.Add(PhotonMap.Photon.At(
                    hit.Point, carried.ToColor(), ray.Direction, hit.Normal,
                    hue.IsEvery ? PhotonMap.Photon.EveryBand : (byte) hue.Band));
            }

            if (!turnsHere)
            {
                // Anything else lets the photon by as it lets a shadow ray by, straight on.
                carried *= hue.Of(PassedStraight(hit, material, ray));

                if (carried.Sum <= 0)
                    return;

                ray = new Ray(hit.Inside ? hit.OverPoint : hit.UnderPoint, ray.Direction);

                continue;
            }

            (Ray mirrorRay, RgbSpectrum mirrored, Ray passRay, RgbSpectrum passed) = Turned(
                hit, material, Pick(beam, index, step, 1, path), Pick(beam, index, step, 2, path),
                Pick(beam, index, step, 3, path), Pick(beam, index, step, 4, path));

            mirrored = hue.Of(mirrored);
            passed = hue.Of(passed);

            double mirrorWeight = Average(mirrored);
            double passWeight = Average(passed);

            // Glass that spreads colors, in a render that carries them band by band, splits what it lets
            // through: a photon of every color going on through it goes on as one band.
            bool splits = CausticsSpectral && hue.IsEvery && hit.Disperses && passRay is not null;

            if (mirrorWeight <= 0 && passWeight <= 0)
                return;

            // What a surface lets through without bending it has not been turned, any more than a shadow ray
            // is: a bubble with nothing but air inside, or a sheet.
            int passTurns = material.Interior.Refracts ? 1 : 0;
            bool mirrorMost = mirrorWeight >= passWeight;
            (Ray most, RgbSpectrum mostShare, int mostTurns, Ray less, RgbSpectrum lessShare, int lessTurns,
                double lessWeight, double mostWeight) = mirrorMost
                    ? (mirrorRay, mirrored, 1, passRay, passed, passTurns, passWeight, mirrorWeight)
                    : (passRay, passed, passTurns, mirrorRay, mirrored, 1, mirrorWeight, passWeight);

            // The lesser way is followed by chance, as a photon of its own, with the odds of its share against
            // the most way's -- so that it carries just what a photon going the most way does.  Photons all
            // alike are what a gather wants: followed more often carrying less, the faint ones crowded out the
            // bright among the nearest, and a pane's light went to grain.
            if (lessWeight > 0)
            {
                double chance = Math.Min(1, lessWeight / mostWeight);

                if (Pick(beam, index, step, 0, path) < chance)
                {
                    Hue lessHue = hue;
                    RgbSpectrum lessCarried = carried * lessShare * (1 / chance);

                    if (splits && mirrorMost)
                    {
                        (less, lessHue, lessCarried) = SplitThrough(
                            hit, ray, hits, material, lessCarried, beam, index, step, path * 2 + 1);
                    }

                    if (less is not null)
                    {
                        Follow(
                            beam, index, setOff, lessHue, less, lessCarried, 0, turns + lessTurns, step + 1,
                            path * 2 + 1, landed);
                    }
                }
            }

            carried *= mostShare;

            if (splits && !mirrorMost)
            {
                (most, hue, carried) = SplitThrough(hit, ray, hits, material, carried, beam, index, step, path * 2);

                if (most is null)
                    return;
            }

            ray = most;
            path *= 2;
            turns += mostTurns;
        }
    }

    /// <summary>
    /// This method splits a photon of every color, going on through glass that spreads colors, into one of
    /// them: a band picked in an even spread over the photons, and a wavelength somewhere within it, as the
    /// eye's rays are bent at (see SplitThrough).  It carries that band's share of its light, as a spectrum,
    /// times the number of bands, standing for them all as one band in so many; and it is bent through the
    /// surface again, by the glass's index at its own wavelength.
    /// </summary>
    /// <returns>The way on, the photon's hue and what it carries; no way where its band is turned back
    /// entirely.</returns>
    private (Ray Next, Hue Hue, RgbSpectrum Carried) SplitThrough(
        Intersection hit, Ray incoming, List<Intersection> hits, Material material, RgbSpectrum carried,
        PhotonBeam beam, int index, int step, ulong path)
    {
        int bands = SpectralColor.Bands;
        int band = Math.Min(bands - 1, (int) (Pick(beam, index, step, 5, path) * bands));
        double width = (SpectralColor.LongestWavelength - SpectralColor.ShortestWavelength) / bands;
        double wavelength = SpectralColor.WavelengthOf(band) + (Pick(beam, index, step, 6, path) - 0.5) * width;
        double light = BandSpectrum.FromIlluminant(carried.ToColor())[band] * bands;
        hit.PrepareUsing(incoming, hits, Environment.IndexOfRefraction, wavelength);

        Vector through = material.IsRough
            ? Bend(
                hit.Normal, hit.Eye, material.Roughness * material.Roughness, hit.N1, hit.N2,
                Pick(beam, index, step, 3, path), Pick(beam, index, step, 4, path)).Direction
            : Bent(hit, hit.N1, hit.N2);

        return through is null
            ? (null, Hue.Every, RgbSpectrum.Black)
            : (RefractedRay(hit, through), new Hue(band, wavelength), RgbSpectrum.White * light);
    }

    // How far back beyond the target a distant light's photons set off from: beyond everything that has
    // edges, worked out once for each sending.
    private double _reach;

    private double ReachOfTheScene()
    {
        BoundingBox all = new ();

        foreach (Surface surface in Surfaces)
        {
            BoundingBox box = Surface.BoxAround(surface, true);
            Vector extent = box is null || box.IsEmpty ? null : box.Maximum - box.Minimum;

            if (extent is not null && double.IsFinite(extent.X) && double.IsFinite(extent.Y) &&
                double.IsFinite(extent.Z))
                all.Add(box);
        }

        if (all.IsEmpty)
            return 1;

        return (all.Maximum - all.Minimum).Magnitude + (all.Minimum - Point.Zero).Magnitude +
               (all.Maximum - Point.Zero).Magnitude + 1;
    }

    /// <summary>
    /// This method returns the first thing a photon from a light meets that it cannot simply pass: what
    /// casts no shadow, and the bulb round the light it came from, it goes straight through, as the light's
    /// shadow rays do.
    /// </summary>
    private static Intersection FirstMetBy(List<Intersection> hits, Light light)
    {
        foreach (Intersection hit in hits)
        {
            if (hit.Distance < 0 || hit is HairIntersection fiber && !IsAWire(fiber))
                continue;

            if (hit.Surface.NoShadow || hit.Portals.CastsNoShadow)
                continue;

            if (light.Housing is { } housing && housing.Contains((hit.Surface, hit.Portals)))
                continue;

            return hit;
        }

        return null;
    }

    private static bool LetsLightBy(Material material)
    {
        return material.Transparency > 0 || material.PigmentMayTransmit;
    }

    /// <summary>
    /// This method works out where a surface that mirrors or bends light sends a photon, and what share
    /// goes each way, exactly as a ray from the eye would share its light there: a smooth surface by its
    /// mirror image and Snell's law, a film by what it mirrors and lets on of each color, and a rough one
    /// through a facet picked as the eye's rays pick them -- one seen from where the photon comes from --
    /// carrying what that facet mirrors or lets through and what of the way on its neighbors leave
    /// unhidden.
    /// </summary>
    /// <param name="hit">Where the photon met the surface.</param>
    /// <param name="material">The surface's material.</param>
    /// <param name="first">The first of two numbers picking a facet to mirror off.</param>
    /// <param name="second">The second.</param>
    /// <param name="third">The first of two picking one to go through.</param>
    /// <param name="fourth">The second.</param>
    /// <returns>The way it is mirrored and the share mirrored, and the way it is let through and the share
    /// let through; a way no light goes is black.</returns>
    private static (Ray Mirrored, RgbSpectrum MirrorShare, Ray Passed, RgbSpectrum PassShare) Turned(
        Intersection hit, Material material, double first, double second, double third, double fourth)
    {
        Color pigment = material.GetColorFor(hit);
        double transparency = material.PigmentMayTransmit ? material.TransparencyFor(pigment) : material.Transparency;
        bool transmits = material.Transparency > 0 || material.PigmentMayTransmit;
        double cosine = hit.Eye.Dot(hit.Normal);
        Ray mirrorRay = null;
        Ray passRay = null;
        RgbSpectrum mirrored = RgbSpectrum.Black;
        RgbSpectrum passed = RgbSpectrum.Black;

        // Bare metal mirrors each color by its own share; under a film, the film's shares say it all.
        BareMetal? metal = material.Film is null ? material.BareMetalAt(hit) : null;

        if (material.Film?.SharesAt<RgbSpectrum>(hit, material.MetalApplies ? material.Metal : null) is { } film)
        {
            // A film mirrors and lets on its shares of each color, as GetHitColor has it, whatever its
            // reflective says.
            Vector bent = transparency > 0 ? Bent(hit, hit.N1, hit.N2) : null;

            mirrorRay = new Ray(hit.MirrorPoint, hit.Reflect);
            mirrored = film.Mirrored;

            if (bent is not null)
            {
                passRay = RefractedRay(hit, bent);
                passed = film.Passed * transparency;
            }
        }
        else if (material.IsRough)
        {
            // A rough surface, as GetRoughReflection and GetRoughRefraction have it: each way through a
            // facet of its own, and no share between them but the facets' own.
            double alpha = material.Roughness * material.Roughness;

            // A brushed surface's facets, in its own frame, along its grooves and across them; see Facets.
            Facets facets = Facets.At(material, hit.Surface, hit.Point, hit.Normal, 0, hit.Portals, hit as HairIntersection);
            (Vector across, Vector along) = facets is null
                ? Microfacets.FrameAround(hit.Normal)
                : (facets.Along, facets.Across);
            Vector eye = new (hit.Eye.Dot(across), hit.Eye.Dot(along), hit.Eye.Dot(hit.Normal));

            if ((material.Reflective > 0 || metal is not null) && eye.Z > 0)
            {
                Vector facet = facets?.SampleVisibleNormal(eye, first, second) ??
                               Microfacets.SampleVisibleNormal(eye, alpha, first, second);
                double cosFacet = eye.Dot(facet);
                Vector away = facet * (2 * cosFacet) - eye;

                if (away.Z > 0)
                {
                    double reflectance = transmits
                        ? material.Reflective * Microfacets.GlassReflectance(cosFacet, hit.N1, hit.N2)
                        : material.ReflectanceAt(cosFacet);
                    double masking = facets?.Masking(away) ?? Microfacets.Masking(away.Z, alpha);

                    mirrorRay = new Ray(hit.MirrorPoint, (across * away.X + along * away.Y + hit.Normal * away.Z).Unit);
                    mirrored = metal is { } bare
                        ? bare.Mirrors<RgbSpectrum>(cosFacet, reflectance) * masking
                        : RgbSpectrum.White * (reflectance * masking);
                }
            }

            if (transparency > 0)
            {
                (Vector through, double weight) = Bend(hit.Normal, hit.Eye, alpha, hit.N1, hit.N2, third, fourth);

                if (through is not null)
                {
                    passRay = RefractedRay(hit, through);
                    passed = RgbSpectrum.White * (weight * transparency);
                }
            }
        }
        else
        {
            // A smooth one: what it mirrors, as GetReflectionColor has it, and what it lets through, bent, as
            // GetRefractedColor has it -- glass sharing what reaches it between the two by one Fresnel share.
            Vector bent = transparency > 0 ? Bent(hit, hit.N1, hit.N2) : null;

            if (material.Reflective > 0 || metal is not null)
            {
                mirrorRay = new Ray(hit.MirrorPoint, hit.Reflect);
                mirrored = metal is { } bare
                    ? bare.Mirrors<RgbSpectrum>(cosine, material.ReflectanceAt(cosine))
                    : RgbSpectrum.White * material.ReflectanceAt(cosine);
            }

            if (bent is not null)
            {
                passRay = RefractedRay(hit, bent);
                passed = RgbSpectrum.White * transparency;
            }

            if (material.Reflective > 0 && transmits)
            {
                double reflectance = hit.Reflectance;

                mirrored *= reflectance;
                passed *= 1 - reflectance;
            }
        }

        // A metal tints what it mirrors, and the filter what is let through, at every surface.
        if (material.Metallic != 0 && material.Metal is null && mirrored.Sum > 0)
            mirrored *= material.GetMetallicTint(RgbSpectrum.From(pigment), cosine);

        // A coat mirrors by its own index over whatever lies beneath it, and what lies beneath mirrors
        // through it, crossing it both ways.  A smooth coat over a smooth mirror mirrors the same way, and
        // the two shares go together; otherwise one way or the other goes on, picked by chance in
        // proportion to what each gives back, so that either carries what a photon should.  A coat is only
        // ever on something opaque, so the number glass would bend by is free to pick with.
        if (material.CoatApplies && !hit.Inside)
        {
            Coat coat = material.Coat;
            (Ray coatRay, double coatShare) = CoatTurned(hit, coat, first, second);

            if (mirrorRay is not null)
                mirrored *= coat.Through<RgbSpectrum>(cosine, cosine, hit.N1);

            double beneath = mirrorRay is null ? 0 : Average(mirrored);

            if (coatRay is not null && coatShare > 0)
            {
                if (beneath <= 0)
                {
                    mirrorRay = coatRay;
                    mirrored = RgbSpectrum.White * coatShare;
                }
                else if (!coat.IsRough && !material.IsRough)
                    mirrored += RgbSpectrum.White * coatShare;
                else
                {
                    double odds = coatShare / (coatShare + beneath);

                    if (third < odds)
                    {
                        mirrorRay = coatRay;
                        mirrored = RgbSpectrum.White * (coatShare / odds);
                    }
                    else
                        mirrored *= 1 / (1 - odds);
                }
            }
        }

        if (passed.Sum > 0 && material.Interior.Filter > 0)
            passed *= material.Interior.GetFilterTint(RgbSpectrum.From(pigment));

        return (mirrorRay, mirrored, passRay, passed);
    }

    /// <summary>
    /// This method works out which way a coat mirrors a photon, and how much of it: the mirror direction for
    /// a smooth coat, or off a facet it sees for a rough one, as the coat's own reflection looks.
    /// </summary>
    private static (Ray Mirrored, double Share) CoatTurned(Intersection hit, Coat coat, double first, double second)
    {
        if (!coat.IsRough)
            return (new Ray(hit.MirrorPoint, hit.Reflect), coat.ReflectanceAt(hit.Eye.Dot(hit.Normal), hit.N1));

        double alpha = coat.Alpha;
        (Vector across, Vector along) = Microfacets.FrameAround(hit.Normal);
        Vector eye = new (hit.Eye.Dot(across), hit.Eye.Dot(along), hit.Eye.Dot(hit.Normal));

        if (eye.Z <= 0)
            return (null, 0);

        Vector facet = Microfacets.SampleVisibleNormal(eye, alpha, first, second);
        double cosFacet = eye.Dot(facet);
        Vector away = facet * (2 * cosFacet) - eye;

        if (away.Z <= 0)
            return (null, 0);

        return (
            new Ray(hit.MirrorPoint, (across * away.X + along * away.Y + hit.Normal * away.Z).Unit),
            coat.ReflectanceAt(cosFacet, hit.N1) * Microfacets.Masking(away.Z, alpha));
    }

    // A photon's light is tested against nought exactly, never IsBlack: each carries a share of the light
    // that shrinks with the number of photons, and IsBlack's allowance took a glow's photons for nothing.
    private static double Average(RgbSpectrum light)
    {
        Color color = light.ToColor();

        return (color.Red + color.Green + color.Blue) / 3;
    }

    /// <summary>
    /// This method works out how much of a photon a surface that does not turn it lets straight by, exactly
    /// as it lets a shadow ray by.
    /// </summary>
    private RgbSpectrum PassedStraight(Intersection hit, Material material, Ray ray)
    {
        Interior interior = material.Interior;
        bool needsPigment = interior.Filter > 0 || material.PigmentMayTransmit;
        Color surfaceColor = needsPigment ? material.GetColorFor(hit) : null;
        double transparency = surfaceColor is null ? material.Transparency : material.TransparencyFor(surfaceColor);

        if (transparency <= 0)
            return RgbSpectrum.Black;

        RgbSpectrum reaching = RgbSpectrum.White * transparency;

        if (interior.Filter > 0)
            reaching *= interior.GetFilterTint(RgbSpectrum.From(surfaceColor));

        if (interior.Refracts && material.Film is null)
            reaching *= 1 - interior.GetReflectanceAt(ray.Direction.Dot(hit.Normal), Environment.IndexOfRefraction);

        if (material.Film is { } film)
        {
            double thickness = film.ThicknessAt(film.Pattern is null
                ? null
                : hit.Surface.WorldToSurface(hit.Point, hit.TimeIndex, hit.Portals));

            reaching *= film.SharesAt<RgbSpectrum>(
                thickness, Math.Abs(ray.Direction.Dot(hit.Normal)), Environment.IndexOfRefraction,
                interior.IndexOfRefraction).Passed;
        }

        return reaching;
    }

    /// <summary>
    /// This method returns the light a surface point takes from the caustics landed round it, as it takes
    /// a lamp's: its color and diffuse share of it, brilliance and grain having their say photon by photon.
    /// </summary>
    private TS CausticsAt<TS>(Intersection intersection, Material material, double ownShare, FilmShares<TS>? film)
        where TS : struct, ISpectrum<TS>
    {
        double grain = material.GrainAt(intersection.LitPoint);
        double brilliance = material.Brilliance;
        double diffuse = material.Diffuse;

        // What a surface takes of light arriving at a slant, as a share of what that light would give it
        // square-on: Phong's diffuse term over the cosine the light's irradiance already holds.
        double Taking(double cosine)
        {
            if (cosine <= 0)
                return 0;

            double slant = Math.Max(cosine, 1e-3);
            double intensity = brilliance == 1 ? slant : Math.Pow(slant, brilliance);

            return Math.Max(0, intensity * diffuse - grain) / slant;
        }

        // Photons split into their colors bring their light band by band, in a render that carries it so.
        bool banded = TS.IsSpectral && _caustics.HasBands;
        Span<double> bands = banded ? stackalloc double[SpectralColor.Bands] : default;
        Color irradiance = _caustics.Gather(
            intersection.Point, intersection.Normal, intersection.Surface.IsASheet, Taking,
            intersection.FootprintRadius, bands);
        TS light = TS.FromIlluminant(irradiance * (1 / Math.PI));

        if (banded)
        {
            for (int band = 0; band < bands.Length; band++)
                bands[band] /= Math.PI;

            light += TS.FromSampled(bands);
        }

        if (light.Sum <= 0)
            return TS.Black;

        TS color = TS.FromReflectance(material.GetColorFor(intersection)) * light;

        if (ownShare != 1)
            color *= ownShare;

        if (film is { } passing)
            color *= passing.Passed;

        return color;
    }

    private static double Fraction(double value) => value - Math.Floor(value);

    // The bases of the sequences a photon's choices are drawn from, one to each choice: where it sets off
    // from, and at each of the first surfaces it meets, which way it goes and through which facets.
    private static readonly int[] Bases =
    [
        2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97, 101, 103,
        107, 109, 113, 127, 131, 137, 139, 149, 151, 157, 163, 167, 173
    ];

    private const int ChoicesPerSurface = 7;
    private const int Emissions = 5;

    /// <summary>
    /// This method returns a number between nought and one for one of a photon's choices -- which way it
    /// goes at a surface, or which facets it goes by.
    /// <para>
    /// **Drawn from a sequence, not at random**, at least at the first few surfaces: the photons of a beam
    /// take each choice in an even spread over the beam, each choice its own sequence, so that those a pane
    /// mirrors, say, are a twentieth of the beam spread evenly across it rather than a twentieth picked by
    /// chance.  Picked by chance, they lay unevenly where they landed, and a film's colors and a rough
    /// mirror's spread went to grain.  The spread is turned by a fixed amount for each beam, choice and way
    /// taken, so that no two line up; past the first few surfaces, the choices are left to chance.
    /// </para>
    /// </summary>
    /// <param name="beam">The beam the photon was sent in.</param>
    /// <param name="index">Which photon of the beam it is.</param>
    /// <param name="step">How many surfaces it has met, or -1 for where it sets off from.</param>
    /// <param name="slot">Which of the choices at this surface.</param>
    /// <param name="path">Which of the ways it might have gone it is on.</param>
    private static double Pick(PhotonBeam beam, int index, int step, int slot, ulong path)
    {
        int dimension = step < 0 ? slot : Emissions + step * ChoicesPerSurface + slot;
        ulong seed = (ulong) beam.FirstBlock;

        if (dimension < Bases.Length)
        {
            return Fraction(
                ScrambledInverse((uint) index, dimension) + Hash(seed, (ulong) (100 + dimension)) +
                Hash(path, (ulong) dimension));
        }

        return Hash((seed << 40) ^ ((ulong) index << 8) ^ (ulong) step, path * 8 + (ulong) slot);
    }

    // Each sequence's digits shuffled by a fixed shuffle of its own: the sequences of the larger bases run
    // in step with one another for their first few hundred numbers, which lined a rough mirror's facets up
    // with where its photons came from, and shuffling each one's digits its own way breaks that.
    private static readonly int[][] Shuffles = Bases.Select((radix, dimension) =>
    {
        int[] digits = Enumerable.Range(0, radix).ToArray();

        for (int last = radix - 1; last > 0; last--)
        {
            int other = (int) (Hash((ulong) dimension, (ulong) last + 1000) * (last + 1));

            (digits[last], digits[other]) = (digits[other], digits[last]);
        }

        return digits;
    }).ToArray();

    private static double ScrambledInverse(uint index, int dimension)
    {
        int radix = Bases[dimension];
        int[] shuffle = Shuffles[dimension];
        double result = 0;
        double scale = 1.0 / radix;

        // Every digit a 32-bit index can have, the leading noughts shuffled too.
        for (double reach = 1; reach < 4294967296.0; reach *= radix)
        {
            result += shuffle[index % radix] * scale;
            index /= (uint) radix;
            scale /= radix;
        }

        return result;
    }

    /// <summary>
    /// This method returns a fraction between nought and one fixed by two numbers, as a die rolled by them.
    /// </summary>
    private static double Hash(ulong value, ulong salt)
    {
        ulong mixed = value * 0x9E3779B97F4A7C15UL + salt * 0xBF58476D1CE4E5B9UL;

        mixed ^= mixed >> 30;
        mixed *= 0xBF58476D1CE4E5B9UL;
        mixed ^= mixed >> 27;
        mixed *= 0x94D049BB133111EBUL;
        mixed ^= mixed >> 31;

        return (mixed >> 11) * (1.0 / 9007199254740992.0);
    }
}
