using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This class carries out an import: it runs a library's definitions in a scope of the library's own
/// and hands the scene only the names it asked for.
/// <para>
/// The scope is the whole point, and the reason is worth stating because the obvious way fails.  Simply
/// throwing away the definitions a scene did not ask for throws away the ones its exports are built
/// out of: withhold a library's <c>taper</c> and the <c>elm</c> that leans on it stops working, because
/// both were declared into the one set of names a render has.  A library needs names of its own, which
/// is the same lexical scoping a function body already gets from where it was <i>written</i> -- and
/// once the library has them, keeping a name back costs its exports nothing, since they go on looking
/// where they were written rather than where they were used.
/// </para>
/// <para>
/// The library's scope is a child of the scene's, so a library may lean on what the scene set up before
/// the import, the way an included file can.  Nothing goes the other way except what was published.
/// </para>
/// <para>
/// <b>Not everything can be kept back, and the line falls where it must.</b>  A function and a
/// primitive each remember the scope they were written in, so one the scene did not ask for may be
/// held back without the ones it did ask for losing sight of it.  A value or a thing -- a color, a
/// material, a surface -- remembers nothing: it is looked up wherever it is used, and a material
/// published to a scene is resolved among the <i>scene's</i> names.  Hold back the color such a
/// material was written against and it finds nothing at render time.  So those are handed over whole,
/// as they always have been, and only what carries its own scope is filtered.
/// </para>
/// </summary>
public class ImportInstruction : Instruction
{
    /// <summary>
    /// This property holds the definitions the library makes.
    /// </summary>
    public InstructionContext Library { get; init; }

    /// <summary>
    /// This property holds the names the scene is to be given.
    /// </summary>
    public IReadOnlyCollection<string> Published { get; init; }

    /// <summary>
    /// This method is used to execute the instruction.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        Variables scope = new (variables);

        Library.CarryOutDefinitions(context, scope);

        scope.PublishTo(variables, Published);
    }
}
