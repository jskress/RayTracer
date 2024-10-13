using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a torus value.
/// </summary>
public class ObjectFileResolver : SurfaceResolver<Group>, IValidatable
{
    /// <summary>
    /// This property holds the reference directory that will be used to resolve the name
    /// of the file to read.
    /// one.
    /// </summary>
    public string Directory { get; set; }

    /// <summary>
    /// This property holds the resolver for the file name property of our object file.
    /// </summary>
    public Resolver<string> FileNameResolver { get; set; }

    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override Group Resolve(RenderContext context, Variables variables)
    {
        string path = FileNameResolver.Resolve(context, variables);

        path = Path.GetFullPath(Path.Combine(Directory, path));

        ObjectFileParser objectFileParser = new (fileName: path);

        Group group = objectFileParser.Parse();

        SetProperties(context, variables, group);

        return group;
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        return FileNameResolver is null
            ? "The \"source\" property is required."
            : null;
    }
}
