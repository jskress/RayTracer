namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This enumeration notes all the supported turtle-style commands that may be used in
/// converting an L-system production into some form of geometry.
/// </summary>
public enum TurtleCommand
{
    Unknown,
    Move,
    DrawLine,
    TurnLeft,
    TurnRight,
    PitchUp,
    PitchDown,
    RollLeft,
    RollRight,
    TurnAround,
    ToVertical,
    StartBranch,
    CompleteBranch
}
