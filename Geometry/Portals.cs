namespace RayTracer.Geometry;

/// <summary>
/// This struct holds the instances a crossing was found through, innermost first: the one whose shared
/// shape holds the surface struck, then the one whose shared shape holds that instance, and so on out to
/// the scene.
/// <para>
/// **A shared shape has no parent, and that is the whole difficulty.**  Everything that carries a point
/// or a normal between the world and a surface walks up from the surface through its parents, and a
/// shape reachable from a dozen places cannot say which of them it stands in.  The crossing knows,
/// because it came through particular instances; each stamps it on the way out, and a walk that runs out
/// of parents at the top of a shared shape carries on through the next of them.
/// </para>
/// <para>
/// **One instance costs nothing; only nesting allocates.**  A crossing through a single instance holds
/// that instance and nothing more, exactly as crossings always did.  Only one found through a shared
/// shape inside another -- a leaf cluster inside an oak inside a wood -- needs the chain of those beyond
/// it, and the chain is built only as the outer instances stamp it.
/// </para>
/// </summary>
public readonly struct Portals : IEquatable<Portals>
{
    /// <summary>
    /// This property holds the innermost instance, or <c>null</c> for a crossing found through none.
    /// </summary>
    public Surface Through { get; }

    /// <summary>
    /// This property holds the instances beyond the innermost, innermost first, or <c>null</c> for none.
    /// </summary>
    public PortalChain Beyond { get; }

    /// <summary>
    /// This constructor records the instances a crossing was found through.
    /// </summary>
    /// <param name="through">The innermost instance.</param>
    /// <param name="beyond">The instances beyond it, innermost first.</param>
    public Portals(Surface through, PortalChain beyond = null)
    {
        Through = through;
        Beyond = beyond;
    }

    /// <summary>
    /// This property notes that a crossing was found through no instance at all.
    /// </summary>
    public bool IsEmpty => Through is null;

    /// <summary>
    /// This property holds the instances that remain once the innermost has been passed through.
    /// </summary>
    public Portals Outward => Beyond is null ? default : new Portals(Beyond.Through, Beyond.Beyond);

    /// <summary>
    /// This property holds the outermost instance stamped so far, or <c>null</c> for none.  While a
    /// crossing is still on its way out it is the one nearest whoever is asking: a combination filtering
    /// its own crossings sees the instance that is its own member, since any further out have yet to stamp.
    /// </summary>
    public Surface Outermost
    {
        get
        {
            if (Beyond is null)
                return Through;

            PortalChain chain = Beyond;

            while (chain.Beyond is not null)
                chain = chain.Beyond;

            return chain.Through;
        }
    }

    /// <summary>
    /// This property notes that some instance the crossing came through was told to cast no shadow.
    /// </summary>
    public bool CastsNoShadow
    {
        get
        {
            for (Portals portals = this; !portals.IsEmpty; portals = portals.Outward)
            {
                if (portals.Through.NoShadow)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// This method returns these portals with one more instance outside them all: the instance a crossing
    /// is passing out through now.
    /// </summary>
    /// <param name="instance">The instance stamping the crossing.</param>
    /// <returns>The portals, with the instance added outermost.</returns>
    public Portals And(Surface instance)
    {
        return IsEmpty ? new Portals(instance) : new Portals(Through, PortalChain.Append(Beyond, instance));
    }

    /// <summary>
    /// This method orders two sets of portals by the instances in them, innermost first, so that two
    /// crossings of one shared shape in two different places never tie.
    /// </summary>
    /// <param name="other">The portals to compare against.</param>
    /// <returns>Less than nought, nought or more than nought, as these come before, with or after.</returns>
    public int CompareTo(Portals other)
    {
        Portals one = this;
        Portals two = other;

        while (true)
        {
            if (ReferenceEquals(one.Through, two.Through))
            {
                if (one.IsEmpty)
                    return 0;

                one = one.Outward;
                two = two.Outward;

                continue;
            }

            return one.IsEmpty ? -1
                : two.IsEmpty ? 1
                : one.Through.Ordinal.CompareTo(two.Through.Ordinal);
        }
    }

    /// <summary>
    /// This method reports whether two sets of portals hold the same instances in the same order.
    /// </summary>
    /// <param name="other">The portals to compare against.</param>
    /// <returns><c>true</c>, if they are the same.</returns>
    public bool Equals(Portals other)
    {
        Portals one = this;
        Portals two = other;

        while (!one.IsEmpty || !two.IsEmpty)
        {
            if (!ReferenceEquals(one.Through, two.Through))
                return false;

            one = one.Outward;
            two = two.Outward;
        }

        return true;
    }

    /// <summary>
    /// This method reports whether an object is the same set of portals as these.
    /// </summary>
    /// <param name="obj">The object to compare against.</param>
    /// <returns><c>true</c>, if it is.</returns>
    public override bool Equals(object obj)
    {
        return obj is Portals other && Equals(other);
    }

    /// <summary>
    /// This method returns a hash of the instances, in order.
    /// </summary>
    /// <returns>The hash.</returns>
    public override int GetHashCode()
    {
        HashCode hash = new ();

        for (Portals portals = this; !portals.IsEmpty; portals = portals.Outward)
            hash.Add(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(portals.Through));

        return hash.ToHashCode();
    }

    /// <summary>
    /// This operator reports whether two sets of portals are the same.
    /// </summary>
    public static bool operator ==(Portals left, Portals right) => left.Equals(right);

    /// <summary>
    /// This operator reports whether two sets of portals differ.
    /// </summary>
    public static bool operator !=(Portals left, Portals right) => !left.Equals(right);
}

/// <summary>
/// This class holds one link of the instances beyond the innermost a crossing was found through.  It is
/// never changed once made: an outer instance stamping a crossing makes a new chain rather than altering
/// the one it was handed, which a copy of the crossing may share.
/// </summary>
/// <param name="through">The instance.</param>
/// <param name="beyond">The instances beyond it, or <c>null</c> for none.</param>
public sealed class PortalChain(Surface through, PortalChain beyond)
{
    /// <summary>
    /// This property holds the instance.
    /// </summary>
    public Surface Through { get; } = through;

    /// <summary>
    /// This property holds the instances beyond it, innermost first, or <c>null</c> for none.
    /// </summary>
    public PortalChain Beyond { get; } = beyond;

    /// <summary>
    /// This method returns a chain with one more instance at its outer end.
    /// </summary>
    /// <param name="chain">The chain so far, or <c>null</c> for none.</param>
    /// <param name="instance">The instance to add.</param>
    /// <returns>The longer chain.</returns>
    internal static PortalChain Append(PortalChain chain, Surface instance)
    {
        return chain is null
            ? new PortalChain(instance, null)
            : new PortalChain(chain.Through, Append(chain.Beyond, instance));
    }
}
