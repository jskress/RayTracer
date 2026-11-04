using System.Collections.Concurrent;
using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class holds a scene's many lights in a tree of nested boxes, so that a place being shaded may look at a few
/// of them, picked by how much each could give it, rather than at every one: the light tree of Conty and Kulla (2017)
/// with the adaptive splitting of Estevez and Kulla (2018), as pbrt-v4, Cycles and Arnold have it.
/// <para>
/// **It holds only lights that thin with distance**: point lights, spotlights and area lights that say how far they
/// <c>fade</c> over.  A light that does not fade gives as much a kilometer off as at arm's length, so there is no
/// telling it from its neighbours by distance, and it is looked at always, as every distant light and sky, every glow
/// and the glowing surfaces are -- those are lights a ray can meet, whose light is shared with what a surface mirrors,
/// and picking among them would change the shares.
/// </para>
/// <para>
/// **Every node knows how much its lights could give a place**: their power, the box they stand in, how far they keep
/// their strength before thinning and how fast, and the cone of the ways they shine.  Its importance at a place is
/// the power, times what is left of it over the nearest the box could be (never nearer than the box is wide), times
/// how square the nearest of its ways faces the place, times how square the place faces the box -- pbrt-v4's
/// <c>LightBounds::Importance</c>, with each light's own fading in place of the inverse square.
/// </para>
/// <para>
/// **Near clusters are opened; far ones are sampled.**  Going down from the root, a node that looks wider from the
/// place than <see cref="Split"/> -- its radius over its distance -- is opened, until its lights are each looked at in
/// full; the nodes left are where the near lights give way to the far, and the far are sampled: <see cref="Samples"/>
/// picks among those nodes in proportion to their importance, each going down its node child by child the same way,
/// every light picked worth one over its chance of being picked over the number of picks.  So the answer is right on
/// average, a lamp at a table is never left to chance, and a street of lamps a kilometer off costs a handful.
/// </para>
/// </summary>
internal sealed class LightTree
{
    /// <summary>
    /// How many fading lights a scene must have before a tree is built of them unasked.
    /// </summary>
    internal const int WorthBuilding = 16;

    /// <summary>
    /// How many picks among the far lights a place first met from the eye takes, unless the scene says.
    /// </summary>
    internal const int DefaultSamples = 16;

    /// <summary>
    /// How wide a node may look, its radius over its distance, before it is opened, unless the scene says.
    /// </summary>
    internal const double DefaultSplit = 0.25;

    // The most lights a place looks at in full before the nodes left are sampled however wide they look (unless the
    // split is nought, which asks for every one).
    private const int MostOpened = 64;

    // Where the picks' numbers start in the run each place draws from (see Scene.ShiftFor's salts).
    private const int Salt = 1000;


    private readonly Light[] _held;
    private readonly Node[] _nodes;

    /// <summary>
    /// This property holds the lights the tree does not hold, which every place looks at.
    /// </summary>
    internal Light[] Always { get; }

    /// <summary>
    /// This property holds how many picks among the far lights a place first met from the eye takes.
    /// </summary>
    internal int Samples { get; }

    /// <summary>
    /// This property holds how wide a node may look from a place, its radius over its distance, before it is opened.
    /// </summary>
    internal double Split { get; }

    /// <summary>
    /// This property holds how many lights the tree holds.
    /// </summary>
    internal int Count => _held.Length;

    // One node of the tree: the box its lights stand in, everything the importance needs, and its children (or, for a
    // leaf, the light it holds).
    private struct Node
    {
        internal double LowX, LowY, LowZ, HighX, HighY, HighZ;
        internal double Power;
        internal double Reach;
        internal double Fall;
        internal Vector Axis;
        internal double CosOpening;
        internal double CosEdge;
        internal int Left;
        internal int Right;
        internal int Light;

        internal readonly bool IsLeaf => Left < 0;

        internal readonly Point Middle => new ((LowX + HighX) / 2, (LowY + HighY) / 2, (LowZ + HighZ) / 2);

