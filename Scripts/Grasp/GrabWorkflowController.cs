using Godot;
using Grasp.Logger;
using Grasp.Robot;
using System.Threading.Tasks;

namespace Grasp.Workflow;

public partial class GrabWorkflowController : Node
{
    [Export] public int TargetBoxId { get; set; } = 0;
    [Export] public float HighlightDelaySec { get; set; } = 1.0f;

    public bool IsRunning { get; private set; }

    [Signal]
    public delegate void WorkflowStartedEventHandler();

    [Signal]
    public delegate void WorkflowFinishedEventHandler(bool success);

    [Signal]
    public delegate void WorkflowStepChangedEventHandler(string step);

    /// <summary>
    /// 按 Space 键启动，按 P 打印当前关节角度（用于调试关键帧）
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true } key)
        {
            if (key.Keycode == Key.Space && !IsRunning)
                StartWorkflow();
            if (key.Keycode == Key.Key1)
                PrintCurrentAngles();
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
