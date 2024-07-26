using RayTracer.Core;
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

/// <summary>
/// This class is used to create planes.
/// </summary>
public class PlaneInstructionSet : ObjectInstructionSet<Plane>;

/// <summary>
/// This class is used to create spheres.
/// </summary>
public class SphereInstructionSet : ObjectInstructionSet<Sphere>;
