using System.Diagnostics;
using RayTracer.Core;
using RayTracer.Extensions;
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
    /// <para>
    /// A fresh one each time rather than one shared instance, because this material is *written to*
    /// below -- its ambient is settled, and then scaled by whatever the scene asked for.  Settling with
    /// `??=` would survive being shared, since it happens once and stays; scaling would not, and a
    /// second render in the same process would scale what the first had already scaled.  That never
    /// shows from a command line, which renders one picture per process, and it shows immediately in a
    /// test suite or a batch.
    /// </para>
    /// </summary>
    private static Material NewOrphanMaterial => new ()
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

        // The things standing at the top of a scene are siblings as much as the children of a group
        // are, so anything put in terms of its neighbours is settled for them here.  A group does the
        // same for its own children, from inside its preparation just above.
        PlacementSettler.Settle(scene.Surfaces);

        // The scene's bodies and swarms are moved to where the frame's time finds them now, once everything else
        // stands where it will -- it is what they strike -- and before anything is sorted for searching.
        PhysicsSettler.Settle(scene, context, sampleTimes);

        // Joints that aim or reach, and spans, are settled once everything they might be asked about stands where it
        // will, bodies included, and before the scene is sorted for searching.
        KinematicsSettler.Settle(scene, sampleTimes);

        // What casts no shadow is settled only now, when every surface a ray can meet exists -- a
        // tube builds its segments as it is readied -- and the command line has had its say.
        ShadowSettler.Settle(scene.Surfaces, context.SuppressAllShadows);

        // Two things can only be settled once the scene is whole, since each depends on the company it
        // keeps rather than on anything written beside it.
        SettleTheSky(context, scene);
        FinalizeSurfaceData(context, scene, scene.Surfaces, WeatherSettler.Settle(scene.Surfaces));
        scene.Environment?.Medium?.TakeTheScenesCounts(context.MediumSamples, context.MediumBounces);

        scene.DiffuseBounces = context.DiffuseBounces;
        scene.DiffuseSamples = context.DiffuseSamples;
        scene.DiffuseCacheError = context.DiffuseCacheError;
        scene.CausticPhotons = context.CausticPhotons;
        scene.SubsurfaceSamples = context.SubsurfaceSamples;
        scene.WeatheringSamples = context.WeatheringSamples;
        scene.CausticsSpectral = context.Spectral;
        scene.TraceDepth = context.TraceDepth ?? 4;
        scene.Bailout = context.TraceDepth is null ? 0 : RenderContext.Bailout;

        // Last of all, and only now: the scene's own surfaces are sorted for searching.  It waits
        // until here because a box cannot be drawn round a surface before the surface knows its shape,
        // and one placed in terms of its neighbours does not stand where it will until they are
        // settled -- both of which have just happened above.
        scene.ArrangeSurfaces();

        // The sky is a pigment as much as any surface's is, and no surface owns it, so nothing else
        // would hand it its chance to get ready.  Without this, a sky read from an image never loads
        // the image.
        scene.Background.RenderingIsAboutToStart(context, null);

        HangTheSun(scene);
        LightTheGlowingThings(scene);

        // Lights standing on things are set out in the world only now, when every surface stands where
        // it will and the scene can be searched for what closes round each of them.
        LightPlacer.Place(scene, sampleTimes.Length);

        // What the scene holds is counted only now that the sky and anything glowing have added the
        // lights they bring, and the render is timed on its own, without the building before it.
        context.Statistics?.Describe(scene, context.Width, context.Height);

        Stopwatch stopwatch = Stopwatch.StartNew();

        // The photons of any caustics are sent before the first pixel, before anything that looks for
        // light, since glass that bends it stops a shadow ray once they have been.
        scene.ShootCausticPhotons();

        // The light among any hair is gathered before the first pixel too, so that it has every thread.
        scene.GatherTheLightAmongHair();

        // Bounced light that is cached is gathered before the first pixel too, in passes over the picture.
        camera.FillTheBounceCache(context, scene);

        Canvas = camera.Render(context, scene);

        context.Statistics?.AddRenderTime(stopwatch.Elapsed);
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
    /// <para>
    /// A blend of physical skies is the sky of a world with more than one sun, and hangs each of them,
    /// and clouds layered over a sky leave it its suns; see <see cref="PhysicalSkyPigment.SunsOf"/>.
    /// </para>
    /// </summary>
    /// <param name="scene">The scene being readied.</param>
    private static void HangTheSun(Scene scene)
    {
        scene.Lights.AddRange(PhysicalSkyPigment.SunsOf(scene.Background));
    }

    /// <summary>
    /// This method turns every surface that said its stuff should light the scene into a light.
    /// <para>
    /// It happens here, after the surfaces are prepared, because a volume light measures the stuff
    /// inside its surface when it is made -- and it cannot do that until the surface knows its own
    /// bounding box, which is settled by preparing.  It also happens after the materials are finalized,
    /// since a surface with no material of its own inherits one, and the medium is in the material.
    /// </para>
    /// <para>
    /// A surface that asked for this and has nothing to give -- no medium, or one that emits nothing,
    /// or one whose density came out at nought everywhere -- is passed over rather than complained
    /// about.  A fire that has gone out is a perfectly good thing to have in a scene, and it should
    /// cost nothing rather than being an error.
    /// </para>
    /// </summary>
    /// <param name="scene">The scene to light.</param>
    private static void LightTheGlowingThings(Scene scene)
    {
        foreach (Surface surface in new SurfaceIterator(scene.Surfaces).Surfaces)
        {
            if (surface.GivesLightSamples is not { } samples)
                continue;

            Medium medium = surface.Material?.Interior?.Medium;

            if (medium is null)
                continue;

            VolumeLight light = new (surface, medium) { Samples = samples };

            if (light.Lights)
                scene.Lights.Add(light);
        }
    }

    /// <summary>
    /// This method ensures that all given surfaces have all relevant data finalized.
    /// This includes making sure a material is attached to all surfaces.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="scene">The scene the surfaces belong to.</param>
    /// <param name="surfaces">The list of surfaces to examine.</param>
    private static void FinalizeSurfaceData(
        RenderContext context, Scene scene, List<Surface> surfaces, Dictionary<Weather, Surface> weathers = null)
    {
        // Ambient stands in for light that has bounced about the scene, which this renderer does not
        // trace unless asked.  A sky light is the real thing that fudge was imitating, and so is the light
        // a scene asks to be bounced, so a scene that has either wants none of the fudge; one without
        // them wants the tenth it has always had.  A material that named its own ambient keeps it either
        // way.
        double whenUnsaid = scene.Lights.OfType<SkyLight>().Any() || context.DiffuseBounces > 0 ? 0 : 0.1;

        // And then the scene may turn the whole lot up or down.  This is applied here, once the settling
        // above has run, so it reaches the materials that named their own ambient as well as the ones
        // that said nothing -- which is the entire point of it, since a curated library says something
        // almost everywhere and a scene-wide *value* could never reach those.
        //
        // Once per material rather than once per surface, and that is belt and braces rather than a fix
        // for anything observed: a `MaterialResolver` clones itself for each surface it dresses, so no
        // two surfaces hold the same `Material` today and multiplying per surface would be harmless.
        // It is written this way because `*=` compounds where the `??=` above does not -- if materials
        // ever come to be shared, scaling per surface would quietly darken a row of houses more than a
        // single house from the very same number, and nothing would report it.
        // A scene that follows bounced light for real wants none of the stand-in for it, not even the ambient
        // a material named for itself -- which a curated library does almost everywhere -- so all of it is
        // scaled to nothing; what glows of its own says `emission` instead.
        double ambientScale = context.DiffuseBounces > 0 ? 0 : context.AmbientScale;
        HashSet<Material> scaled = ambientScale.Near(1) ? null : [];
        Dictionary<Material, Material> orphaned = [];

        // The first undressed part of each shared shape, found once however many instances stand for it: a wood of
        // trees, every part of which has a material of its own, walked a whole tree for each of 145 thousand.
        Dictionary<Surface, Surface> undressed = new (ReferenceEqualityComparer.Instance);

        foreach (Surface surface in new SurfaceIterator(surfaces).Surfaces)
        {
            Settle(surface);

            // Hair with no material of its own wears the materials of what it grew from, which are
            // settled by now -- the very same ones, so they are not settled twice.
            if (surface.Hair is { } hair)
            {
                if (hair.Material is null)
                    hair.WearWhatItGrewFrom();
                else
                    Settle(hair);
            }
        }

        // What weather bares is a material like any other, readied on the first surface it was said on.
        foreach ((Weather weather, Surface surface) in weathers ?? [])
        {
            if (weather.WearTo is not { } bared)
                continue;

            bared.Ambient ??= whenUnsaid;
            bared.ReflectionSamples = context.ReflectionSamples;

            if (scaled is not null && scaled.Add(bared))
                bared.Ambient *= ambientScale;

            bared.Pigment.RenderingIsAboutToStart(context, surface);
        }

        return;

        void Settle(Surface surface)
        {
            // A surface with no material gets one of its own; one whose material only adds decals to
            // whatever it is handed, and was handed nothing, gets the same with its decals on top.
            // Either is noted, for a surface of a shared shape: where a ray meets it, it wears what its holders gave
            // that place instead (see Surface.MaterialThrough).
            if (surface.Material is null)
            {
                surface.WearsTheDefault = true;
                surface.OwnBeforeTheDefault = null;
                surface.Material = NewOrphanMaterial;
            }
            else if (surface.Material.InheritsAppearance)
            {
                surface.WearsTheDefault = true;
                surface.OwnBeforeTheDefault = surface.Material;
                surface.Material = Material.HandedDown(surface.Material, NewOrphanMaterial, orphaned);
            }

            surface.Material.Ambient ??= whenUnsaid;

            // How many ways a rough surface looks is the scene's to say, and is said here, once every context block
            // has had its say: copied in as each material was read, it was whatever the context said at that point
            // in the file, so a `reflection samples` written after the objects did nothing.
            surface.Material.ReflectionSamples = context.ReflectionSamples;

            // And how hard a medium works, the same way: copied in as it was read, a `medium samples` written after
            // the objects did nothing for them.
            surface.Material.Interior?.Medium?.TakeTheScenesCounts(context.MediumSamples, context.MediumBounces);

            if (scaled is not null && scaled.Add(surface.Material))
                surface.Material.Ambient *= ambientScale;

            // Now that the surface certainly has a material, it gets its say over the pigment on it.
            surface.MaterialIsSettled();

            // A material on an instance dresses the parts of its shared shape that were left undressed (see
            // Surface.MaterialThrough), which are settled before it.  So it is readied for the first of them, as it
            // was when it was handed down into them: an image takes its mapping from the shape it is on, and has
            // none for an instance.
            //
            // **A surface's seed is not put on its pigment here**, nor on a blend's pattern.  A pigment, or a blend, may
            // be worn by many surfaces -- a named one wherever it is named, a group's on every part -- and settled onto
            // it, the first surface's seed was everyone's.  Noise given no seed of its own draws on the seed of the
            // surface being read, as it is read; see Pigment.Wearer.
            Surface wearer = surface is Instance instance
                ? (undressed.TryGetValue(instance.Prototype, out Surface first)
                      ? first
                      : undressed[instance.Prototype] = FirstUndressedIn(instance)) ?? surface
                : surface;

            surface.Material.Pigment.RenderingIsAboutToStart(context, wearer);

            // A blend's materials are settled as the surface's own is, each of them.
            if (surface.Material.Blend is { } blend)
            {
                foreach (Material each in blend.AllMaterials)
                {
                    each.Ambient ??= whenUnsaid;
                    each.ReflectionSamples = context.ReflectionSamples;
                    each.Interior?.Medium?.TakeTheScenesCounts(context.MediumSamples, context.MediumBounces);

                    if (scaled is not null && scaled.Add(each))
                        each.Ambient *= ambientScale;

                    each.Pigment.RenderingIsAboutToStart(context, wearer);
                }
            }
        }
    }

    /// <summary>
    /// This method returns the first part of an instance's shared shape that wears whatever the instance is
    /// dressed in, or <c>null</c> if every part has a material of its own.
    /// </summary>
    /// <param name="instance">The instance to look inside.</param>
    /// <returns>The first undressed part, if any.</returns>
    private static Surface FirstUndressedIn(Instance instance)
    {
        return new SurfaceIterator([instance.Prototype]).Surfaces
            .FirstOrDefault(part => part.WearsTheDefault && part is not (Group or CsgSurface or Instance));
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