        internal readonly double Radius
        {
            get
            {
                double x = HighX - LowX, y = HighY - LowY, z = HighZ - LowZ;

                return Math.Sqrt(x * x + y * y + z * z) / 2;
            }
        }
    }

    private LightTree(Light[] held, Node[] nodes, Light[] always, int samples, double split)
    {
        _held = held;
        _nodes = nodes;
        Always = always;
        Samples = samples;
        Split = split;
        Ambient = new AmbientOfTheHeld(held).Summed();
    }

    /// <summary>
    /// This property holds the light that stands for every held light's ambient: what each lamp lends a surface
    /// for the light it stands in for, which no shadow takes away and no distance thins -- so it is the same from
    /// every lamp at every place, and is given in full, never picked.  Left to the picks, the ambient of five
    /// hundred lamps a kilometer off came from the rare place that picked one, weighed as all of them: sparkles.
    /// </summary>
    internal Light Ambient { get; }

    /// <summary>
    /// This method builds a tree of the given lights that thin with distance, or returns <c>null</c> when there are
    /// too few of them to be worth one.
    /// </summary>
    /// <param name="lights">The scene's lights.</param>
    /// <param name="times">How many instants of the shutter's opening the lights may stand at.</param>
    /// <param name="asked">Whether the scene asked for a tree: then one is built for as few as two.</param>
    /// <param name="samples">How many picks a place first met from the eye takes.</param>
    /// <param name="split">How wide a node may look before it is opened.</param>
    /// <returns>The tree, or <c>null</c>.</returns>
    internal static LightTree Of(List<Light> lights, int times, bool asked, int samples, double split)
    {
        List<Light> held = [];
        List<Light> always = [];

        foreach (Light light in lights)
        {
            if (Fades(light))
                held.Add(light);
            else
                always.Add(light);
        }

        if (held.Count < (asked ? 2 : WorthBuilding + 1))
            return null;

        Node[] leaves = held.Select((light, index) => LeafOf(light, index, times)).ToArray();
        List<Node> nodes = [];
        int[] order = Enumerable.Range(0, leaves.Length).ToArray();

        Build(leaves, order, 0, order.Length, nodes);

        return new LightTree(held.ToArray(), nodes.ToArray(), always.ToArray(), samples, split);
    }

    /// <summary>
    /// This method notes whether a light is one the tree holds: a point light, spotlight or area light that thins with
    /// distance.
    /// </summary>
    internal static bool Fades(Light light) =>
        light is PointLight && !light.CanBeSeen && light.FadeDistance is > 0 && light.FadePower > 0;

    /// <summary>
    /// This method makes the leaf for one light: its box over every instant it stands at, its power, its fading, and
    /// the cone of the ways it shines.
    /// </summary>
    private static Node LeafOf(Light light, int index, int times)
    {
        Node node = new ()
        {
            LowX = double.PositiveInfinity, LowY = double.PositiveInfinity, LowZ = double.PositiveInfinity,
            HighX = double.NegativeInfinity, HighY = double.NegativeInfinity, HighZ = double.NegativeInfinity,
            Power = Math.Max(light.Color.Red, Math.Max(light.Color.Green, light.Color.Blue)),
            Reach = light.FadeDistance ?? 0,
            Fall = light.FadePower,
            Axis = new Vector(0, 1, 0),
            CosOpening = -1,
            CosEdge = 0,
            Left = -1,
            Right = -1,
            Light = index
        };

        for (int time = 0; time < Math.Max(times, 1); time++)
        {
            switch (light.At(time))
            {
                case AreaLight area:
                    foreach (double u in new[] { -0.5, 0.5 })
                    {
                        foreach (double v in new[] { -0.5, 0.5 })
                            Hold(ref node, area.Location + area.Axis1 * u + area.Axis2 * v);
                    }

                    break;
                case Spotlight spot:
                    Hold(ref node, spot.Location);

                    // A spotlight shines down its axis and out to the edge of its falloff; past that, nothing.
                    if (!spot.PointAt.Matches(spot.Location))
                    {
                        node.Axis = (spot.PointAt - spot.Location).Unit;
                        node.CosOpening = 1;
                        node.CosEdge = Math.Cos(Math.Min(Math.Max(spot.Falloff, spot.Radius), Math.PI / 2));
                    }

                    break;
                case PointLight point:
                    Hold(ref node, point.Location);
                    break;
            }
        }

        return node;
    }

