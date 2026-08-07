using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Pigments;
using RayTracer.Terms;

namespace RayTracer.Instructions;

/// <summary>
/// This class represents a render operation.
/// </summary>
public class RenderInstruction : Instruction
{
    /// <summary>
    /// The material to use for surfaces that asked to inherit their material but the
    /// inheritance never happened.
    /// </summary>
    private static readonly Material OrphanMaterial = new ()
    {
        Pigment = new SolidPigment(Colors.Gray40)
    };

    /// <summary>
    /// This property is used to inform the instruction of the current set of objects.
    /// </summary>
    internal List<object> Objects { private get; set; }

    /// <summary>
    /// This property tells us whether our set of objects contains explicit scenes.
    /// </summary>
    private bool HasExplicitScene => Objects.Any(thing => thing is Scene);

    /// <summary>
    /// This property exposes the canvas that represents the actual image we rendered.
    /// </summary>
    internal Canvas Canvas { get; private set; }

    private readonly Term _sceneName;
    private readonly Term _cameraName;

    public RenderInstruction(Term sceneName, Term cameraName)
    {
        _sceneName = sceneName;
        _cameraName = cameraName;
    }

    /// <summary>
    /// This method is used to execute the instruction.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        using Scene scene = GetScene(context, variables);
        Camera camera = GetCamera(context, scene, variables);

        // Give each surface a chance to do any precomputing needed, telling it the instants the
        // camera will look at so that anything set moving can work out where it stands at each of
        // them before the first ray is fired.
        double[] sampleTimes = camera.SampleTimes;

        foreach (Surface surface in scene.Surfaces)
            surface.PrepareForRendering(sampleTimes);

        // Two things can only be settled once the scene is whole, since each depends on the company it
        // keeps rather than on anything written beside it.
        SettleTheSky(context, scene);
        FinalizeSurfaceData(context, scene, scene.Surfaces);

        // The sky is a pigment as much as any surface's is, and no surface owns it, so nothing else
        // would hand it its chance to get ready.  Without this, a sky read from an image never loads
        // the image.
        scene.Background.RenderingIsAboutToStart(context, null);

        HangTheSun(scene);

