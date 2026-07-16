using Lex.Clauses;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to build the dispatcher that routes each top-level clause we
    /// parse to whichever method knows how to handle it, based on its tag.
    /// </summary>
    /// <returns>The clause dispatcher to use for top-level clauses.</returns>
    private ClauseDispatcher CreateDispatcher()
    {
        return new ClauseDispatcher()
            .On(nameof(HandleStartContextClause), _ => HandleStartContextClause())
            .On(nameof(HandleStartSceneClause), _ => HandleStartSceneClause())
            .On(nameof(HandleStartCameraClause), HandleStartCameraClause)
            .On(nameof(HandleStartPointLightClause), HandleStartPointLightClause)
            .On(nameof(HandleStartPlaneClause), HandleStartPlaneClause)
            .On(nameof(HandleStartSphereClause), HandleStartSphereClause)
            .On(nameof(HandleStartCubeClause), HandleStartCubeClause)
            .On(nameof(HandleStartCylinderClause), HandleStartCylinderClause)
            .On(nameof(HandleStartConicClause), HandleStartConicClause)
            .On(nameof(HandleStartTorusClause), HandleStartTorusClause)
            .On(nameof(HandleStartEggClause), HandleStartEggClause)
            .On(nameof(HandleStartExtrusionClause), HandleStartExtrusionClause)
            .On(nameof(HandleStartLatheClause), HandleStartLatheClause)
            .On(nameof(HandleStartBlobClause), HandleStartBlobClause)
            .On(nameof(HandleStartTubeClause), HandleStartTubeClause)
            .On(nameof(HandleStartSweepClause), HandleStartSweepClause)
            .On(nameof(HandleStartTextClause), HandleStartTextClause)
            .On(nameof(HandleStartLSystemClause), HandleStartLSystemClause)
            .On(nameof(HandleStartHeightFieldClause), HandleStartHeightFieldClause)
            .On(nameof(HandleStartTriangleClause), HandleStartTriangleClause)
            .On(nameof(HandleStartSmoothTriangleClause), HandleStartSmoothTriangleClause)
            .On(nameof(HandleStartParallelogramClause), HandleStartParallelogramClause)
            .On(nameof(HandleStartDiscClause), HandleStartDiscClause)
            .On(nameof(HandleStartGenericShapeClause), HandleStartGenericShapeClause)
            .On(nameof(HandleStartObjectFileClause), HandleStartObjectFileClause)
            .On(nameof(HandleStartObjectClause), HandleStartObjectClause)
            .On(nameof(HandleStartCsgClause), HandleStartCsgClause)
            .On(nameof(HandleStartGroupClause), HandleStartGroupClause)
            .On(nameof(HandleBackgroundClause), HandleBackgroundClause)
            .On(nameof(HandleRenderClause), HandleRenderClause)
            .On(nameof(HandleSetThingToVariableClause), HandleSetThingToVariableClause)
            .On(nameof(HandleSetVariableClause), HandleSetVariableClause)
            .OnUnhandled(clause => throw new Exception($"No handler registered for the {clause.Tag} tag."));
    }
}
