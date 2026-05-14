using Godot;
using Grasp.BoxWall;
using Grasp.Logger;
using Grasp.Robot;
using System.Threading.Tasks;

namespace Grasp.Workflow;

public partial class GrabWorkflowController : Node
{
    [Export] public int TargetBoxId { get; set; } = 0;
    [Export] public float HighlightDelaySec { get; set; } = 1.0f;
    [Export] public float TcpOffset { get; set; } = 0.15f;
    [Export] public float ApproachDistance { get; set; } = 0.3f;
    [Export] public Vector3 LiftDirection { get; set; } = Vector3.Left;
    [Export] public float LiftDistance { get; set; } = 0.3f;
    [Export] public Vector3 PlacePosition { get; set; } = new(-1.0f, -1.0f, 0.0f);
    [Export] public Vector3 PlaceRotationEulerDeg { get; set; } = new(0, 90, 0);

    public bool IsRunning { get; private set; }

    [Signal]
    public delegate void WorkflowStartedEventHandler();

    [Signal]
    public delegate void WorkflowFinishedEventHandler(bool success);

    [Signal]
    public delegate void WorkflowStepChangedEventHandler(string step);

    private Node3D? _worldRoot;

    public override void _Ready()
    {
        _worldRoot = GetParent() as Node3D;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true } key)
        {
            if (key.Keycode == Key.Space && !IsRunning)
                ShowGrabWaypoints();
            if (key.Keycode == Key.Key1)
                PrintCurrentAngles();
            if (key.Keycode == Key.Key2)
                ShowEndEffectorFrames();
        }
    }

    private void PrintCurrentAngles()
    {
        var angles = RobotController.Instance.GetJointAngles();
        var degs = System.Array.ConvertAll(angles, a => Mathf.RadToDeg(a));
        Logger.Logger.Instance.Info("GrabWorkflow",
            $"Current angles (deg): [{string.Join(", ", System.Array.ConvertAll(degs, d => $"{d:F1}"))}]");
        Logger.Logger.Instance.Info("GrabWorkflow",
            $"Current angles (rad): [{string.Join(", ", System.Array.ConvertAll(angles, a => $"{a:F4}"))}]");
    }

    /// <summary>
    /// 按 Space 显示抓取路径的关键点空间位姿（调试用）
    /// </summary>
    private void ShowGrabWaypoints()
    {
        var box = BoxWallManager.Instance.GetBox(TargetBoxId);
        if (box == null)
        {
            Logger.Logger.Instance.Warn("GrabWorkflow", $"Box {TargetBoxId} not found");
            return;
        }

        // 清除旧标记
        RemoveWaypointMarkers();

        var gripper = RobotController.Instance.Gripper;
        Basis eeBasis = gripper?.GlobalTransform.Basis ?? Basis.Identity;
        var config = BuildConfig();

        // 使用预计算的 GrabPath 或实时计算
        var path = box.GrabPath ?? GrabPathCalculator.Compute(box, eeBasis, config);
        if (box.GrabPath == null)
        {
            box.GrabPath = path;
        }

        GrabPathCalculator.PrintPath(path);

        // 在场景中创建标记
        var wp = path.Waypoints;
        CreatePoseMarker("Wp_BoxCenter", wp[0].Position, new Color(1, 1, 1));
        CreatePoseMarker("Wp_Grab", wp[1].Position, new Color(1, 1, 0));
        CreatePoseMarker("Wp_Approach", wp[2].Position, new Color(0, 1, 1));
        CreatePoseMarker("Wp_Lift", wp[3].Position, new Color(0, 1, 0));
        CreatePoseMarker("Wp_Place", wp[4].Position, new Color(1, 0.5f, 0));

        // 绘制路径连线
        CreatePathLine(new[] { wp[2].Position, wp[1].Position, wp[3].Position, wp[4].Position });
    }

    private GrabPathConfig BuildConfig()
    {
        return new GrabPathConfig
        {
            TcpOffset = TcpOffset,
            TcpRotationOffset = Basis.Identity.Rotated(Vector3.Up, Mathf.Pi / 2f),
            ApproachDistance = ApproachDistance,
            LiftDirection = LiftDirection,
            LiftDistance = LiftDistance,
            PlacePosition = PlacePosition,
            PlaceRotation = Basis.Identity
                .Rotated(Vector3.Up, Mathf.DegToRad(PlaceRotationEulerDeg.Y))
                .Rotated(Vector3.Right, Mathf.DegToRad(PlaceRotationEulerDeg.X))
                .Rotated(Vector3.Back, Mathf.DegToRad(PlaceRotationEulerDeg.Z))
                .GetRotationQuaternion()
        };
    }

    private void CreatePoseMarker(string name, Vector3 pos, Color color)
    {
        if (_worldRoot == null) return;

        var mesh = new SphereMesh { Radius = 0.03f, Height = 0.06f };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded
        };
        var marker = new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            MaterialOverride = mat,
            Position = pos
        };
        _worldRoot.AddChild(marker);
    }

    private void CreatePathLine(Vector3[] points)
    {
        if (_worldRoot == null || points.Length < 2) return;

        var lineMesh = new ImmediateMesh();
        lineMesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        lineMesh.SurfaceSetColor(new Color(1, 1, 1, 0.6f));
        foreach (var p in points)
            lineMesh.SurfaceAddVertex(p);
        lineMesh.SurfaceEnd();

        var mat = new StandardMaterial3D
        {
            ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(1, 1, 1, 0.6f)
        };

        var lineNode = new MeshInstance3D
        {
            Name = "Wp_PathLine",
            Mesh = lineMesh,
            MaterialOverride = mat
        };
        _worldRoot.AddChild(lineNode);
    }

    private void RemoveWaypointMarkers()
    {
        if (_worldRoot == null) return;
        foreach (var child in _worldRoot.GetChildren())
        {
            if (child is Node3D n && n.Name.ToString().StartsWith("Wp_"))
                n.QueueFree();
        }
    }

    /// <summary>
    /// 按 Key 2 显示机械臂末端坐标系和虚拟夹爪坐标系
    /// </summary>
    private void ShowEndEffectorFrames()
    {
        var gripper = RobotController.Instance.Gripper;
        if (gripper == null)
        {
            Logger.Logger.Instance.Warn("GrabWorkflow", "Gripper is null");
            return;
        }

        // 清除旧标记
        RemoveWaypointMarkers();

        var eeTransform = gripper.GlobalTransform;
        Vector3 eePos = eeTransform.Origin;
        Basis eeBasis = eeTransform.Basis;

        // 虚拟夹爪：沿末端 +X 平移 TcpOffset，绕 Y 轴旋转 90°
        Vector3 tcpPos = eePos + eeBasis.X.Normalized() * TcpOffset;
        Basis tcpBasis = eeBasis.Rotated(Vector3.Up, Mathf.Pi / 2f);

        // 日志输出位姿
        var eeEuler = eeBasis.GetEuler();
        var tcpEuler = tcpBasis.GetEuler();
        Logger.Logger.Instance.Info("GrabWorkflow",
            $"End-Effector: pos=({eePos.X:F3}, {eePos.Y:F3}, {eePos.Z:F3}) " +
            $"euler(deg)=({Mathf.RadToDeg(eeEuler.X):F1}, {Mathf.RadToDeg(eeEuler.Y):F1}, {Mathf.RadToDeg(eeEuler.Z):F1})");
        Logger.Logger.Instance.Info("GrabWorkflow",
            $"Virtual Gripper: pos=({tcpPos.X:F3}, {tcpPos.Y:F3}, {tcpPos.Z:F3}) " +
            $"euler(deg)=({Mathf.RadToDeg(tcpEuler.X):F1}, {Mathf.RadToDeg(tcpEuler.Y):F1}, {Mathf.RadToDeg(tcpEuler.Z):F1})");

        // 末端坐标系（RGB = XYZ）
        CreateAxisMarker("Wp_EE_Axes", eeTransform, 0.4f);
        // 虚拟夹爪坐标系
        CreateAxisMarker("Wp_TCP_Axes", new Transform3D(tcpBasis, tcpPos), 0.3f);
    }

    private void CreateAxisMarker(string name, Transform3D transform, float len)
    {
        if (_worldRoot == null) return;

        var axesMesh = new ImmediateMesh();
        axesMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);

        Vector3 o = transform.Origin;
        Vector3 x = transform.Basis.X.Normalized();
        Vector3 y = transform.Basis.Y.Normalized();
        Vector3 z = transform.Basis.Z.Normalized();

        // X - Red
        axesMesh.SurfaceSetColor(new Color(1, 0.2f, 0.2f));
        axesMesh.SurfaceAddVertex(o);
        axesMesh.SurfaceAddVertex(o + x * len);
        // Y - Green
        axesMesh.SurfaceSetColor(new Color(0.2f, 1, 0.2f));
        axesMesh.SurfaceAddVertex(o);
        axesMesh.SurfaceAddVertex(o + y * len);
        // Z - Blue
        axesMesh.SurfaceSetColor(new Color(0.2f, 0.2f, 1));
        axesMesh.SurfaceAddVertex(o);
        axesMesh.SurfaceAddVertex(o + z * len);

        axesMesh.SurfaceEnd();

        var mat = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Disabled
        };

        var node = new MeshInstance3D
        {
            Name = name,
            Mesh = axesMesh,
            MaterialOverride = mat
        };
        _worldRoot.AddChild(node);
    }

    public async void StartWorkflow()
    {
        if (IsRunning) return;

        IsRunning = true;
        EmitSignal(SignalName.WorkflowStarted);
        Logger.Logger.Instance.Info("GrabWorkflow",
            $"Starting workflow for box {TargetBoxId}");

        try
        {
            // === Define keyframes (angles in radians) ===
            // TODO: Tune these by using Key 1 to print current angles at desired poses
            var homeAngles = new float[] { 0, 0, 0, 0, 0, 0 };

            var approachKeyframes = new JointKeyframe[]
            {
                new() { Angles = homeAngles, DurationSec = 0.01f },
                new() { Angles = new float[] { 0f, -0.3f, 0.8f, 0f, 0.5f, 0f }, DurationSec = 2.0f },
                new() { Angles = new float[] { 0f, -0.5f, 1.0f, 0f, 0.6f, 0f }, DurationSec = 1.5f },
            };

            var transportKeyframes = new JointKeyframe[]
            {
                new() { Angles = new float[] { 0f, -0.2f, 0.6f, 0f, 0.3f, 0f }, DurationSec = 2.0f },
                new() { Angles = new float[] { 0.5f, -0.1f, 0.4f, 0f, 0.2f, 0f }, DurationSec = 1.5f },
            };

            var returnKeyframes = new JointKeyframe[]
            {
                new() { Angles = new float[] { 0f, -0.3f, 0.6f, 0f, 0.3f, 0f }, DurationSec = 1.5f },
                new() { Angles = homeAngles, DurationSec = 2.0f },
            };

            // Step 1: Highlight
            EmitStep("Highlighting target box");
            BoxAttachController.Instance.HighlightBox(TargetBoxId);
            await WaitTimer(HighlightDelaySec);

            // Step 2: Move to box
            EmitStep("Moving to box");
            JointSequencePlayer.Instance.Play(approachKeyframes);
            await ToSignal(JointSequencePlayer.Instance,
                JointSequencePlayer.SignalName.PlaybackFinished);

            // Step 3: Grab
            EmitStep("Grabbing box");
            BoxAttachController.Instance.GrabBox(TargetBoxId);
            await ToSignal(BoxAttachController.Instance,
                BoxAttachController.SignalName.BoxAttached);

            // Step 4: Transport
            EmitStep("Transporting box");
            await WaitTimer(0.5f);
            JointSequencePlayer.Instance.Play(transportKeyframes);
            await ToSignal(JointSequencePlayer.Instance,
                JointSequencePlayer.SignalName.PlaybackFinished);

            // Step 5: Release
            EmitStep("Releasing box");
            BoxAttachController.Instance.ReleaseBox(TargetBoxId);
            await ToSignal(BoxAttachController.Instance,
                BoxAttachController.SignalName.BoxReleased);

            // Step 6: Return home
            EmitStep("Returning home");
            JointSequencePlayer.Instance.Play(returnKeyframes);
            await ToSignal(JointSequencePlayer.Instance,
                JointSequencePlayer.SignalName.PlaybackFinished);

            EmitSignal(SignalName.WorkflowFinished, true);
            Logger.Logger.Instance.Info("GrabWorkflow", "Workflow completed successfully");
        }
        catch (System.Exception e)
        {
            Logger.Logger.Instance.Error("GrabWorkflow", $"Workflow failed: {e.Message}");
            EmitSignal(SignalName.WorkflowFinished, false);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void EmitStep(string step)
    {
        EmitSignal(SignalName.WorkflowStepChanged, step);
        Logger.Logger.Instance.Info("GrabWorkflow", $"Step: {step}");
    }

    private async Task WaitTimer(float durationSec)
    {
        var timer = new Timer { OneShot = true, WaitTime = durationSec };
        AddChild(timer);
        timer.Start();
        await ToSignal(timer, Timer.SignalName.Timeout);
        timer.QueueFree();
    }
}
