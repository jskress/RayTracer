namespace RayTracer.Geometry;

/// <summary>
/// This class represents what a shape grammar built: a group of the walls, ledges, roofs and placed things its
/// rules put in their scopes.  The grammar is carried out when the scene is read, not when it is rendered, since
/// nothing it does needs anything prepared; what is here by then is an ordinary group.
/// </summary>
public class ShapeGrammar : Group;
