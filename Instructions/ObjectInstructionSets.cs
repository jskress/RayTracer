using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to create scenes.
/// </summary>
public class SceneInstructionSet : ObjectInstructionSet<Scene>;

/// <summary>
/// This class is used to create cameras.
/// </summary>
public class CameraInstructionSet : ObjectInstructionSet<Camera>;

/// <summary>
/// This class is used to create point lights.
/// </summary>
public class PointLightInstructionSet : ObjectInstructionSet<PointLight>;

/// <summary>
/// This class is used to create materials.
/// </summary>
public class MaterialInstructionSet : ObjectInstructionSet<Material>;

public class SurfaceInstructionSet<TObject> : ObjectInstructionSet<TObject>
    where TObject : Surface, new()
{
    /// <summary>
    /// This method may be used by subclasses to perform any initialization on our created
    /// object that is needed.
    /// </summary>
    /// <param name="context">The current render context.</param>
    protected override void InitObject(RenderContext context)
    {
        CreatedObject.NoShadow = context.SuppressAllShadows;
    }
}

/// <summary>
/// This class is used to create planes.
/// </summary>
public class PlaneInstructionSet : SurfaceInstructionSet<Plane>;

/// <summary>
/// This class is used to create spheres.
/// </summary>
public class SphereInstructionSet : SurfaceInstructionSet<Sphere>;

/// <summary>
/// This class is used to create cubes.
/// </summary>
public class CubeInstructionSet : SurfaceInstructionSet<Cube>;

/// <summary>
/// This class is used to create cylinders.
/// </summary>
public class CylinderInstructionSet : SurfaceInstructionSet<Cylinder>;

/// <summary>
/// This class is used to create conics.
/// </summary>
public class ConicInstructionSet : SurfaceInstructionSet<Conic>;