    /// <summary>
    /// This method grows a node's box to hold a place.
    /// </summary>
    private static void Hold(ref Node node, Point place)
    {
        node.LowX = Math.Min(node.LowX, place.X);
        node.LowY = Math.Min(node.LowY, place.Y);
        node.LowZ = Math.Min(node.LowZ, place.Z);
        node.HighX = Math.Max(node.HighX, place.X);
        node.HighY = Math.Max(node.HighY, place.Y);
        node.HighZ = Math.Max(node.HighZ, place.Z);
    }

    /// <summary>
    /// This method builds the node over a run of the leaves, cutting the run across whichever axis their middles are
    /// most spread along, at the middle of that spread (by count, if that leaves one side empty), and returns its
    /// number.  The root is node nought.
    /// </summary>
    private static int Build(Node[] leaves, int[] order, int from, int to, List<Node> nodes)
    {
        if (to - from == 1)
        {
            nodes.Add(leaves[order[from]]);

            return nodes.Count - 1;
        }

        int number = nodes.Count;

        nodes.Add(default);

        double lowX = double.PositiveInfinity, lowY = double.PositiveInfinity, lowZ = double.PositiveInfinity;
        double highX = double.NegativeInfinity, highY = double.NegativeInfinity, highZ = double.NegativeInfinity;

        for (int index = from; index < to; index++)
        {
            Point middle = leaves[order[index]].Middle;

            lowX = Math.Min(lowX, middle.X);
            lowY = Math.Min(lowY, middle.Y);
            lowZ = Math.Min(lowZ, middle.Z);
            highX = Math.Max(highX, middle.X);
            highY = Math.Max(highY, middle.Y);
            highZ = Math.Max(highZ, middle.Z);
        }

        double spreadX = highX - lowX, spreadY = highY - lowY, spreadZ = highZ - lowZ;
        int axis = spreadX >= spreadY && spreadX >= spreadZ ? 0 : spreadY >= spreadZ ? 1 : 2;
        double at = axis == 0 ? (lowX + highX) / 2 : axis == 1 ? (lowY + highY) / 2 : (lowZ + highZ) / 2;
        int cut = from;

        for (int index = from; index < to; index++)
        {
            Point middle = leaves[order[index]].Middle;
            double coordinate = axis == 0 ? middle.X : axis == 1 ? middle.Y : middle.Z;

            if (coordinate < at)
            {
                (order[cut], order[index]) = (order[index], order[cut]);
                cut++;
            }
        }

        if (cut == from || cut == to)
            cut = (from + to) / 2;

        int left = Build(leaves, order, from, cut, nodes);
        int right = Build(leaves, order, cut, to, nodes);

        nodes[number] = Joined(nodes[left], nodes[right], left, right);

        return number;
    }

