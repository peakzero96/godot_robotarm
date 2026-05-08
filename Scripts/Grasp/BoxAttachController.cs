using Godot;
using Grasp.BoxWall;
using Grasp.Logger;
using Grasp.Main;
using Grasp.Robot;

namespace Grasp.Workflow;

public partial class BoxAttachController : Node
{
    public static BoxAttachController Instance { get; private set; } = null!;

    [Export] public float AttachTransitionSec { get; set; } = 0.3f;

    private MeshInstance3D? _attachedBox;
    private StandardMaterial3D? _attachedBoxMaterial;
    private Node3D? _worldRoot;

    [Signal]
    public delegate void BoxAttachedEventHandler(int boxId);

    [Signal]
    public delegate void BoxReleasedEventHandler(int boxId);

    public override void _Ready()
    {
        Instance = this;
    }

    public void SetWorldRoot(Node3D worldRoot)
    {
        _worldRoot = worldRoot;
    }

    public void HighlightBox(int boxId)
    {
        BoxWallManager.Instance.UpdateBoxState(boxId, BoxState.Targeted);
        Logger.Logger.Instance.Info("BoxAttachController", $"Box {boxId} highlighted");
    }

    /// <summary>
    /// 计算箱子抓取面坐标系，参照 end_effector 朝向选择最优抓取面。
    /// 规则：
    ///   1. 找到与 end_effector X 轴夹角最小的局部坐标轴（法线轴）
    ///   2. 该法线轴的两个面中，离世界坐标原点更近的是抓取面
    ///   3. 抓取面坐标系 X 轴从表面指向箱子中心
    /// </summary>
    public static Transform3D CalculateGrabTransform(BoxInstance box, Basis endEffectorBasis, out int normalAxis)
    {
        // 箱子纯旋转 Basis（直接从四元数构建，避免 euler 重建误差）
        var boxRotBasis = new Basis(box.RotationQuat);

        Vector3 stdX = endEffectorBasis.X.Normalized();
        Vector3 stdY = endEffectorBasis.Y.Normalized();

        // 1. 哪个局部轴与 end_effector X 夹角最小
        float dotX = Mathf.Abs(boxRotBasis.X.Normalized().Dot(stdX));
        float dotY = Mathf.Abs(boxRotBasis.Y.Normalized().Dot(stdX));
        float dotZ = Mathf.Abs(boxRotBasis.Z.Normalized().Dot(stdX));

        normalAxis = 0;
        float maxDot = dotX;
        if (dotY > maxDot) { normalAxis = 1; maxDot = dotY; }
        if (dotZ > maxDot) { normalAxis = 2; }

        // 法线方向和半尺寸
        Vector3 normalAxisVec = normalAxis switch
        {
            0 => boxRotBasis.X,
            1 => boxRotBasis.Y,
            _ => boxRotBasis.Z
        };
        Vector3 normalDir = normalAxisVec.Normalized();
        float halfSize = normalAxis switch
        {
            0 => box.Size.X / 2f,
            1 => box.Size.Y / 2f,
            _ => box.Size.Z / 2f
        };

        // 2. 两个候选面，选离原点近的
        Vector3 posPlus = box.Position + normalDir * halfSize;
        Vector3 posMinus = box.Position - normalDir * halfSize;

        Vector3 surfacePos;
        int sign;
        if (posPlus.Length() < posMinus.Length())
        {
            surfacePos = posPlus;
            sign = 1;
        }
        else
        {
            surfacePos = posMinus;
            sign = -1;
        }

        // 3. 构建抓取面坐标系
        // X: 从表面指向箱子中心
        Vector3 grabX = (-sign * normalDir).Normalized();

        // Y: 从剩余两个局部轴中选与 end_effector Y 夹角最小的，正交化
        int r0 = normalAxis == 0 ? 1 : 0;
        int r1 = normalAxis == 2 ? 1 : 2;
        Vector3 axisR0 = (r0 switch { 0 => boxRotBasis.X, 1 => boxRotBasis.Y, _ => boxRotBasis.Z }).Normalized();
        Vector3 axisR1 = (r1 switch { 0 => boxRotBasis.X, 1 => boxRotBasis.Y, _ => boxRotBasis.Z }).Normalized();

        Vector3 grabY = Mathf.Abs(axisR0.Dot(stdY)) >= Mathf.Abs(axisR1.Dot(stdY))
            ? axisR0 : axisR1;
        grabY = (grabY - grabX * grabY.Dot(grabX)).Normalized();

        // Z: 右手系
        Vector3 grabZ = grabX.Cross(grabY).Normalized();

        var grabBasis = new Basis(grabX, grabY, grabZ);
        return new Transform3D(grabBasis, surfacePos);
    }

