using Lex.Clauses;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle a clause that specifies the assignment of a value to
    /// a variable
    /// </summary>
    private void HandleSetVariableClause(Clause clause)
    {
        string name = clause.Text();
        Term term = clause.Term();

        _context.InstructionContext.AddInstruction(new SetVariableInstruction(name, term: term));
    }

    /// <summary>
    /// This method is used to handle a clause that specifies the assignment of a value to
    /// a variable
    /// </summary>
    private void HandleSetThingToVariableClause(Clause clause)
    {
        string name = clause.Text();
        string type = clause.Text(2);
        string second = clause.Text(3);

        clause.Tokens.RemoveRange(0, 2);

        IObjectResolver resolver = type switch
        {
            "pigment" => ParsePigmentClause(),
            "material" => GetMaterialResolver(clause),
            "transform" => GetTransformResolver(clause),
            "plane" => ParsePlaneClause(clause),
            "sphere" => ParseSphereClause(clause),
            "cube" => ParseCubeClause(clause),
            "cylinder" => ParseCylinderClause(clause),
            "conic" => ParseConicClause(clause),
            "torus" => ParseTorusClause(clause),
            "extrusion" => ParseExtrusionClause(clause),
            "text" => ParseTextClause(clause),
            "triangle" => ParseTriangleClause(clause),
            "smooth" when second == "triangle" => ParseSmoothTriangleClause(clause),
            "parallelogram" => ParseParallelogramClause(clause),
            "object" when second == "file" => ParseObjectFileClause(clause),
            "union" or "difference" or "intersection" or "csg" => ParseCsgClause(clause),
            "group" => ParseGroupClause(clause),
            _ => null
        };

        if (resolver != null)
        {
            _context.InstructionContext.AddInstruction(new SetVariableInstruction(name, objectResolver: resolver));

            if (resolver is ICloneable cloneable)
                _context.ExtensibleItems[name] = cloneable;
        }
    }

    /// <summary>
    /// This is a helper method that finds an extensible item and, if necessary,
    /// creating a copy of it.
    /// </summary>
    /// <param name="name">The name of the item to gain a copy of.</param>
    /// <param name="copy">A flag noting whether we should return a copy of the item.</param>
    /// <returns>A copy of the named item or <c>null</c>, if the item does not exist or is
    /// not of the appropriate type.</returns>
    private TValue GetExtensibleItem<TValue>(Token name, bool copy)
        where TValue : class, ICloneable
    {
        TValue result = _context.ExtensibleItems.TryGetValue(name.Text, out ICloneable cloneable) &&
            cloneable is TValue value
            ? copy ? (TValue) value.Clone() : value
            : null;

        if (result == null)
        {
            throw new TokenException($"The variable, {name.Text} is not defined or is not the proper type.")
            {
                Token = name
            };
        }

        return result;
    }
}
