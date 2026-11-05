using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve an object file: a Wavefront model, its materials read from the libraries it names,
/// any of them put back by the scene's own, and the rest of a surface's properties laid on as for any group.
/// </summary>
public class ObjectFileResolver : SurfaceResolver<Group>, IValidatable
{
    /// <summary>
    /// This property holds the folder the scene was read from, which the model's file is found from.
    /// </summary>
    public string Directory { get; set; }

    /// <summary>
    /// This property holds the resolver for the model's file.
    /// </summary>
    public Resolver<string> FileNameResolver { get; set; }

    /// <summary>
    /// This property holds the scene's materials to put in place of the model's own, by the name the model gives them.
    /// </summary>
    public List<(Resolver<string> Name, Resolver<Material> Material)> Replacements { get; set; } = [];

    /// <summary>
    /// This method reads the model.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The model, as a group.</returns>
    public override Group Resolve(RenderContext context, Variables variables)
    {
        string path = FileNameResolver.Resolve(context, variables);

        path = Path.GetFullPath(Path.Combine(Directory, path));

        ObjectFileParser objectFileParser = new (fileName: path)
        {
            Replacements = Replacements.ToDictionary(
                pair => pair.Name.Resolve(context, variables), pair => pair.Material.Resolve(context, variables))
        };

        Group group = objectFileParser.Parse();

        foreach (string name in Replacements.Select(pair => pair.Name.Resolve(context, variables)))
        {
            if (!objectFileParser.Materials.ContainsKey(name))
            {
                throw new Exception(
                    $"The model {Path.GetFileName(path)} has no material named '{name}' to replace; its materials are " +
                    $"{(objectFileParser.Materials.Count == 0 ? "none" : string.Join(", ", objectFileParser.Materials.Keys.Select(key => $"'{key}'")))}.");
            }
        }

        if (objectFileParser.Unread.Count > 0)
        {
            Terminal.ShowWarning(
                $"{Path.GetFileName(path)}: not read: {string.Join(", ", objectFileParser.Unread)}.");
        }

        SetProperties(context, variables, group);

        return group;
    }

    /// <summary>
    /// This method creates a copy of this resolver, its list of replacements its own.
    /// </summary>
    /// <returns>The copy.</returns>
    public override object Clone()
    {
        ObjectFileResolver resolver = (ObjectFileResolver) base.Clone();

        resolver.Replacements = [..resolver.Replacements];

        return resolver;
    }

    public string Validate()
    {
        return FileNameResolver is null
            ? "The \"source\" property is required."
            : null;
    }
}
