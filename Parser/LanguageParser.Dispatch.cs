using Lex.Clauses;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL
/// </summary>
public partial class LanguageParser
{
    private readonly Dictionary<string, Action> _blockStartHandlers = new ();
    private readonly Dictionary<string, Action<Clause>> _clauseHandlers = new ();

    /// <summary>
    /// This method is used to fill up our dispatch tables.
    /// </summary>
    private void FillDispatchTables()
    {
        // Block starters.
        _blockStartHandlers.Add(nameof(HandleStartContextClause), HandleStartContextClause);
        _blockStartHandlers.Add(nameof(HandleStartSceneClause), HandleStartSceneClause);

        // Clause handlers.
        _clauseHandlers.Add(nameof(HandleStartCameraClause), HandleStartCameraClause);
        _clauseHandlers.Add(nameof(HandleStartPointLightClause), HandleStartPointLightClause);
        _clauseHandlers.Add(nameof(HandleStartPlaneClause), HandleStartPlaneClause);
        _clauseHandlers.Add(nameof(HandleStartSphereClause), HandleStartSphereClause);
        _clauseHandlers.Add(nameof(HandleStartCubeClause), HandleStartCubeClause);
        _clauseHandlers.Add(nameof(HandleStartCylinderClause), HandleStartCylinderClause);
        _clauseHandlers.Add(nameof(HandleStartConicClause), HandleStartConicClause);
        _clauseHandlers.Add(nameof(HandleStartTorusClause), HandleStartTorusClause);
        _clauseHandlers.Add(nameof(HandleStartExtrusionClause), HandleStartExtrusionClause);
        _clauseHandlers.Add(nameof(HandleStartTriangleClause), HandleStartTriangleClause);
        _clauseHandlers.Add(nameof(HandleStartSmoothTriangleClause), HandleStartSmoothTriangleClause);
        _clauseHandlers.Add(nameof(HandleStartParallelogramClause), HandleStartParallelogramClause);
        _clauseHandlers.Add(nameof(HandleStartObjectFileClause), HandleStartObjectFileClause);
        _clauseHandlers.Add(nameof(HandleStartObjectClause), HandleStartObjectClause);
        _clauseHandlers.Add(nameof(HandleStartCsgClause), HandleStartCsgClause);
        _clauseHandlers.Add(nameof(HandleStartGroupClause), HandleStartGroupClause);
        _clauseHandlers.Add(nameof(HandleBackgroundClause), HandleBackgroundClause);
        _clauseHandlers.Add(nameof(HandleSetThingToVariableClause), HandleSetThingToVariableClause);
        _clauseHandlers.Add(nameof(HandleSetVariableClause), HandleSetVariableClause);
    }

    /// <summary>
    /// This method is used to process the given top-level clause.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    /// <param name="blockTag">The name, if any, of the owning block clause.</param>
    private void ProcessClause(Clause clause, string blockTag)
    {
        string tag = DetermineTag(clause, blockTag);

        if (tag.StartsWith("HandleStart") && _blockStartHandlers.TryGetValue(tag, out Action action))
            action();
        else if (_clauseHandlers.TryGetValue(tag, out Action<Clause> clauseAction))
            clauseAction(clause);
        else
            throw new Exception($"No handler registered for the {tag} tag.");
    }

    /// <summary>
    /// This is a helper method for ensuring we have a clause tag.
    /// </summary>
    /// <param name="clause">The clause to start with.</param>
    /// <param name="blockTag">The name of the owning block clause.</param>
    /// <returns></returns>
    private static string DetermineTag(Clause clause, string blockTag)
    {
        string tag = clause.Tag;

        return string.IsNullOrEmpty(tag)
            ? $"Handle{blockTag[..1].ToUpperInvariant()}{blockTag[1..]}"
            : tag;
    }
}
