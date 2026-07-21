namespace RayTracer.Parser;

/// <summary>
/// This class knows where libraries of reusable definitions live, so that a scene may import from
/// one by name rather than by working out a path to it.
/// </summary>
public static class LibraryLocator
{
    /// <summary>
    /// This property holds the directory the ray tracer keeps its libraries in.  It sits beside the
    /// font catalog, under the same dot directory in the user's profile, since the two are the same
    /// sort of thing: material the ray tracer brings with it rather than material a scene supplies.
    /// </summary>
    public static readonly string LibrariesDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".rayTracer", "Libraries");

    /// <summary>
    /// This method works out which file an import names, or returns <c>null</c> if there is no such
    /// file to be found.
    /// <para>
    /// A name is looked for beside the scene first and among the shipped libraries second, so that
    /// a scene may keep a library of its own alongside it and, where it wants to, put one of its
    /// own in front of one that came with the ray tracer.  Either may be named with or without the
    /// <c>.igl</c> extension, since a library is better named for what it holds than for how it is
    /// stored.
    /// </para>
    /// </summary>
    /// <param name="name">The name the scene gave.</param>
    /// <param name="sceneDirectory">The directory holding the file doing the importing.</param>
    /// <returns>The full path of the library, or <c>null</c> if it could not be found.</returns>
    public static string Find(string name, string sceneDirectory)
    {
        foreach (string directory in new[] { sceneDirectory, LibrariesDirectory })
        {
            string given = Path.GetFullPath(Path.Combine(directory, name));

            foreach (string candidate in new[] { given, given + ".igl" })
            {
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }
}
