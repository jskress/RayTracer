namespace RayTracer.Geometry;

/// <summary>
/// This class settles which surfaces cast no shadow, once the scene is built and ready.
/// <para>
/// **A shadow ray asks the surface it crossed, and that is always a leaf** -- the sphere inside a
/// union, a segment inside a tube.  So a `no shadow` written on a group, or on anything made of
/// other surfaces, said nothing to the surfaces a ray actually meets, and they went on casting
/// shadows; it is handed down to everything inside here instead.  A tube's segments are only built
/// when it is readied, which is one reason this waits until then.
/// </para>
/// <para>
/// **The other reason is the command line.**  `--no-shadows` reaches the render's context just
/// before it renders, long after every surface was made; read as each surface was made, as it used
/// to be, it came too late to touch any of them and did nothing at all.  A scene's own
/// `context { no shadows }` was read the same way, so it missed whatever was made ahead of it in
/// the file, and every tube.  Both are applied here, to the whole scene, which is what they say.
/// </para>
/// <para>
/// **A shared shape is left to its instances.**  Its one copy stands behind every instance of it,
/// so it cannot take on what one instance says without imposing it on all of them; an instance
/// that casts no shadow keeps the mark itself, and the shadow test asks it as well as the surface.
/// Only a scene that casts no shadows anywhere marks the shared shape.
/// </para>
/// </summary>
internal static class ShadowSettler
{
    /// <summary>
    /// This method settles every surface in a scene.
    /// </summary>
    /// <param name="surfaces">The scene's surfaces, all of them readied.</param>
    /// <param name="noShadowsAnywhere">Whether nothing in the scene casts a shadow.</param>
    internal static void Settle(IEnumerable<Surface> surfaces, bool noShadowsAnywhere)
    {
        HashSet<Surface> shared = [];

        foreach (Surface surface in surfaces)
            Settle(surface, noShadowsAnywhere, noShadowsAnywhere, shared);
    }

    /// <summary>
    /// This method settles one surface and everything it holds.
    /// </summary>
    /// <param name="surface">The surface to settle.</param>
    /// <param name="noShadow">Whether something holding this surface casts no shadow.</param>
    /// <param name="noShadowsAnywhere">Whether nothing in the scene casts a shadow.</param>
    /// <param name="shared">The shared shapes already settled, since each is reached once per
    /// instance of it.</param>
    private static void Settle(
        Surface surface, bool noShadow, bool noShadowsAnywhere, HashSet<Surface> shared)
    {
        noShadow |= surface.NoShadow;
        surface.NoShadow = noShadow;

        switch (surface)
        {
            case Group group:
                foreach (Surface child in group.Surfaces)
                    Settle(child, noShadow, noShadowsAnywhere, shared);
                break;
            case CsgSurface csgSurface:
                Settle(csgSurface.Left, noShadow, noShadowsAnywhere, shared);
                Settle(csgSurface.Right, noShadow, noShadowsAnywhere, shared);
                break;
            case Tube { Root: not null } tube:
                Settle(tube.Root, noShadow, noShadowsAnywhere, shared);
                break;
            case Instance instance when shared.Add(instance.Prototype):
                Settle(instance.Prototype, noShadowsAnywhere, noShadowsAnywhere, shared);
                break;
        }
    }
}