    /// <summary>
    /// This method makes the node over two others: the box round both, their powers added, their fading as one (the
    /// reach their powers' squares weigh, the slowest fall), and the cone round both their cones (pbrt-v4's
    /// <c>DirectionCone</c> union), reaching out to the wider of their edges.
    /// </summary>
    private static Node Joined(Node a, Node b, int left, int right)
    {
        double power = a.Power + b.Power;
        double reach = power > 0
            ? Math.Sqrt((a.Power * a.Reach * a.Reach + b.Power * b.Reach * b.Reach) / power)
            : Math.Max(a.Reach, b.Reach);
        (Vector axis, double cosOpening) = ConeRound(a.Axis, a.CosOpening, b.Axis, b.CosOpening);

        return new Node
        {
            LowX = Math.Min(a.LowX, b.LowX), LowY = Math.Min(a.LowY, b.LowY), LowZ = Math.Min(a.LowZ, b.LowZ),
            HighX = Math.Max(a.HighX, b.HighX), HighY = Math.Max(a.HighY, b.HighY), HighZ = Math.Max(a.HighZ, b.HighZ),
            Power = power,
            Reach = reach,
            Fall = Math.Min(a.Fall, b.Fall),
            Axis = axis,
            CosOpening = cosOpening,
            CosEdge = Math.Min(a.CosEdge, b.CosEdge),
            Left = left,
            Right = right,
            Light = -1
        };
    }

    /// <summary>
    /// This method returns the narrowest cone found holding two cones, each said by its axis and the cosine of its
    /// half-angle: one of them, if it holds the other; the whole sphere, past a half-turn; else a cone between them.
    /// </summary>
    private static (Vector Axis, double Cos) ConeRound(Vector a, double cosA, Vector b, double cosB)
    {
        double thetaA = Math.Acos(Math.Clamp(cosA, -1, 1));
        double thetaB = Math.Acos(Math.Clamp(cosB, -1, 1));
        double thetaD = Math.Acos(Math.Clamp(a.Dot(b), -1, 1));

        if (Math.Min(thetaD + thetaB, Math.PI) <= thetaA)
            return (a, cosA);

        if (Math.Min(thetaD + thetaA, Math.PI) <= thetaB)
            return (b, cosB);

        double thetaO = (thetaA + thetaD + thetaB) / 2;

        if (thetaO >= Math.PI)
            return (a, -1);

        Vector turn = a.Cross(b);

        if (turn.Magnitude < 1e-12)
            return (a, -1);

        // The axis turned from a toward b, about their cross, by the angle the new cone reaches past a's.
        double thetaR = thetaO - thetaA;
        Vector k = turn.Unit;
        Vector axis = a * Math.Cos(thetaR) + k.Cross(a) * Math.Sin(thetaR) + k * (k.Dot(a) * (1 - Math.Cos(thetaR)));

        return (axis.Unit, Math.Cos(thetaO));
    }

    /// <summary>
    /// This method works out how much a node's lights could give a place: pbrt-v4's importance, with the node's own
    /// fading over the distance in place of the inverse square.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="where">The place.</param>
    /// <param name="normal">Which way the place faces, or <c>null</c> for a place in a medium.</param>
    /// <param name="oneSided">Whether only light on the side the normal faces can light the place.</param>
    /// <returns>The node's importance there.</returns>
    private static double ImportanceOf(in Node node, Point where, Vector normal, bool oneSided, in Lobe lobe = default)
    {
        if (node.Power <= 0)
            return 0;

        Point middle = node.Middle;
        Vector off = where - middle;
        double distance = off.Magnitude;
        double radius = node.Radius;
        double near = Math.Max(distance, radius);
        double fading = near <= node.Reach || node.Fall <= 0
            ? 1
            : node.Fall == 2
                ? node.Reach * node.Reach / (near * near)
                : Math.Pow(node.Reach / near, node.Fall);

        // Inside the box, or on its middle, every way might be the way: nothing is ruled out by direction.
        if (distance <= radius || distance <= 0)
            return node.Power * fading * (lobe.Mirror is null ? 1 : 1 + lobe.Strength);

        Vector toward = off * (1 / distance);
        double sinBound = radius / distance;
        double cosBound = Math.Sqrt(Math.Max(0, 1 - sinBound * sinBound));

        // How square the nearest of the node's ways faces the place.
        double cosAway = node.Axis.Dot(toward);
        double sinAway = Math.Sqrt(Math.Max(0, 1 - cosAway * cosAway));
        double sinOpening = Math.Sqrt(Math.Max(0, 1 - node.CosOpening * node.CosOpening));
        double cosPast = CosLess(sinAway, cosAway, sinOpening, node.CosOpening);
        double sinPast = SinLess(sinAway, cosAway, sinOpening, node.CosOpening);
        double cosFacing = CosLess(sinPast, cosPast, sinBound, cosBound);

        if (cosFacing <= node.CosEdge)
            return 0;

        double importance = node.Power * fading * cosFacing;

        // How square the place faces the nearest of the box: on its own side only for an opaque solid, which a light
        // behind gives nothing; either side for a sheet, or anything light passes through.
        if (normal is not null)
        {
            double cosIn = oneSided ? -toward.Dot(normal) : Math.Abs(toward.Dot(normal));
            double sinIn = Math.Sqrt(Math.Max(0, 1 - cosIn * cosIn));

            importance *= Math.Max(0, CosLess(sinIn, cosIn, sinBound, cosBound));
        }

        // And on a glossy place, more where the place mirrors the eye: its highlight's lobe, as near as the box comes
        // to where the place mirrors, raised to the lobe's power.  It weighs the picks, never what a pick is worth, so
        // it is only a guide: a lamp anywhere is still picked by the chance it is given.
        if (lobe.Mirror is { } mirror && importance > 0)
        {
            double cosMirror = -toward.Dot(mirror);
            double sinMirror = Math.Sqrt(Math.Max(0, 1 - cosMirror * cosMirror));
            double closest = CosLess(sinMirror, cosMirror, sinBound, cosBound);

            if (closest > 0)
                importance *= 1 + lobe.Strength * Math.Pow(closest, lobe.Power);
        }

        return Math.Max(importance, 0);
    }

