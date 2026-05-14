using Godot;

namespace Grasp.Workflow;

public enum WaypointKind
{
    BoxCenter,
    GrabSurface,
    Approach,
    Lift,
    Place
}

public struct WaypointPose
{
    public WaypointKind Kind;
    public Vector3 Position;
    public Quaternion Orientation;
    public Basis Basis;
}
