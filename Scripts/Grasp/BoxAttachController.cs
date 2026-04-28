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

    public async void GrabBox(int boxId)
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

        // Hide the wall copy
        BoxWallManager.Instance.UpdateBoxState(boxId, BoxState.Grabbed);

        // Create standalone box at wall position
        CreateStandaloneBox(box.Position, box.Size);

        // Tween to gripper position
        Vector3 targetPos = gripper.GlobalPosition;
        var tween = CreateTween();
        tween.TweenProperty(_attachedBox, "global_position", targetPos, AttachTransitionSec)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);

        // Reparent to gripper
        var globalTransform = _attachedBox.GlobalTransform;
        _attachedBox.GetParent()?.RemoveChild(_attachedBox);
        gripper.AddChild(_attachedBox);
        _attachedBox.GlobalTransform = globalTransform;
        _attachedBox.Position = Vector3.Zero;

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
