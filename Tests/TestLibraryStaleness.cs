using System.Reflection;
using RayTracer.Parser;

namespace Tests;

/// <summary>
/// This test covers the one thing the library installer cannot afford to be quiet about: that the
/// libraries carried inside the assembly are the ones in the repository it was built from.
/// <para>
/// A library ships as an embedded resource, so installing one copies it out of the assembly and
/// never off disk.  Edit a library, install without building, and the older text is written out over
/// the edit while the command reports success.  Nothing errors and nothing is said.  That cost real
/// time here: three separate measurements of the trees library were taken against a build that did
/// not contain the change being measured, and every one of them read as "this makes no difference".
/// </para>
/// </summary>
[TestClass]
public class TestLibraryStaleness
{
    /// <summary>
    /// Walks up from wherever the tests are running to find the root of the repository.
    /// </summary>
    private static string RepositoryRoot
    {
        get
        {
            DirectoryInfo directory = new (AppContext.BaseDirectory);

            while (directory is not null &&
                   !File.Exists(Path.Combine(directory.FullName, "RayTracer.csproj")))
                directory = directory.Parent;

            Assert.IsNotNull(directory, "could not find the repository root");

            return directory.FullName;
        }
    }

    [TestMethod]
    public void TestTheAssemblyCarriesTheLibrariesTheRepositoryHolds()
    {
        // Running at all means the test project has just been built, so the two should agree.  When
        // they do not, the build is behind the source -- and every library-backed test in this suite
        // is quietly testing the older text.
        Assembly assembly = typeof(LibraryLocator).Assembly;
        string prefix = $"{assembly.GetName().Name}.Libraries.";
        List<string> stale = [];

        foreach (string resource in assembly.GetManifestResourceNames()
                     .Where(name => name.StartsWith(prefix) && name.EndsWith(".igl")))
        {
            string name = resource[prefix.Length..];
            string path = Path.Combine(RepositoryRoot, "Libraries", name);

            if (!File.Exists(path))
                continue;

            using Stream stream = assembly.GetManifestResourceStream(resource);
            using StreamReader reader = new (stream!);

            if (reader.ReadToEnd() != File.ReadAllText(path))
                stale.Add(name);
        }

        Assert.IsEmpty(stale,
            $"the assembly's copy of these libraries differs from the repository's: " +
            $"{string.Join(", ", stale.Order())}.  The build is behind the source.");
    }
}
