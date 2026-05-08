using Godot;
using Grasp.Logger;
using System.Collections.Generic;

namespace Grasp.BoxWall;

public partial class BoxWallManager : Node
{
    public static BoxWallManager Instance { get; private set; } = null!;

    [Signal]
    public delegate void BoxWallLoadedEventHandler(int totalCount);

    [Signal]
    public delegate void BoxStateChangedEventHandler(int boxId, BoxState newState);

    private MultiMeshInstance3D? _meshInstance;
    private Node3D? _axesContainer;
    private Dictionary<int, BoxInstance> _boxes = new();
    private Node3D? _worldRoot;
    private int _grabbedCount;

    public int TotalCount => _boxes.Count;
    public int GrabbedCount => _grabbedCount;
    public int RemainingCount => _boxes.Count - _grabbedCount;

    public override void _Ready()
    {
        Instance = this;
    }

    // [DEBUG] B 键切换箱子坐标系显示 — 调试完毕后删除此方法 + BoxWallLoader 中坐标系生成段
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.B })
        {
            if (_axesContainer != null)
            {
                _axesContainer.Visible = !_axesContainer.Visible;
                Logger.Logger.Instance.Info("BoxWallManager",
                    $"Box axes visible: {_axesContainer.Visible}");
            }
        }
    }

    public void SetWorldRoot(Node3D worldRoot)
    {
        _worldRoot = worldRoot;
    }

    public bool LoadBoxWall(string jsonData)
    {
        ClearWall();

        var result = BoxWallLoader.Load(jsonData);
        if (result == null)
        {
            Logger.Logger.Instance.Error("BoxWallManager", "Failed to load box wall");
            return false;
        }

        _meshInstance = result.MeshInstance;
        _axesContainer = result.AxesContainer;

        _boxes.Clear();
        foreach (var box in result.Boxes)
        {
            _boxes[box.Id] = box;
        }

        if (_worldRoot != null)
        {
            _worldRoot.AddChild(_meshInstance);
        }

        EmitSignal(SignalName.BoxWallLoaded, result.TotalCount);

        Logger.Logger.Instance.Info("BoxWallManager",
            $"Box wall loaded: {result.TotalCount} boxes");
        return true;
    }

    public BoxInstance? GetBox(int id)
    {
        return _boxes.TryGetValue(id, out var box) ? box : null;
    }

    public void UpdateBoxState(int id, BoxState newState)
    {
        if (!_boxes.TryGetValue(id, out var box)) return;

        BoxState oldState = box.State;
        box.State = newState;

        if (oldState == BoxState.Waiting && newState == BoxState.Grabbed)
        {
            _grabbedCount++;
        }

        UpdateBoxVisual(box);
        EmitSignal(SignalName.BoxStateChanged, id, (int)newState);
    }

    private void UpdateBoxVisual(BoxInstance box)
    {
        if (_meshInstance == null || _meshInstance.Multimesh == null) return;

        Transform3D hiddenTransform = new Transform3D(
            Basis.Identity.Scaled(Vector3.Zero), Vector3.Zero);

        switch (box.State)
        {
            case BoxState.Waiting:
                _meshInstance.Multimesh.SetInstanceTransform(box.MultiMeshIndex,
                    _meshInstance.Multimesh.GetInstanceTransform(box.MultiMeshIndex));
                _meshInstance.Multimesh.SetInstanceColor(box.MultiMeshIndex, box.Color);
                break;

            case BoxState.Targeted:
                var brightColor = box.Color.Lightened(0.3f);
                _meshInstance.Multimesh.SetInstanceColor(box.MultiMeshIndex, brightColor);
                break;

            case BoxState.Grabbed:
            case BoxState.Released:
                _meshInstance.Multimesh.SetInstanceTransform(box.MultiMeshIndex, hiddenTransform);
                break;
        }
    }

    public void ResetProgress()
    {
        _grabbedCount = 0;
    }

    private void ClearWall()
    {
        if (_meshInstance != null)
        {
            _meshInstance.QueueFree();
            _meshInstance = null;
        }
        _boxes.Clear();
        _grabbedCount = 0;
    }
}
