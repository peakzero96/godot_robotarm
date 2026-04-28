using Godot;
using Grasp.Logger;

namespace Grasp.Robot;

public partial class RobotController : Node
{
    public static RobotController Instance { get; private set; } = null!;

    [Signal]
    public delegate void RobotChangedEventHandler(string robotName, int jointCount);

    private Node3D? _robotRoot;
    private JointPivot[] _joints = System.Array.Empty<JointPivot>();
    private Node3D? _gripper;
    private string _robotName = "";
    private Node3D? _worldRoot;

    public int JointCount => _joints.Length;
    public JointPivot[] Joints => _joints;
    public Node3D? Gripper => _gripper;
    public string RobotName => _robotName;
    public Node3D? RobotRoot => _robotRoot;

    public override void _Ready()
    {
        Instance = this;
    }

    public void SetWorldRoot(Node3D worldRoot)
    {
        _worldRoot = worldRoot;
    }

    public bool LoadRobot(string? robotPath = null)
    {
        robotPath ??= Grasp.Main.AppConfig.Instance.RobotPath;

        // Remove existing robot
        if (_robotRoot != null)
        {
            _robotRoot.QueueFree();
            _robotRoot = null;
            _joints = System.Array.Empty<JointPivot>();
            _gripper = null;
        }

        var result = RobotLoader.Load(robotPath);
        if (result == null)
        {
            Logger.Logger.Instance.Error("RobotController", $"Failed to load robot from: {robotPath}");
            return false;
        }

        _robotRoot = result.RootNode;
        _joints = result.Joints;
        _gripper = result.Gripper;
        _robotName = result.RobotName;

        if (_worldRoot != null)
        {
            _worldRoot.AddChild(_robotRoot);
            _robotRoot.Position = new Vector3(0, 0, 0);
            _robotRoot.Rotation = new Vector3(-1.57f, 0, 0);
        }

        EmitSignal(SignalName.RobotChanged, _robotName, _joints.Length);

        Logger.Logger.Instance.Info("RobotController",
            $"Robot loaded: {_robotName}, {_joints.Length} joints");
        return true;
    }

    public void SetJointAngles(float[] angles)
    {
        for (int i = 0; i < Mathf.Min(angles.Length, _joints.Length); i++)
        {
            _joints[i].SetAngle(angles[i]);
        }
    }

    public float[] GetJointAngles()
    {
        var angles = new float[_joints.Length];
        for (int i = 0; i < _joints.Length; i++)
        {
            angles[i] = _joints[i].GetAngle();
        }
        return angles;
    }
}
