using System.Text;
using RayTracer.Core;

namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This class represents an L-system primitive.
/// </summary>
public class LSystem : Group
{
    /// <summary>
    /// This property holds the axiom, or starting point, for the L-system production.
    /// </summary>
    public string Axiom { get; set; }

    /// <summary>
    /// This property holds the number of generations that the L-system production should
    /// use.
    /// </summary>
    public int Generations { get; set; }

    /// <summary>
    /// This property holds the list of render command overrides to use with this L-system.
    /// </summary>
    public List<LSystemRenderCommandMapping> CommandMappings { get; } = [];

    /// <summary>
    /// This property holds the list of production rules that the L-system producer should
    /// use.
    /// </summary>
    public List<ProductionRuleSpec> ProductionRules { get; } = [];

    /// <summary>
    /// This property holds the set of controls that dictate how productions from this
    /// L-system are rendered into geometry.
    /// </summary>
    public LSystemRenderingControls RenderingControls { get; set; } = new ();

    /// <summary>
    /// This property holds the recipe for the leaf surface to stamp down for each <c>~</c>
    /// command in the production.  It defaults to a small green patch; a scene may override
    /// it with a named surface of its own.  It is a factory because every leaf needs its own
    /// copy and a named leaf must be resolved with a render context this surface doesn't hold
    /// at render time, so the resolver captures that resolution here at resolve time.
    /// </summary>
    public Func<Surface> LeafFactory { get; set; } = DefaultLeaf.Create;

    /// <summary>
    /// This property notes whether <see cref="LeafFactory"/> is still the built-in leaf rather than
    /// one the scene named.  It matters only for colour: the built-in leaf carries a green to fall
    /// back on, so that a bare tree reads as leaves rather than as bark, and that green is the last
    /// thing consulted.  A leaf the scene named has no such fallback -- if the scene left it bare,
    /// it meant it to inherit like anything else.
    /// </summary>
    public bool UsesBuiltInLeaf { get; set; } = true;

    /// <summary>
    /// This property holds the recipe for each surface the production may name after a <c>~</c>,
    /// keyed by the character that names it, so one plant may carry leaves, blossom and fruit.
    /// A <c>~</c> naming nothing bound here falls back to <see cref="LeafFactory"/>.
    /// </summary>
    public Dictionary<Rune, Func<Surface>> SurfaceFactories { get; } = new ();

    /// <summary>
    /// This property holds the material each character in the production stands for.  Meeting one
    /// changes what the turtle draws with from there on, and a branch gives the change back when
    /// it closes, so a limb may be coloured without disturbing its neighbours.
    /// </summary>
    public Dictionary<Rune, Material> MaterialBindings { get; } = new ();

    /// <summary>
    /// This property holds the material to draw with at each branching depth, the first standing
    /// for the trunk.  It is how a plant is coloured from root to tip without a production rule
    /// per level; anything deeper than the list draws with the last of them.
    /// </summary>
    public List<Material> DepthMaterials { get; } = [];

    /// <summary>
    /// This property notes whether turtle orientation commands should be ignored regarding
    /// context sensitive evaluation.
    /// </summary>
    public bool IgnoreOrientationCommands { get; set; }

    /// <summary>
    /// This property holds the collection of runes that should be ignored regarding
    /// context sensitive evaluation.
    /// </summary>
    public Rune[] SymbolsToIgnore { get; init; }

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to
    /// perform any expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        string production = GetProduction();
        LSystemShapeRenderer renderer = RenderingControls.CreateRenderer(production);

        foreach (LSystemRenderCommandMapping mapping in CommandMappings)
            renderer.CommandMapping[mapping.CommandCharacter] = mapping.TurtleCommand;

        renderer.LeafFactory = LeafFactory;
        renderer.DefaultLeafMaterialFactory = UsesBuiltInLeaf ? DefaultLeaf.CreateMaterial : null;

        foreach (KeyValuePair<Rune, Func<Surface>> pair in SurfaceFactories)
            renderer.SurfaceFactories[pair.Key] = pair.Value;

        foreach (KeyValuePair<Rune, Material> pair in MaterialBindings)
            renderer.MaterialBindings[pair.Key] = pair.Value;

        renderer.DepthMaterials.AddRange(DepthMaterials);

        renderer.Render();

        foreach (Surface surface in renderer.Surfaces)
            Add(surface);

        BoundingBox ??= renderer.BoundingBox;

        base.PrepareSurfaceForRendering();
    }

    /// <summary>
    /// This method is used to create the production from which we should generate our
    /// geometry.
    /// </summary>
    /// <returns>The production to use.</returns>
    private string GetProduction()
    {
        LSystemProducer producer = new LSystemProducer
        {
            Axiom = Axiom,
            Seed = Seed,
            SymbolsToIgnore = GetAllSymbolsToIgnore()
        };

        foreach (ProductionRuleSpec rule in ProductionRules)
            producer.AddRule(rule);

        return producer.Produce(Generations);
    }

    /// <summary>
    /// This method creates an array of the runes that should be ignored regarding context
    /// sensitive evaluation..
    /// </summary>
    /// <returns>The array of runes that should be ignored.</returns>
    private Rune[] GetAllSymbolsToIgnore()
    {
        HashSet<Rune> renderCommands = [];

        if (IgnoreOrientationCommands)
            renderCommands.UnionWith(LSystemShapeRenderer.Commands);

        if (SymbolsToIgnore != null)
            renderCommands.UnionWith(SymbolsToIgnore);

        return renderCommands.ToArray();
    }
}
