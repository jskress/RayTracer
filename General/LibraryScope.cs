namespace RayTracer.General;

/// <summary>
/// This class holds the names a library was carried out among, for the calls written in it.
/// <para>
/// A scene that imports a value is handed the value's recipe, and makes it for itself, among its own names -- which
/// is right for whatever the scene adds to it, a loop's counter or a value of the scene's own.  But a call the recipe
/// holds, of a primitive or a function the library kept back, names something only the library has.  Made among the
/// scene's names, it found nothing ("nothing named lump is a primitive"), or found something else of that name the
/// scene had made for itself.
/// </para>
/// <para>
/// So every call read while a library is being read is stamped with that library's scope, which is filled in when
/// the import is carried out, and looks for what it calls there first -- where it was written -- and only then
/// where it is being made.  What it is given, and anything the scene laid over it, are still worked out where it is
/// made.
/// </para>
/// </summary>
public class LibraryScope
{
    [ThreadStatic] private static LibraryScope _beingRead;

    /// <summary>
    /// This property holds the scope of the library being read on this thread, or <c>null</c> while a scene is: what
    /// a call read now is stamped with.  It is the parser's to set, around each library it reads.  A function call is
    /// built by the expression parser, which is shared and knows nothing of which file it is reading, so it has to be
    /// told this way.
    /// </summary>
    internal static LibraryScope BeingRead
    {
        get => _beingRead;
        set => _beingRead = value;
    }

    /// <summary>
    /// This property holds the names the library was carried out among, or <c>null</c> until its import has been.
    /// Each time the scene is carried out -- once for every frame of an animation -- the import runs again and this is
    /// set afresh.
    /// </summary>
    internal Variables Variables { get; set; }

    /// <summary>
    /// This method finds what a call names: among the names of the library it was written in, if it was written in
    /// one and that library has been carried out, and otherwise among the names where it is being made.
    /// </summary>
    /// <param name="writtenIn">The scope of the library the call was written in, or <c>null</c>.</param>
    /// <param name="variables">The names where the call is being made.</param>
    /// <param name="name">The name the call gives.</param>
    /// <param name="type">The type of thing called.</param>
    /// <returns>What the name holds of that type, or <c>null</c>.</returns>
    internal static object Find(LibraryScope writtenIn, Variables variables, string name, Type type)
    {
        return writtenIn?.Variables?.GetValue(name, type) ?? variables.GetValue(name, type);
    }
}
