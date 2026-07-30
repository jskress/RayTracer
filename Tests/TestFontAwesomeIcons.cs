using System.IO.Compression;
using RayTracer.Graphics;

namespace Tests;

/// <summary>
/// These tests cover reading a FontAwesome icon's outline out of a zip: how the <c>style:name</c>
/// specification names an entry, and what happens when the specification, the entry or the SVG is
/// not what it should be.  They read from a small zip of their own rather than the installed one.
/// </summary>
[TestClass]
public class TestFontAwesomeIcons
{
    private const string HeartData = "M0 0 L10 0 L10 10 Z";
    private const string StarData = "M5 0 L6 4 L10 4 L7 6 Z";
    private const string StarFullData = "M5 0 L5.5 3 L6 4 L8 4 L10 4 L7 6 Z";
    private const string MoonData = "M2 2 L8 2 L8 8 L2 8 Z";

    private string _zipPath;

    [TestInitialize]
    public void CreateZip()
    {
        _zipPath = Path.Combine(Path.GetTempPath(), $"fa-test-{Guid.NewGuid():N}.zip");

        using ZipArchive archive = ZipFile.Open(_zipPath, ZipArchiveMode.Create);

        // The real FontAwesome download nests its icons under a version-named folder, so most
        // entries sit under one here; a lone root-level entry (moon) checks that layout resolves
        // too.  The star is carried in both the fuller "svgs-full" and the trimmed "svgs", to show
        // the fuller one is favored; heart and moon are in "svgs" alone, to show the fall back.
        const string root = "fontawesome-free-7.3.1-desktop";

        Add(archive, $"{root}/svgs/regular/heart.svg", Svg(HeartData));
        Add(archive, $"{root}/svgs/solid/star.svg", Svg(StarData));
        Add(archive, $"{root}/svgs-full/solid/star.svg", Svg(StarFullData));
        Add(archive, $"{root}/svgs/solid/blank.svg",
            "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");
        Add(archive, "svgs/regular/moon.svg", Svg(MoonData));
    }

    [TestCleanup]
    public void RemoveZip()
    {
        if (_zipPath != null && File.Exists(_zipPath))
            File.Delete(_zipPath);
    }

    private static string Svg(string data) =>
        $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 512 512\"><path d=\"{data}\"/></svg>";

    private static void Add(ZipArchive archive, string name, string content)
    {
        using StreamWriter writer = new (archive.CreateEntry(name).Open());

        writer.Write(content);
    }

    [TestMethod]
    public void TestASingleNameTakesTheRegularStyle()
    {
        Assert.AreEqual(HeartData, FontAwesomeIcons.ReadPathData("heart", _zipPath));
    }

    [TestMethod]
    public void TestTheFullerOutlineIsFavored()
    {
        // The star is in both "svgs-full" and "svgs"; the fuller one wins, for the most detail.
        Assert.AreEqual(StarFullData, FontAwesomeIcons.ReadPathData("solid:star", _zipPath));
    }

    [TestMethod]
    public void TestAnIconInSvgsAloneIsStillFound()
    {
        // The web download has no "svgs-full", so an icon carried only in "svgs" -- whether under
        // the version folder (heart) or at a bare root (moon) -- must still resolve.
        Assert.AreEqual(HeartData, FontAwesomeIcons.ReadPathData("heart", _zipPath));
        Assert.AreEqual(MoonData, FontAwesomeIcons.ReadPathData("moon", _zipPath));
    }

    [TestMethod]
    public void TestAMissingIconIsReported()
    {
        Exception exception = Assert.ThrowsExactly<Exception>(
            () => FontAwesomeIcons.ReadPathData("solid:heart", _zipPath));

        Assert.IsTrue(exception.Message.Contains("solid/heart"));
    }

    [TestMethod]
    public void TestAMissingZipIsReported()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"no-such-{Guid.NewGuid():N}.zip");

        Exception exception = Assert.ThrowsExactly<Exception>(
            () => FontAwesomeIcons.ReadPathData("heart", missing));

        Assert.IsTrue(exception.Message.Contains(missing));
    }

    [TestMethod]
    public void TestAMalformedSpecificationIsRejected()
    {
        // Too many parts, or an empty part on either side, is not a valid icon name.
        foreach (string bad in new[] { "a:b:c", ":heart", "heart:", "", ":" })
        {
            Assert.ThrowsExactly<Exception>(
                () => FontAwesomeIcons.ReadPathData(bad, _zipPath),
                $"'{bad}' should not be a valid icon name");
        }
    }

    [TestMethod]
    public void TestAnIconWithNoOutlineIsReported()
    {
        Exception exception = Assert.ThrowsExactly<Exception>(
            () => FontAwesomeIcons.ReadPathData("solid:blank", _zipPath));

        Assert.IsTrue(exception.Message.Contains("no path outline"));
    }
}