        Canvas = camera.Render(context, scene);
    }

    /// <summary>
    /// This method hands any sky light the sky it is to carry, which is the scene's own background
    /// unless it was given one of its own -- so that what lights the scene is what the scene shows.
    /// A pigment of its own needs its chance to get ready just as the background does.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="scene">The scene being readied.</param>
    private static void SettleTheSky(RenderContext context, Scene scene)
    {
        foreach (SkyLight sky in scene.Lights.OfType<SkyLight>())
        {
            if (sky.Pigment is null)
                sky.Pigment = scene.Background;
            else
                sky.Pigment.RenderingIsAboutToStart(context, null);
        }
    }

    /// <summary>
    /// This method gives the scene the sun that goes with a sky worked out from the air.
    /// <para>
    /// A physical sky knows where the sun stands, and knows what color it is by the time it reaches
    /// the ground, that being what the sky was worked out from.  Saying both of those in a scene file
    /// is saying the same thing twice, and the second saying is where they part company -- a white sun
    /// under a red sky being the commonest way to get one of these wrong.  So the sky supplies it,
    /// unless the scene wrote <c>no light</c>.
    /// </para>
    /// <para>
    /// It is added rather than substituted: a scene may have as many lights beside this one as it
    /// likes, so wanting a lamp of one's own is no reason to refuse this one.
    /// </para>
    /// </summary>
    /// <param name="scene">The scene being readied.</param>
    private static void HangTheSun(Scene scene)
    {
        if (scene.Background is PhysicalSkyPigment sky && sky.SunAsALight() is { } sun)
            scene.Lights.Add(sun);
    }

    /// <summary>
    /// This method ensures that all given surfaces have all relevant data finalized.
    /// This includes making sure a material is attached to all surfaces.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="scene">The scene the surfaces belong to.</param>
    /// <param name="surfaces">The list of surfaces to examine.</param>
    private static void FinalizeSurfaceData(
        RenderContext context, Scene scene, List<Surface> surfaces)
    {
        // Ambient stands in for light that has bounced about the scene, which this renderer does not
        // trace.  A sky light is the real thing that fudge was imitating, so a scene that has one wants
        // none of the fudge; one without it wants the tenth it has always had.  A material that named
        // its own ambient keeps it either way.
        double whenUnsaid = scene.Lights.OfType<SkyLight>().Any() ? 0 : 0.1;

        foreach (Surface surface in new SurfaceIterator(surfaces).Surfaces)
        {
            surface.Material ??= OrphanMaterial;
            surface.Material.Ambient ??= whenUnsaid;

            Pigment pigment = surface.Material.Pigment;

            pigment.Seed ??= surface.Seed;

            pigment.RenderingIsAboutToStart(context, surface);
        }
    }

    /// <summary>
    /// This method is used to get the scene to render.
    /// </summary>
    /// <param name="context">The current render context, which carries any command line name.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The scene to render.</returns>
    private Scene GetScene(RenderContext context, Variables variables)
    {
        List<Scene> scenes = HasExplicitScene
            ? Objects
                .Where(thing => thing is Scene)
                .Cast<Scene>()
                .ToList()
            : [CreateImplicitScene()];

        return IsolateObject(variables, scenes, context.SceneName, _sceneName, "scene");
    }

    /// <summary>
    /// This method is used to create a scene out of the root level objects when we don't
    /// have explicit scenes.
    /// </summary>
    /// <returns>The scene implied by our objects.</returns>
    private Scene CreateImplicitScene()
    {
        Scene scene = new Scene();

        foreach (object thing in Objects)
        {
            switch (thing)
            {
                case Camera camera:
                    scene.Cameras.Add(camera);
                    break;
                case Light light:
                    scene.Lights.Add(light);
                    break;
                case Surface surface:
                    scene.Surfaces.Add(surface);
                    break;
                case Pigment pigment:
                    scene.Background = pigment;
                    break;
                case SceneEnvironment environment:
                    scene.Environment = environment;
                    break;
                default:
                    throw new Exception($"Internal error: unknown object type: {thing.GetType().Name}");
            }
        }

        return scene;
    }

    /// <summary>
    /// This method is used to find the proper camera to use.
    /// </summary>
    /// <param name="context">The current render context, which carries any command line name.</param>
    /// <param name="scene">The scene to look for the camera.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The camera to use.</returns>
    private Camera GetCamera(RenderContext context, Scene scene, Variables variables)
    {
        return IsolateObject(variables, scene.Cameras, context.CameraName, _cameraName, "camera");
    }

    /// <summary>
    /// This is a helper method for isolating a specific item by default or by name from
    /// a list.
    /// </summary>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="items">The list of items to search.</param>
    /// <param name="overrideName">A name from the command line, which wins over the term when
    /// given.</param>
    /// <param name="nameTerm">The term to evaluate to derive the name of the desired item
    /// if there is a name.</param>
    /// <param name="noun">A noun to use for errors.</param>
    /// <returns>The desired item.</returns>
    private static TItem IsolateObject<TItem>(
        Variables variables, List<TItem> items, string overrideName, Term nameTerm, string noun)
        where TItem : NamedThing
    {
        string name = overrideName ?? nameTerm?.GetValue<string>(variables, false);

        if (name == null)
        {
            if (items.Count == 0)
                throw new Exception($"No {noun} was defined to render.");

            if (items.Count > 1)
            {
                throw new Exception(
                    $"No {noun} name specified to render, and more than one {noun} is defined.");
            }

            return items.First();
        }

        TItem item = items.FirstOrDefault(s => s.Name == name);

        if (item == null)
            throw new Exception($"No {noun} named '{name}' found to render.");

        return item;
    }
}
