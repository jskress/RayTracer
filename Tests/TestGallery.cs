namespace Tests;

/// <summary>
/// These tests keep the gallery's pictures tied to the scenes that make them.
/// <para>
/// Every scene under <c>gallery/</c> keeps its render beside it, and those renders are the first
/// thing anyone sees of this ray tracer.  Nothing about them is checked by building or testing, so
/// when the renderer's output changes they go stale in silence -- and worse, a picture that cannot
/// be made again from the repository is not merely stale but unrecoverable.  Both have happened:
/// three scenes carried images their own scene files no longer produced, and a fourth needed an
/// antialiasing setting that was written down nowhere at all, so re-rendering it "correctly" would
/// have quietly thrown away the picture's smooth edges.
/// </para>
/// <para>
/// <c>gallery/sources.txt</c> is what makes them re-renderable, and this test keeps that file
/// honest.  It is the same arrangement <c>TestDocumentation</c> holds over the figures under
/// <c>docs/</c>, for the same reason.
/// </para>
/// </summary>
[TestClass]
public class TestGallery
{
    private static string GalleryDirectory =>
        Path.Combine(TestDocumentation.RepositoryRoot, "gallery");

    /// <summary>
    /// Every gallery scene that keeps a picture beside it must be listed, with the size to render
    /// it at and whatever else its command line needs.
    /// <para>
    /// **This test cannot see that a picture has gone stale.**  Knowing that would mean rendering
    /// the whole gallery, which is about an hour and has no business in a test run.  What it can do
    /// is keep the *means* of noticing from rotting: a scene added without a source line, a picture
    /// re-rendered at the wrong size, or a listed scene renamed out from under its entry is caught
    /// here, and re-rendering the lot is one loop, spelled out at the top of that file.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestEverySceneRecordsHowItsPictureIsMade()
    {
        string manifest = Path.Combine(GalleryDirectory, "sources.txt");

        Assert.IsTrue(File.Exists(manifest),
            $"{manifest} is missing, so nothing records how the gallery is rendered");

        Dictionary<string, (int Width, int Height)> listed = [];
        List<string> problems = [];

        foreach (string line in File.ReadAllLines(manifest))
        {
            string text = line.Trim();

            if (text.Length == 0 || text.StartsWith('#'))
                continue;

            // The flags field is allowed to be empty, which is the common case.
            string[] parts = text.Split('|');

            if (parts.Length != 4 || !int.TryParse(parts[1], out int width) ||
                !int.TryParse(parts[2], out int height))
            {
                problems.Add($"  {text} -- should read scene|width|height|flags");

                continue;
            }

            listed[parts[0]] = (width, height);
        }

        foreach ((string scene, (int width, int height)) in listed)
        {
            if (!File.Exists(Path.Combine(TestDocumentation.RepositoryRoot, scene)))
            {
                problems.Add($"  {scene} is listed but there is no such scene");

                continue;
            }

            string picture = Path.Combine(
                TestDocumentation.RepositoryRoot, Path.ChangeExtension(scene, ".png"));

            if (!File.Exists(picture))
            {
                problems.Add($"  {scene} is listed but keeps no picture beside it");

                continue;
            }

            (int actualWidth, int actualHeight) = TestDocumentation.SizeOfPng(picture);

            if (actualWidth != width || actualHeight != height)
            {
                problems.Add(
                    $"  {scene} is listed as {width}x{height} but its picture is really " +
                    $"{actualWidth}x{actualHeight}");
            }
        }

        foreach (string picture in Directory.EnumerateFiles(
                     GalleryDirectory, "*.png", SearchOption.AllDirectories))
        {
            string scene = Path.ChangeExtension(picture, ".igl");

            if (!File.Exists(scene))
                continue;

            string relative = Path.GetRelativePath(TestDocumentation.RepositoryRoot, scene)
                .Replace(Path.DirectorySeparatorChar, '/');

            if (!listed.ContainsKey(relative))
            {
                problems.Add(
                    $"  {relative} is not listed, so nothing records how to make its picture again");
            }
        }

        Assert.AreEqual(0, problems.Count,
            "the gallery and the file that records how to render it do not agree:\n" +
            string.Join('\n', problems));
    }
}
