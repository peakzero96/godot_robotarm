using Godot;
using Grasp.Logger;
using Grasp.Robot;
using Grasp.BoxWall;
using Grasp.Workflow;
using System.Text.Json;

namespace Grasp.Main;

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; } = null!;

    private Node3D? _worldRoot;

    public override void _Ready()
    {
        Instance = this;

        // Wait one frame for AutoLoad singletons to be ready
        CallDeferred(MethodName.Initialize);
    }

    private void Initialize()
    {
        Logger.Logger.Instance.Info("GameManager", "Initializing...");

        // Find WorldRoot in the scene tree (GameManager is an AutoLoad, so we need
        // to search from the scene root)
        var sceneRoot = GetTree().CurrentScene;
        if (sceneRoot != null)
        {
            _worldRoot = sceneRoot.FindChild("WorldRoot", true, false) as Node3D;
        }

        if (_worldRoot == null)
        {
            Logger.Logger.Instance.Error("GameManager", "WorldRoot not found!");
            return;
        }

        // Set world root for managers
        RobotController.Instance.SetWorldRoot(_worldRoot);
        BoxWallManager.Instance.SetWorldRoot(_worldRoot);
        BoxAttachController.Instance.SetWorldRoot(_worldRoot);

        // Load robot
        RobotController.Instance.LoadRobot();

        // Load test box wall
        LoadTestBoxWall();

        // Create grab workflow controller
        var workflow = new GrabWorkflowController();
        _worldRoot.AddChild(workflow);

        Logger.Logger.Instance.Info("GameManager", "Initialization complete");
    }

    private void LoadTestBoxWall()
    {
        // var filePath = "res://dataset/box_position.txt";
        // using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
        // ... original test code removed

        LoadBoxWallFromFile("res://dataset/box_position.txt");
    }

    private void LoadBoxWallFromFile(string filePath)
    {
        using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            Logger.Logger.Instance.Error("GameManager", $"Failed to open: {filePath}");
            return;
        }

        string line = file.GetLine();
        if (string.IsNullOrEmpty(line)) return;

        string[] parts = line.Split(';');
        // parts[0]: ignore (1100)
        // parts[1]: box count
        // parts[2..]: 11 fields per box: x,y,z, quat_a,quat_b,quat_c,quat_d, H,W,D,reserved
        int boxCount = int.Parse(parts[1]);

        var boxes = new System.Collections.Generic.List<object>();

        for (int i = 0; i < boxCount; i++)
        {
            string[] f = parts[i + 2].Split(',');

            // positions are in mm, convert to meters
            float x = float.Parse(f[0]) / 1000f;
            float y = -float.Parse(f[1]) / 1000f;
            float z = -float.Parse(f[2]) / 1000f;

            // quaternion (x, y, z, w)
            float qa = float.Parse(f[3]);
            float qb = float.Parse(f[4]);
            float qc = float.Parse(f[5]);
            float qd = float.Parse(f[6]);

            // size in mm → meters
            float width = float.Parse(f[7]);
            float height = float.Parse(f[8]);
            float depth = 0.2f; // uniform depth

            // 180° around X axis to match y=-y, z=-z
            var frameQuat = Quaternion.FromEuler(new Vector3(Mathf.Pi, 0, 0));
            var quat = (frameQuat * new Quaternion(qa, qb, qc, qd).Normalized()).Normalized();
            var euler = quat.GetEuler();

            boxes.Add(new
            {
                id = i,
                position = new { x, y, z },
                rotation_deg = new
                {
                    x = Mathf.RadToDeg(euler.X),
                    y = Mathf.RadToDeg(euler.Y),
                    z = Mathf.RadToDeg(euler.Z)
                },
                size = new { x = width, y = height, z = depth },
                color = "#C4A882"
            });
        }

        var wallData = new
        {
            version = "1.0",
            wall_id = "real_box_wall",
            boxes
        };

        string json = JsonSerializer.Serialize(wallData);
        BoxWallManager.Instance.LoadBoxWall(json);

        Logger.Logger.Instance.Info("GameManager", $"Loaded {boxCount} boxes from {filePath}");
    }
}
