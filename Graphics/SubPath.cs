namespace RayTracer.Graphics;

/// <summary>
/// This class wraps a <see cref="GeneralPath"/> to represent a subpath. It carries a
/// list of subpaths that are contained by this subpath.
/// </summary>
public class SubPath
{
    /// <summary>
    /// This property holds the one run of a larger path that this subpath stands for.
    /// </summary>
    public GeneralPath Path { get; init; }

    /// <summary>
    /// This property holds the subpaths this one contains, each of which may contain
    /// subpaths of its own.
    /// </summary>
    public List<SubPath> ContainedPaths { get; } = [];

    /// <summary>
    /// This is a helper method for reporting whether this subpath contains the specified
    /// one.
    /// </summary>
    /// <param name="subPath">The subpath to test.</param>
    /// <returns><code>true</code>, if the subpath is contained by this one, or <code>false</code>,
    /// if not.</returns>
    public bool Contains(SubPath subPath)
    {
        return Path.Contains(subPath.Path);
    }
}