    public void GrabBox(int boxId)
    {
        var box = BoxWallManager.Instance.GetBox(boxId);
        if (box == null)
        {
            Logger.Logger.Instance.Error("BoxAttachController", $"Box {boxId} not found");
            return;
        }

        var gripper = RobotController.Instance.Gripper;
        if (gripper == null)
        {
            Logger.Logger.Instance.Error("BoxAttachController", "Gripper is null");
            return;
        }

        var grabTransform = CalculateGrabTransform(box, gripper.GlobalTransform.Basis, out int normalAxis);
        Logger.Logger.Instance.Info("BoxAttachController",
            $"Box {boxId} grab position: {grabTransform.Origin}, normalAxis: {normalAxis}");

        // Hide the wall copy
        BoxWallManager.Instance.UpdateBoxState(boxId, BoxState.Grabbed);

        // Create standalone box at its center position
        CreateStandaloneBox(box.Position, box.Size);

        // Reparent to gripper, preserving global transform (box stays in place)
        var globalTransform = _attachedBox.GlobalTransform;
        _attachedBox.GetParent()?.RemoveChild(_attachedBox);
        gripper.AddChild(_attachedBox);
        _attachedBox.GlobalTransform = globalTransform;

        EmitSignal(SignalName.BoxAttached, boxId);
        Logger.Logger.Instance.Info("BoxAttachController", $"Box {boxId} attached to gripper");
    }

    public async void ReleaseBox(int boxId)
    {
        if (_attachedBox == null) return;

        var gripper = RobotController.Instance.Gripper;
        if (gripper == null) return;

        Vector3 releasePos = _attachedBox.GlobalPosition;

        // Reparent to WorldRoot
        _attachedBox.GetParent()?.RemoveChild(_attachedBox);
        if (_worldRoot != null)
            _worldRoot.AddChild(_attachedBox);
        _attachedBox.GlobalPosition = releasePos;

        BoxWallManager.Instance.UpdateBoxState(boxId, BoxState.Released);

        // Fade out
        float fadeDuration = (float)AppConfig.Instance.FadeOutDurationMs / 1000f;
        _attachedBoxMaterial!.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        var fadeTween = CreateTween();
        fadeTween.TweenProperty(_attachedBoxMaterial, "albedo_color:a", 0.0f, fadeDuration);
        await ToSignal(fadeTween, Tween.SignalName.Finished);

        _attachedBox.QueueFree();
        _attachedBox = null;
        _attachedBoxMaterial = null;

        EmitSignal(SignalName.BoxReleased, boxId);
        Logger.Logger.Instance.Info("BoxAttachController", $"Box {boxId} released and faded");
    }

    private void CreateStandaloneBox(Vector3 position, Vector3 size)
    {
        _attachedBoxMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(AppConfig.Instance.BoxGrabbedColor),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };

        _attachedBox = new MeshInstance3D
        {
            Name = "GrabbedBox",
            Mesh = new BoxMesh(),
            MaterialOverride = _attachedBoxMaterial,
            Position = position,
            Scale = size
        };

        if (_worldRoot != null)
            _worldRoot.AddChild(_attachedBox);
    }

    public MeshInstance3D? GetAttachedBox() => _attachedBox;
}
