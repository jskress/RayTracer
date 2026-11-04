using RayTracer.Basics;
using RayTracer.Geometry;

namespace RayTracer.Core;

/// <summary>
/// This part of the scene hands a place the lights it is to be lit by: every light, at a weight of one, unless the
/// scene holds enough lights that thin with distance for a tree of them (<see cref="LightTree"/>), when a place
/// looks at a few picked by how much each could give it, each weighed as its picking says.
/// </summary>
public partial class Scene
{
    /// <summary>
    /// This property holds the tree of the scene's lights that thin with distance, if it has one.
    /// </summary>
    internal LightTree LightTree { get; private set; }

    // Every surface any light stands housed in, gathered once: asked light by light, a bounce in a street of five
    // hundred lamps looked through five hundred housings at every place it met.
    private HashSet<(Surface Surface, Portals Portals)> _housings;

    /// <summary>
    /// This method builds the tree of the scene's lights that thin with distance, once every light is set out, or
    /// leaves it without one: when there are too few unasked, or when the scene says not to.
    /// </summary>
    /// <param name="times">How many instants of the shutter's opening the lights may stand at.</param>
    /// <param name="asked">Whether the scene asked for a tree (<c>true</c>), asked for none (<c>false</c>), or said
    /// nothing (<c>null</c>).</param>
    /// <param name="samples">How many picks among the far lights a place first met from the eye takes.</param>
    /// <param name="split">How wide a node may look before it is opened.</param>
    internal void ArrangeTheLights(int times, bool? asked, int samples, double split)
    {
        LightTree = asked == false ? null : LightTree.Of(Lights, times, asked == true, samples, split);
        _housings = null;
    }

    /// <summary>
    /// This method returns the lights a place is to be lit by, each with what its answer is worth: every light, at
    /// one, with no tree; with one, those it chooses (see <see cref="Core.LightTree.Choose"/>).  A place first met
    /// from the eye takes the tree's picks; anywhere else -- along a bounce, in a medium -- one, the many places
    /// there averaging it out as they average a lamp's one sample.  Dispose of what comes back when done with it.
    /// </summary>
    /// <param name="where">The place being lit.</param>
    /// <param name="normal">Which way it faces, or <c>null</c> for a place in a medium.</param>
    /// <param name="first">Whether the place was first met from the eye.</param>
    /// <param name="oneSided">Whether only light on the side the normal faces can light the place: an opaque solid's,
    /// not a sheet's or glass's.</param>
    /// <returns>The lights, and their weights.</returns>
    /// <param name="lobe">The place's highlight, if it has one and shows it, which guides the picks.</param>
    internal LightPicks LightsAt(
        Point where, Vector normal, bool first, bool oneSided = false, LightTree.Lobe lobe = default)
    {
        if (LightTree is not { } tree)
            return new LightPicks(Lights, null);

        List<(Light, double, bool)> picked = LightPicks.Rent();

        tree.Choose(where, normal, oneSided && normal is not null, first ? tree.Samples : 1, picked, lobe);

        return new LightPicks(null, picked);
    }

    /// <summary>
    /// This method notes whether a surface, standing behind the given instances, is one some light is housed in.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="portals">The instances it stands behind.</param>
    /// <returns><c>true</c>, if some light is housed in it.</returns>
    internal bool HousesALight(Surface surface, Portals portals)
    {
        HashSet<(Surface, Portals)> housings = LazyInitializer.EnsureInitialized(ref _housings, () =>
        {
            HashSet<(Surface, Portals)> all = [];

            foreach (Light light in Lights)
            {
                if (light.Housing is { } housing)
                    all.UnionWith(housing);
            }

            return all;
        });

        return housings.Count > 0 && housings.Contains((surface, portals));
    }
}

/// <summary>
/// This struct holds the lights a place is to be lit by and what each one's answer is worth (see
/// <see cref="Scene.LightsAt"/>): the scene's own list of them, each worth one, or a list of those a tree chose, which
/// goes back to be used again once the place is lit.
/// </summary>
internal readonly struct LightPicks : IDisposable
{
    [ThreadStatic] private static Stack<List<(Light, double, bool)>> _spare;

    private readonly List<Light> _all;
    private readonly List<(Light Light, double Weight, bool Held)> _picked;

    internal LightPicks(List<Light> all, List<(Light, double, bool)> picked)
    {
        _all = all;
        _picked = picked;
    }

    /// <summary>
    /// This property reports how many lights there are.
    /// </summary>
    internal int Count => _picked?.Count ?? _all?.Count ?? 0;

    /// <summary>
    /// This indexer returns one of the lights, its weight, and whether a tree holds it -- a held light gives only its
    /// direct light, its ambient being given by the light that stands for every held light's (see
    /// <see cref="LightTree.Ambient"/>).
    /// </summary>
    internal (Light Light, double Weight, bool Held) this[int index] =>
        _picked is null ? (_all[index], 1, false) : _picked[index];

    /// <summary>
    /// This method returns a list to choose lights into, one kept from before if there is one.
    /// </summary>
    internal static List<(Light, double, bool)> Rent()
    {
        Stack<List<(Light, double, bool)>> spare = _spare ??= new Stack<List<(Light, double, bool)>>();

        return spare.TryPop(out List<(Light, double, bool)> list) ? list : [];
    }

    /// <summary>
    /// This method hands a chosen list back to be used again.
    /// </summary>
    public void Dispose()
    {
        if (_picked is null)
            return;

        _picked.Clear();
        (_spare ??= new Stack<List<(Light, double, bool)>>()).Push(_picked);
    }

    /// <summary>
    /// This method returns the means of walking the lights.
    /// </summary>
    public Enumerator GetEnumerator() => new (this);

    /// <summary>
    /// This struct walks the lights a place is lit by, with their weights and whether a tree holds them.
    /// </summary>
    internal struct Enumerator(LightPicks picks)
    {
        private int _index = -1;

        public (Light Light, double Weight, bool Held) Current => picks[_index];

        public bool MoveNext() => ++_index < picks.Count;
    }
}