    /// <summary>
    /// This record holds a glossy place's highlight, as the picks are guided by it: the way the place mirrors the eye,
    /// how sharply the highlight falls away from it, as the power of the cosine, and how bright it is at its peak
    /// against the place's diffuse light.  Nothing, for a matte place.
    /// </summary>
    /// <param name="Mirror">The way, from the place, that the place mirrors the eye, or <c>null</c>.</param>
    /// <param name="Power">The power of the cosine the highlight falls away by.</param>
    /// <param name="Strength">How bright the highlight is at its peak, as a share of the diffuse light.</param>
    internal readonly record struct Lobe(Vector Mirror, double Power, double Strength)
    {
        /// <summary>
        /// This method returns the lobe of a material's brightest highlight at a place seen from the given way: a
        /// coat's, a rough base's, or Phong's; nothing for a material with none worth guiding the picks by.  A rough
        /// lobe of alpha a falls away about as a cosine to the power 1 / (2a^2), and peaks at about its mirror share --
        /// Schlick's, at the eye's slant -- over 4 pi a^2; Phong's falls away to its shininess and peaks at its
        /// specular.  Weighed by a flat amount
        /// instead, a floor's faint highlight drew the picks from the light it was actually lit by, and the grain
        /// doubled.
        /// </summary>
        /// <param name="material">The material.</param>
        /// <param name="normal">The way the place faces.</param>
        /// <param name="eye">The way to the eye.</param>
        /// <returns>The lobe.</returns>
        internal static Lobe Of(Material material, Vector normal, Vector eye)
        {
            double diffuse = Math.Max(material.Diffuse * 0.5, 0.05);
            double slant = Math.Pow(1 - Math.Clamp(normal.Dot(eye), 0, 1), 5);
            double power = 0;
            double strength = 0;

            // A rough lobe's mirror share rises toward a graze as Schlick's Fresnel has it: a table seen at a slant
            // mirrors several times what it does seen from above.
            void Rough(double share, double roughness)
            {
                double alpha = roughness * roughness;
                double fresnel = share + (1 - share) * slant;
                double peak = fresnel / (4 * Math.PI * alpha * alpha) / diffuse;

                if (peak > strength)
                {
                    strength = peak;
                    power = 1 / (2 * alpha * alpha);
                }
            }

            if (material.CoatApplies)
                Rough(0.04, Math.Max(material.Coat.Roughness, Coat.LampRoughness));

            if (material.IsRough)
                Rough(Math.Max(material.Reflective, 0.04), material.Roughness);
            else if (material.Specular > 0 && material.Specular / diffuse > strength)
            {
                strength = material.Specular / diffuse;
                power = material.Shininess;
            }

            return power < 2 || strength < 0.5
                ? default
                : new Lobe(normal * (2 * normal.Dot(eye)) - eye, Math.Min(power, 10000), Math.Min(strength, 1000));
        }
    }

