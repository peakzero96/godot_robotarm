using Godot;
using Grasp.Logger;
using Grasp.Robot;

namespace Grasp.Workflow;

public partial class JointSequencePlayer : Node
{
    public static JointSequencePlayer Instance { get; private set; } = null!;

    public bool IsPlaying { get; private set; }

    private Tween? _activeTween;

    [Signal]
    public delegate void PlaybackFinishedEventHandler();

    [Signal]
    public delegate void PlaybackStartedEventHandler();

    public override void _Ready()
    {
        Instance = this;
    }

    public async void Play(JointKeyframe[] keyframes)
    {
        if (keyframes == null || keyframes.Length == 0) return;
        if (IsPlaying)
        {
            Logger.Logger.Instance.Warn("JointSequencePlayer", "Already playing, ignored");
            return;
        }

        IsPlaying = true;
        EmitSignal(SignalName.PlaybackStarted);
        Logger.Logger.Instance.Info("JointSequencePlayer",
            $"Starting playback: {keyframes.Length} keyframes");

        float[] startAngles = RobotController.Instance.GetJointAngles();

        for (int i = 0; i < keyframes.Length; i++)
        {
            var kf = keyframes[i];

            if (kf.DurationSec <= 0f)
            {
                RobotController.Instance.SetJointAngles(kf.Angles);
                startAngles = (float[])kf.Angles.Clone();
                continue;
            }

            float[] fromAngles = (float[])startAngles.Clone();
            float[] toAngles = kf.Angles;

            var tween = CreateTween();
            _activeTween = tween;
            tween.TweenMethod(
                Callable.From((double t) => InterpolateJoints(fromAngles, toAngles, (float)t)),
                0.0, 1.0, kf.DurationSec
            ).SetTrans(Tween.TransitionType.Cubic)
             .SetEase(Tween.EaseType.InOut);

            await ToSignal(tween, Tween.SignalName.Finished);
            startAngles = (float[])kf.Angles.Clone();
        }

        IsPlaying = false;
        EmitSignal(SignalName.PlaybackFinished);
        Logger.Logger.Instance.Info("JointSequencePlayer", "Playback finished");
    }

    private void InterpolateJoints(float[] from, float[] to, float t)
    {
        int count = Mathf.Min(Mathf.Min(from.Length, to.Length),
            RobotController.Instance.JointCount);
        var interpolated = new float[count];
        for (int i = 0; i < count; i++)
            interpolated[i] = Mathf.Lerp(from[i], to[i], t);
        RobotController.Instance.SetJointAngles(interpolated);
    }

    public void Stop()
    {
        if (_activeTween != null && _activeTween.IsValid())
            _activeTween.Kill();
        _activeTween = null;
        IsPlaying = false;
    }
}
