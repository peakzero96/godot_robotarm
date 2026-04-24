using Godot;
using Grasp.Logger;
using Grasp.Robot;
using Grasp.BoxWall;
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

        // Load robot
        RobotController.Instance.LoadRobot();

        // Load test box wall
        LoadTestBoxWall();

        Logger.Logger.Instance.Info("GameManager", "Initialization complete");
    }

    private void LoadTestBoxWall()
    {
        float baseX = 0f;
        float baseY = 0.3f;
        float baseZ = 0f;
        float boxW = 0.25f;
        float boxH = 0.6f;
        float boxD = 0.4f;
        float gap = 0.01f;

        var boxes = new System.Collections.Generic.List<object>();
        int id = 0;

        for (int layer = 0; layer < 3; layer++)
        {
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    boxes.Add(new
                    {
                        id = id++,
                        position = new
                        {
                            x = baseX + col * (boxW + gap),
                            y = baseY + layer * (boxH + gap),
                            z = baseZ + row * (boxD + gap)
                        },
                        rotation_deg = new { x = 0.0, y = 0.0, z = 0.0 },
                        size = new { x = boxW, y = boxH, z = boxD },
                        color = "#C4A882"
                    });
                }
            }
        }

        var wallData = new
        {
            version = "1.0",
            wall_id = "test_wall",
            boxes = boxes
        };

        string json = JsonSerializer.Serialize(wallData);
        BoxWallManager.Instance.LoadBoxWall(json);
    }
}