    // The cosine and sine of one angle less another, never below nought, from their sines and cosines.
    private static double CosLess(double sinA, double cosA, double sinB, double cosB) =>
        cosA > cosB ? 1 : cosA * cosB + sinA * sinB;

    private static double SinLess(double sinA, double cosA, double sinB, double cosB) =>
        cosA > cosB ? 0 : sinA * cosB - cosA * sinB;

    /// <summary>
    /// This method puts the lights a place is to be lit by in the given list, each with what its answer is worth and
    /// whether the tree holds it: every light the tree does not hold, at one; the light standing for every held
    /// light's ambient (see <see cref="Ambient"/>), at one; every light of a node opened, at one; and the picks
    /// among the rest.  A held light gives only its direct light, its ambient being the stand-in's.
    /// </summary>
    /// <param name="where">The place.</param>
    /// <param name="normal">Which way the place faces, or <c>null</c>.</param>
    /// <param name="oneSided">Whether only light on the side the normal faces can light the place.</param>
    /// <param name="picks">How many picks among the far lights to take.</param>
    /// <param name="into">The list to fill.</param>
    /// <param name="lobe">The place's highlight, which guides the picks toward where it mirrors the eye.</param>
    internal void Choose(
        Point where, Vector normal, bool oneSided, int picks, List<(Light Light, double Weight, bool Held)> into,
        Lobe lobe = default)
    {
        foreach (Light light in Always)
            into.Add((light, 1, false));

        into.Add((Ambient, 1, false));

        // Open the nodes that look wide from here, widest first being the nearest, until the most that may be opened
        // are; the rest are where the far lights begin.
        List<(int Node, double Importance)> far = Far;
        Queue<int> open = Open;
        int opened = 0;

        far.Clear();
        open.Clear();
        open.Enqueue(0);

        while (open.TryDequeue(out int number))
        {
            ref Node node = ref _nodes[number];

            bool budget = Split <= 0 || opened < MostOpened;

            // A light reached by opening the node round it is near: it is looked at in full.  (The root, a lone
            // light, is too.)
            if (node.IsLeaf && budget)
            {
                if (ImportanceOf(node, where, normal, oneSided) > 0)
                {
                    into.Add((_held[node.Light], 1, true));
                    opened++;
                }

                continue;
            }

            if (!node.IsLeaf && budget && LooksWide(node, where))
            {
                open.Enqueue(node.Left);
                open.Enqueue(node.Right);

                continue;
            }

            double importance = ImportanceOf(node, where, normal, oneSided, lobe);

            if (importance > 0)
                far.Add((number, importance));
        }

        if (far.Count == 0 || picks <= 0)
            return;

        double total = 0;

        foreach ((_, double importance) in far)
            total += importance;

        int first = into.Count;

        double offset = Scene.ShiftFor(where, Salt);

        for (int pick = 0; pick < picks; pick++)
        {
            // One number for the whole pick, the picks spread evenly over the far lights' importance, and stretched
            // again at every choice to the part of it that choice took (pbrt's SampleDiscrete): so the picks stay
            // spread evenly over the lights themselves, not only over the nodes they start from.
            double draw = (pick + offset) / picks;
            double running = 0;
            int chosen = far.Count - 1;

            for (int index = 0; index < far.Count; index++)
            {
                double share = far[index].Importance / total;

                if (draw < running + share || index == far.Count - 1)
                {
                    chosen = index;
                    draw = Math.Clamp((draw - running) / share, 0, Math.BitDecrement(1.0));
                    break;
                }

                running += share;
            }

            double chance = far[chosen].Importance / total;
            int at = far[chosen].Node;

            while (!_nodes[at].IsLeaf)
            {
                ref Node node = ref _nodes[at];
                double left = ImportanceOf(_nodes[node.Left], where, normal, oneSided, lobe);
                double right = ImportanceOf(_nodes[node.Right], where, normal, oneSided, lobe);

                if (left + right <= 0)
                {
                    chance = 0;
                    break;
                }

                double toLeft = left / (left + right);

                if (draw < toLeft)
                {
                    chance *= toLeft;
                    draw = Math.Min(draw / toLeft, Math.BitDecrement(1.0));
                    at = node.Left;
                }
                else
                {
                    chance *= 1 - toLeft;
                    draw = Math.Min((draw - toLeft) / (1 - toLeft), Math.BitDecrement(1.0));
                    at = node.Right;
                }
            }

            if (chance <= 0)
                continue;

            Light light = _held[_nodes[at].Light];
            double weight = 1 / (picks * chance);
            int found = -1;

            for (int index = first; index < into.Count; index++)
            {
                if (ReferenceEquals(into[index].Light, light))
                {
                    found = index;
                    break;
                }
            }

            if (found < 0)
                into.Add((light, weight, true));
            else
                into[found] = (light, into[found].Weight + weight, true);
        }
    }

