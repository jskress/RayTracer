using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

/// <summary>
/// These tests cover the order two crossings come out in when they are the same distance away.
/// <para>
/// A tie is commoner than it sounds: two solids meeting exactly at a face, or a cube subtracted from
/// another so their sides are coplanar.  Something has to decide which crossing is first, because
/// which is first decides which surface gets shaded, and a CSG surface walks the list in order
/// toggling whether it is inside each half.
/// </para>
/// <para>
/// What decided it before was nothing at all.  <c>CompareTo</c> compared distance alone, so ties
/// compared equal, and <c>List.Sort</c> is not a stable sort -- equal keys come out in whatever order
/// the sort's own bookkeeping happens to leave them.  The picture therefore depended on the order the
/// crossings were handed in, which is to say on how the geometry was walked.  That was not a theory:
/// reversing the walk over a group's children moved two pixels of one gallery scene, and arranging
/// those children into a tree of boxes moved two others.
/// </para>
/// </summary>
[TestClass]
public class TestIntersectionOrdering
{
    [TestMethod]
    public void TestDistanceStillDecidesWhenItDiffers()
    {
        Sphere near = new ();
        Sphere far = new ();
        Intersection first = new (near, 1);
        Intersection second = new (far, 2);

        Assert.IsTrue(first.CompareTo(second) < 0);
        Assert.IsTrue(second.CompareTo(first) > 0);
    }

    [TestMethod]
    public void TestATieGoesToTheSurfaceBuiltFirst()
    {
        // Built in this order, so this is the order they must come out in.
        Sphere earlier = new ();
        Sphere later = new ();
        Intersection fromEarlier = new (earlier, 5);
        Intersection fromLater = new (later, 5);

        Assert.IsTrue(fromEarlier.CompareTo(fromLater) < 0,
            "the surface built first should sort first at an equal distance");
        Assert.IsTrue(fromLater.CompareTo(fromEarlier) > 0,
            "and the comparison has to say so both ways round");
    }

    [TestMethod]
    public void TestATieSortsTheSameWhicheverOrderItArrivesIn()
    {
        // The whole point.  The same two crossings, handed to the sorter both ways round, have to come
        // out the same way.  With distance as the only key this passes or fails on the mood of the
        // sort.
        Sphere earlier = new ();
        Sphere later = new ();
        Intersection fromEarlier = new (earlier, 5);
        Intersection fromLater = new (later, 5);

        List<Intersection> oneWay = [fromEarlier, fromLater];
        List<Intersection> theOther = [fromLater, fromEarlier];

        oneWay.Sort();
        theOther.Sort();

        Assert.AreSame(earlier, oneWay[0].Surface);
        Assert.AreSame(earlier, theOther[0].Surface);
        Assert.AreSame(later, oneWay[1].Surface);
        Assert.AreSame(later, theOther[1].Surface);
    }

    [TestMethod]
    public void TestALongListOfTiesSortsTheSameHoweverItIsShuffled()
    {
        // Two crossings can come out right by luck.  Thirty cannot, and thirty is where an unstable
        // sort actually starts rearranging things: below a certain length the runtime falls back to an
        // insertion sort, which happens to be stable, so a small case can pass while the real one
        // fails.
        List<Sphere> spheres = [];

        for (int index = 0; index < 30; index++)
            spheres.Add(new Sphere());

        List<Intersection> inOrder = spheres
            .Select(sphere => new Intersection(sphere, 3))
            .ToList();
        List<Intersection> shuffled = ShuffledCopy(inOrder);

        inOrder.Sort();
        shuffled.Sort();

        for (int index = 0; index < inOrder.Count; index++)
        {
            Assert.AreSame(inOrder[index].Surface, shuffled[index].Surface,
                $"crossing {index} came out differently for a different arrival order");
            Assert.AreSame(spheres[index], inOrder[index].Surface,
                $"crossing {index} is not the {index}th surface built");
        }
    }

    [TestMethod]
    public void TestTwoCrossingsOfOneSurfaceAtOneDistanceAreEqual()
    {
        // A tangent hit can be reported twice at the same distance on the same surface.  There is
        // nothing to choose between those and the comparison should say so rather than inventing an
        // order.
        Sphere sphere = new ();
        Intersection one = new (sphere, 4);
        Intersection two = new (sphere, 4);

        Assert.AreEqual(0, one.CompareTo(two));
        Assert.AreEqual(0, two.CompareTo(one));
    }

    [TestMethod]
    public void TestTheOrderIsTotalSoSortingIsWellDefined()
    {
        // A comparison used for sorting has to be consistent: if a is before b and b before c then a
        // must be before c.  Distance alone is not, once ties are involved, and a sort handed an
        // inconsistent comparison is entitled to do anything at all.
        Sphere first = new ();
        Sphere second = new ();
        Sphere third = new ();
        Intersection a = new (first, 5);
        Intersection b = new (second, 5);
        Intersection c = new (third, 5);

        Assert.IsTrue(a.CompareTo(b) < 0);
        Assert.IsTrue(b.CompareTo(c) < 0);
        Assert.IsTrue(a.CompareTo(c) < 0, "the order is not transitive");
    }

    /// <summary>
    /// Rearranges a copy by a fixed pattern rather than at random, so a failure here can be looked
    /// into rather than merely seen once.
    /// </summary>
    private static List<Intersection> ShuffledCopy(List<Intersection> source)
    {
        List<Intersection> copy = [..source];

        for (int index = 0; index < copy.Count; index++)
        {
            int with = (index * 7 + 3) % copy.Count;

            (copy[index], copy[with]) = (copy[with], copy[index]);
        }

        return copy;
    }
}
