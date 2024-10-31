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
    public List<ProductionRule> ProductionRules { get; } = [];

    /// <summary>
    /// This property holds the type of renderer to use when converting an L-system
    /// production into geometry.
    /// </summary>
    public LSystemRendererType RendererType { get; set; }

    /// <summary>
    /// This property carries the global angle to use in rendering the surface. 
    /// </summary>
    public double Angle { get; set; }

    /// <summary>
    /// This property carries the global distance the turtle is to travel for each move
    /// to use in rendering the surface. 
    /// </summary>
    public double Distance { get; set; } = 1;

    /// <summary>
    /// This property carries the global diameter of the "stroke" that the turtle is to
    /// use for each move in rendering the surface. 
    /// </summary>
    public double Diameter { get; set; } = 0.1;

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to
    /// perform any expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        string production = GetProduction();
        LSystemShapeRenderer renderer = RendererType.GetRenderer(production);

        renderer.Angle = Angle;
        renderer.Distance = Distance;
        renderer.Diameter = Diameter;
        
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
            Seed = Seed
        };

        foreach (ProductionRule rule in ProductionRules)
            producer.AddRule(rule);

        return producer.Produce(Generations);
    }
}
