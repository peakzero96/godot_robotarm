using Godot;

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
    public Vector3 Position { get; set; }
    public Vector3 RotationDeg { get; set; }
    public Vector3 Size { get; set; }
    public Color Color { get; set; }
    public BoxState State { get; set; } = BoxState.Waiting;
    public int MultiMeshIndex { get; set; }
}