    /// <summary>
    /// This method notes whether a node looks wide enough from a place to be opened: its radius over its distance at
    /// least the split, or the place inside it.  At a split of nought every node is opened, and every light looked at.
    /// </summary>
    private bool LooksWide(in Node node, Point where)
    {
        double distance = (where - node.Middle).Magnitude;
        double radius = node.Radius;

        return distance <= radius || radius >= Split * distance;
    }

    /// <summary>
    /// This class is the light that stands for every held light's ambient: it carries all their light together,
    /// and none of it reaches anywhere -- its one sample is worth nothing, so it is spared its shadow ray and a
    /// surface takes only the ambient it lends.
    /// </summary>
    private sealed class AmbientOfTheHeld(Light[] held) : Light
    {
        private readonly ConcurrentDictionary<Type, object> _sums = new ();

        /// <summary>
        /// This method returns all the held lights' light together, in whatever bands the render carries.
        /// </summary>
        public override TS EmittedToward<TS>(LightSample sample)
        {
            if (_sums.TryGetValue(typeof(TS), out object known))
                return (TS) known;

            TS sum = TS.Black;

            foreach (Light light in held)
                sum += light.Emitted<TS>();

            _sums[typeof(TS)] = sum;

            return sum;
        }

        /// <summary>
        /// This method returns the one sample, which is worth nothing.
        /// </summary>
        public override LightSample SampleToward(Point point, int index, Vector normal = null) =>
            new (normal ?? new Vector(0, 1, 0), double.PositiveInfinity, 0);

        /// <summary>
        /// This method returns a way toward the light, which lies nowhere.
        /// </summary>
        public override (Vector Direction, double Distance) TowardFrom(Point point) =>
            (new Vector(0, 1, 0), double.PositiveInfinity);

        /// <summary>
        /// This method returns the light's color: all the held lights' together.
        /// </summary>
        public override Color ColorFor(LightSample sample) => Color;

        internal AmbientOfTheHeld Summed()
        {
            double red = 0, green = 0, blue = 0;

            foreach (Light light in held)
            {
                red += light.Color.Red;
                green += light.Color.Green;
                blue += light.Color.Blue;
            }

            Color = new Color(red, green, blue);

            return this;
        }
    }

    [ThreadStatic] private static List<(int Node, double Importance)> _far;
    [ThreadStatic] private static Queue<int> _open;

    private static List<(int Node, double Importance)> Far => _far ??= [];
    private static Queue<int> Open => _open ??= new Queue<int>();
}
