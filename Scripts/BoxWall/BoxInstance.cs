using Godot;
using Grasp.Workflow;

namespace Grasp.BoxWall;

public enum BoxState
{
    Waiting,
    Targeted,
    Grabbed,
    Released
}

public class BoxInstance
{
    public int Id { get; set; }
    // Position Box的朝向机械臂的面心，由视觉算法获取
    public Vector3 Position { get; set; }
    public Quaternion RotationQuat { get; set; }
    public Vector3 Size { get; set; }
    public Color Color { get; set; }
    public BoxState State { get; set; } = BoxState.Waiting;
    public int MultiMeshIndex { get; set; }
    public GrabPath? GrabPath { get; set; }
    // MessCenter Box的质心，即内部中心
    public Vector3 MessCenter {get; set;}
}
