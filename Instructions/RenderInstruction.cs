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
        using Scene scene = GetScene(variables);
        Camera camera = GetCamera(scene, variables);

        // Give each surface a chance to do any precomputing needed.
        foreach (Surface surface in scene.Surfaces)
            surface.PrepareForRendering();

        FinalizeSurfaceData(context, scene.Surfaces);
 
        Canvas = camera.Render(context, scene);
    }

    /// <summary>
    /// This method ensures that all given surfaces have all relevant data finalized.
    /// This includes making sure a material is attached to all surfaces.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="surfaces">The list of surfaces to examine.</param>
    private static void FinalizeSurfaceData(RenderContext context, List<Surface> surfaces)
    {
        foreach (Surface surface in new SurfaceIterator(surfaces).Surfaces)
        {
            surface.Material ??= OrphanMaterial;

            Pigment pigment = surface.Material.Pigment;

            pigment.Seed ??= surface.Seed;

            pigment.RenderingIsAboutToStart(context, surface);
        }
    }

    /// <summary>
    /// This method is used to get the scene to render.
    /// </summary>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The scene to render.</returns>
    private Scene GetScene(Variables variables)
    {
        List<Scene> scenes = HasExplicitScene
            ? Objects
                .Where(thing => thing is Scene)
                .Cast<Scene>()
                .ToList()
            : [CreateImplicitScene()];

        return IsolateObject(variables, scenes, _sceneName, "scene");
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
                case PointLight pointLight:
                    scene.Lights.Add(pointLight);
                    break;
                case Surface surface:
                    scene.Surfaces.Add(surface);
                    break;
                case Pigment pigment:
                    scene.Background = pigment;
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
    /// <param name="scene">The scene to look for the camera.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The camera to use.</returns>
    private Camera GetCamera(Scene scene, Variables variables)
    {
        return IsolateObject(variables, scene.Cameras, _cameraName, "camera");
    }

    /// <summary>
    /// This is a helper method for isolating a specific item by default or by name from
    /// a list.
    /// </summary>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="items">The list of items to search.</param>
    /// <param name="nameTerm">The term to evaluate to derive the name of the desired item
    /// if there is a name.</param>
    /// <param name="noun">A noun to use for errors.</param>
    /// <returns>The desired item.</returns>
    private static TItem IsolateObject<TItem>(
        Variables variables, List<TItem> items, Term nameTerm, string noun)
        where TItem : NamedThing
    {
        string name = nameTerm?.GetValue<string>(variables, false);

        if (name == null)
        {
            if (items.Count != 1)
                throw new Exception($"No {noun} name specified to render and multiple {noun}as exist.");

            return items.First();
        }

        TItem item = items.FirstOrDefault(s => s.Name == name);

        if (item == null)
            throw new Exception($"No {noun} named '{name}' found to render.");

        return item;
    }
}
