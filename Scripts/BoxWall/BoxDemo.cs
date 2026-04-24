using Godot;
using Grasp.Logger;

namespace Grasp.BoxWall;

/// <summary>
/// 临时 Demo: 键盘控制单个箱子移动
/// Q/E: 切换选中的箱子
/// WASD: 在 XZ 平面移动
/// R/F: 上下移动 (Y)
/// 1/2: 左右旋转 (Y轴)
/// 3/4: 前后旋转 (X轴)
/// T: 切换目标状态 (颜色高亮)
/// </summary>
public partial class BoxDemo : Node3D
{
    private bool _toggled;
    private int _selectedIndex;
    private float _moveSpeed = 0.05f;
    private float _rotSpeed = 0.05f;

    public override void _Ready()
    {
        Logger.Logger.Instance.Info("BoxDemo",
            "Q/E=切换箱子, WASD=移动, R/F=升降, 1/2/3/4=旋转, T=高亮");
    }

    public override void _Process(double delta)
    {
        var mgr = BoxWallManager.Instance;
        if (mgr == null || mgr.TotalCount == 0) return;

        var multiMesh = GetMultiMesh();
        if (multiMesh == null) return;

        // Select box (use direct key checks)
        if (Input.IsKeyPressed(Key.Q) && !_toggled)
        {
            _toggled = true;
            _selectedIndex = (_selectedIndex - 1 + mgr.TotalCount) % mgr.TotalCount;
            PrintInfo(mgr, multiMesh);
        }
        if (Input.IsKeyPressed(Key.E) && !_toggled)
        {
            _toggled = true;
            _selectedIndex = (_selectedIndex + 1) % mgr.TotalCount;
            PrintInfo(mgr, multiMesh);
        }
        if (!Input.IsKeyPressed(Key.Q) && !Input.IsKeyPressed(Key.E))
            _toggled = false;

        // Get current transform
        var transform = multiMesh.GetInstanceTransform(_selectedIndex);
        var basis = transform.Basis;
        var pos = transform.Origin;

        // Move
        if (Input.IsKeyPressed(Key.W)) pos.Z -= _moveSpeed;
        if (Input.IsKeyPressed(Key.S)) pos.Z += _moveSpeed;
        if (Input.IsKeyPressed(Key.A)) pos.X -= _moveSpeed;
        if (Input.IsKeyPressed(Key.D)) pos.X += _moveSpeed;
        if (Input.IsKeyPressed(Key.R)) pos.Y += _moveSpeed;
        if (Input.IsKeyPressed(Key.F)) pos.Y -= _moveSpeed;

        // Rotate
        if (Input.IsKeyPressed(Key.Key1))
            basis = basis.Rotated(Vector3.Up, _rotSpeed);
        if (Input.IsKeyPressed(Key.Key2))
            basis = basis.Rotated(Vector3.Up, -_rotSpeed);
        if (Input.IsKeyPressed(Key.Key3))
            basis = basis.Rotated(Vector3.Right, _rotSpeed);
        if (Input.IsKeyPressed(Key.Key4))
            basis = basis.Rotated(Vector3.Right, -_rotSpeed);

        // Toggle highlight
        if (Input.IsKeyPressed(Key.T) && !_toggled)
        {
            _toggled = true;
            var box = mgr.GetBox(_selectedIndex);
            if (box != null)
            {
                var newState = box.State == BoxState.Targeted
                    ? BoxState.Waiting
                    : BoxState.Targeted;
                mgr.UpdateBoxState(_selectedIndex, newState);
            }
        }

        multiMesh.SetInstanceTransform(_selectedIndex, new Transform3D(basis, pos));
    }

    private static MultiMesh? GetMultiMesh()
    {
        // Find the BoxWall MultiMeshInstance3D in WorldRoot
        var root = Engine.GetMainLoop() as SceneTree;
        if (root == null) return null;
        var worldRoot = root.CurrentScene?.FindChild("WorldRoot", true, false) as Node3D;
        if (worldRoot == null) return null;
        var meshInstance = worldRoot.FindChild("BoxWall", true, false) as MultiMeshInstance3D;
        return meshInstance?.Multimesh;
    }

    private void PrintInfo(BoxWallManager mgr, MultiMesh multiMesh)
    {
        var t = multiMesh.GetInstanceTransform(_selectedIndex);
        var box = mgr.GetBox(_selectedIndex);
        Logger.Logger.Instance.Info("BoxDemo",
            $"Selected box[{_selectedIndex}] pos={t.Origin} state={box?.State}");
    }
}