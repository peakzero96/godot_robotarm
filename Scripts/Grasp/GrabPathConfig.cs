using Godot;

namespace Grasp.Workflow;

public struct GrabPathConfig
{
    public float TcpOffset;
    public Basis TcpRotationOffset;
    public float ApproachDistance;
    public Vector3 LiftDirection;
    public float LiftDistance;
    public Vector3 PlacePosition;
    public Quaternion PlaceRotation;
}
