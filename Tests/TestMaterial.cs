using RayTracer.Core;
using RayTracer.Pigments;

namespace Tests;

[TestClass]
public class TestMaterial
{
    [TestMethod]
    public void TestConstruction()
    {
        Material material = new ();

        Assert.AreSame(SolidPigment.White, material.Pigment);

        // Ambient is the one property with nothing in it to begin with, because what it ought to be
        // depends on the scene the material ends up in: a scene with a sky light has the real thing
        // ambient was standing in for and wants none of the fudge, while one without it wants the tenth
        // it has always had.  Until a scene settles that, the material behaves as it always did.
        Assert.IsNull(material.Ambient);
        Assert.AreEqual(0.1, material.EffectiveAmbient);
        Assert.AreEqual(0.9, material.Diffuse);
        Assert.AreEqual(0.9, material.Specular);
        Assert.AreEqual(200.0, material.Shininess);
        Assert.AreEqual(0, material.Reflective);
        Assert.AreEqual(0, material.Transparency);
        Assert.AreEqual(1, material.Interior.IndexOfRefraction);
    }
}
