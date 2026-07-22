using System.Reflection;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;

namespace Tests;

/// <summary>
/// These tests cover the indices of refraction a scene may name.
/// <para>
/// They are set as variables by reflecting over the constants, so adding one to the class is all
/// it takes to put a name in a scene's hands.  Several of them share a name with a color, which
/// is what these are mostly here to pin down: a variable holds a value for each type rather than
/// one value, so the two live side by side and which is meant is settled by where the name is
/// used.
/// </para>
/// </summary>
[TestClass]
public class TestIndicesOfRefraction
{
    /// <summary>
    /// Returns the names the indices of refraction will be set under, found the same way the
    /// class itself finds them.
    /// </summary>
    private static List<string> IndexNames() => typeof(IndicesOfRefraction)
        .GetFields(BindingFlags.Static | BindingFlags.Public)
        .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(double))
        .Select(field => field.Name)
        .ToList();

    [TestMethod]
    public void TestEveryConstantBecomesAName()
    {
        Variables variables = new ();

        IndicesOfRefraction.AddToVariables(variables);

        foreach (string name in IndexNames())
            Assert.IsTrue(variables.ContainsKey(name), $"{name} was not set as a variable");
    }

    [TestMethod]
    public void TestAnIndexAndAColorOfTheSameNameBothSurvive()
    {
        // Turquoise, aquamarine, coral, ivory and quartz are all both.  Registering the indices
        // after the colors does not cost the colors anything, since the two are kept apart by
        // type rather than piled onto one name.
        Variables variables = new ();

        Colors.AddToVariables(variables);
        IndicesOfRefraction.AddToVariables(variables);

        foreach (string name in new[] { "Aquamarine", "Coral", "Ivory", "Quartz", "Turquoise" })
        {
            Assert.IsInstanceOfType<Color>(variables.GetValue(name, typeof(Color)),
                $"the color named {name} was lost");
            Assert.IsInstanceOfType<double>(variables.GetValue(name, typeof(double)),
                $"the index named {name} was lost");
        }
    }

    [TestMethod]
    public void TestTheOrderTheyAreRegisteredInDoesNotMatter()
    {
        Variables colorsFirst = new ();
        Variables indicesFirst = new ();

        Colors.AddToVariables(colorsFirst);
        IndicesOfRefraction.AddToVariables(colorsFirst);

        IndicesOfRefraction.AddToVariables(indicesFirst);
        Colors.AddToVariables(indicesFirst);

        Assert.AreEqual(
            (double) colorsFirst.GetValue("Turquoise", typeof(double)),
            (double) indicesFirst.GetValue("Turquoise", typeof(double)));
        Assert.AreEqual(
            colorsFirst.GetValue("Turquoise", typeof(Color)),
            indicesFirst.GetValue("Turquoise", typeof(Color)));
    }

    [TestMethod]
    public void TestTheStonesAndGlassesFromPovRayAreThere()
    {
        Variables variables = new ();

        IndicesOfRefraction.AddToVariables(variables);

        // A handful worth spot-checking against POV-Ray's own ior.inc, one from each of the three
        // groups the values were taken from.
        foreach ((string name, double expected) in new (string, double)[]
                 {
                     ("CrownGlass", 1.51673), ("FlintGlass", 1.78446), ("Emerald", 1.58),
                     ("Ruby", 1.766), ("Moissanite", 2.67), ("AndraditeGarnet", 1.888)
                 })
        {
            Assert.IsTrue(variables.ContainsKey(name), $"{name} is not in scope");
            Assert.AreEqual(expected, (double) variables.GetValue(name), 1e-9, name);
        }
    }
}
