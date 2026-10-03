using System.IO.Compression;
using System.Reflection;
using RayTracer.Commands;
using RayTracer.Parser;

namespace Tests;

/// <summary>
/// These tests cover the parts of the <c>libraries</c> verb that write into the user's own directory:
/// installing the shipped libraries, removing one, and installing a FontAwesome zip.
/// <para>
/// **What they hold the verb to is <c>--dry-run</c>'s one promise: that it writes nothing.**  The flag
/// is accepted alongside every action, and for a long time only the two kinds of import honored it --
/// <c>--install --overwrite --dry-run</c> rewrote every library in the user's set and reported each
/// one as installed, which is the opposite of what a person asking that question wanted to learn.  So
/// each dry run here is paired with the same call made for real, which has to write: a verb that
/// simply never wrote anything would pass the dry runs, and the pairs are what keep it honest.
/// </para>
/// </summary>
[TestClass]
public class TestLibrariesCommand
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"libraries-command-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// The names of the libraries the assembly carries, as an install writes them.
    /// </summary>
    private static string[] ShippedFileNames()
    {
        Assembly assembly = typeof(LibraryLocator).Assembly;
        string prefix = $"{assembly.GetName().Name}.Libraries.";

        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix) && name.EndsWith(".igl"))
            .Select(name => name[prefix.Length..])
            .Order()
            .ToArray();
    }

    [TestMethod]
    public void TestAnInstallWritesEveryShippedLibrary()
    {
        // The control for the dry runs below: made for real, the same call fills the directory.
        string libraries = Path.Combine(_directory, "Libraries");

        LibrariesCommand.InstallShippedLibraries(libraries, overwrite: false, dryRun: false);

        string[] written = Directory.GetFiles(libraries).Select(Path.GetFileName).Order().ToArray();

        Assert.IsNotEmpty(written, "an install wrote nothing at all");
        CollectionAssert.AreEqual(ShippedFileNames(), written);
    }

    [TestMethod]
    public void TestADryRunInstallWritesNothing()
    {
        // Not a library, and not even the directory they would go in.
        string libraries = Path.Combine(_directory, "Libraries");

        LibrariesCommand.InstallShippedLibraries(libraries, overwrite: true, dryRun: true);

        Assert.IsFalse(Directory.Exists(libraries), "a dry-run install created the library directory");
    }

    [TestMethod]
    public void TestADryRunInstallLeavesALibraryYouHaveAlone()
    {
        // The case the flag exists for: what would --overwrite replace?  Asking must not replace it.
        string libraries = Path.Combine(_directory, "Libraries");
        string mine = Path.Combine(libraries, ShippedFileNames()[0]);
        const string tuned = "// tuned by hand, and not to be lost\n";

        Directory.CreateDirectory(libraries);
        File.WriteAllText(mine, tuned);

        LibrariesCommand.InstallShippedLibraries(libraries, overwrite: true, dryRun: true);

        Assert.AreEqual(tuned, File.ReadAllText(mine), "a dry-run install replaced a library already there");
        Assert.HasCount(1, Directory.GetFiles(libraries), "a dry-run install wrote libraries beside it");

        // And made for real, the same call does replace it.
        LibrariesCommand.InstallShippedLibraries(libraries, overwrite: true, dryRun: false);

        Assert.AreNotEqual(tuned, File.ReadAllText(mine), "an install with --overwrite did not replace it");
    }

    [TestMethod]
    public void TestADryRunRemoveRemovesNothing()
    {
        string library = Path.Combine(_directory, "golds.igl");

        File.WriteAllText(library, "Gold = color [1, 0.8, 0.3]\n");

        LibrariesCommand.RemoveLibrary(_directory, "golds", dryRun: true);

        Assert.IsTrue(File.Exists(library), "a dry-run remove removed the library");

        LibrariesCommand.RemoveLibrary(_directory, "golds", dryRun: false);

        Assert.IsFalse(File.Exists(library), "a remove left the library where it was");
    }

    [TestMethod]
    public void TestADryRunFontAwesomeInstallWritesNothing()
    {
        // The smallest zip the verb will accept: an svgs folder, wrapped as the download wraps it.
        string zip = Path.Combine(_directory, "fontawesome-free.zip");
        string target = Path.Combine(_directory, "installed", "fontawesome.zip");

        using (ZipArchive archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            ZipArchiveEntry entry = archive.CreateEntry("fontawesome-free-6/svgs/solid/star.svg");

            using StreamWriter writer = new (entry.Open());

            writer.Write("<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M0 0L1 1\"/></svg>");
        }

        LibrariesCommand.InstallFontAwesomeZip(zip, target, dryRun: true);

        Assert.IsFalse(File.Exists(target), "a dry-run FontAwesome install wrote the zip");
        Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(target)),
            "a dry-run FontAwesome install created the directory it would go in");

        LibrariesCommand.InstallFontAwesomeZip(zip, target, dryRun: false);

        Assert.IsTrue(File.Exists(target), "a FontAwesome install did not write the zip");
    }
}
