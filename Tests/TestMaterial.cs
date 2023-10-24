using RayTracer.ColorSources;
using RayTracer.Core;
using RayTracer.Graphics;

namespace Tests;

[TestClass]
public class TestMaterial
{
    [TestMethod]
    public void TestConstruction()
    {
        Material material = new ();

        Assert.AreSame(SolidColorSource.White, material.ColorSource);
        Assert.AreEqual(0.1, material.Ambient);
        Assert.AreEqual(0.9, material.Diffuse);
        Assert.AreEqual(0.9, material.Specular);
        Assert.AreEqual(200.0, material.Shininess);
        Assert.AreEqual(0, material.Reflective);
        Assert.AreEqual(0, material.Transparency);
        Assert.AreEqual(1, material.IndexOfRefraction);
    }
}
