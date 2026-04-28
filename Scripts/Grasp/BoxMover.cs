using Godot;

namespace Grasp.Workflow;

/// <summary>
/// 独立测试：创建箱子，Tween 动画移动后淡出消失。
/// 添加到 WorldRoot 运行即可看到效果。
/// </summary>
public partial class BoxMover : Node3D
{
    [Export] public Vector3 StartPosition { get; set; } = new Vector3(1.5f, 0.3f, 0.0f);
    [Export] public Vector3 EndPosition { get; set; } = new Vector3(1.5f, 1.5f, 0.5f);
    [Export] public Vector3 BoxSize { get; set; } = new Vector3(0.3f, 0.2f, 0.6f);
    [Export] public Color BoxColor { get; set; } = new Color("#C4A882");
    [Export] public float MoveDurationSec { get; set; } = 2.0f;
    [Export] public float FadeDurationSec { get; set; } = 2.0f;

    private MeshInstance3D? _boxMesh;
    private StandardMaterial3D? _boxMaterial;

    public override void _Ready()
    {
        CreateBox();
        CallDeferred(MethodName.StartAnimation);
    }

    private void CreateBox()
    {
        _boxMaterial = new StandardMaterial3D
        {
            AlbedoColor = BoxColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };

        _boxMesh = new MeshInstance3D
        {
            Mesh = new BoxMesh(),
            MaterialOverride = _boxMaterial,
            Position = StartPosition,
            Scale = BoxSize
        };
        AddChild(_boxMesh);
    }

    private async void StartAnimation()
    {
        // Tween 移动
        var tween = CreateTween();
        tween.TweenProperty(_boxMesh, "position", EndPosition, MoveDurationSec)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);

        // Tween 淡出
        var fadeTween = CreateTween();
        fadeTween.TweenProperty(_boxMaterial, "albedo_color:a", 0.0f, FadeDurationSec);
        await ToSignal(fadeTween, Tween.SignalName.Finished);

        QueueFree();
    }
}
