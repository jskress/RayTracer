using System.Text;

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
