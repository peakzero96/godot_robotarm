using Godot;
using Grasp.Robot;

namespace Grasp.UI;

public partial class JointDisplayPanel : PanelContainer
{
    [Export] public Color ValueColor { get; set; } = new Color("88ccff");

    private VBoxContainer? _jointList;
    private Label? _titleLabel;
    private HSlider[] _sliders = System.Array.Empty<HSlider>();
    private Label[] _valueLabels = System.Array.Empty<Label>();
    private bool _updatingSliders;

    public override void _Ready()
    {
        _jointList = GetNode<VBoxContainer>("VBox/JointList");
        _titleLabel = GetNode<Label>("VBox/Title");

        if (RobotController.Instance != null)
        {
            RobotController.Instance.Connect(
                RobotController.SignalName.RobotChanged,
                new Callable(this, MethodName.OnRobotChanged));
            OnRobotChanged(RobotController.Instance.RobotName, RobotController.Instance.JointCount);
        }
    }

    public override void _Process(double delta)
    {
        if (_updatingSliders || RobotController.Instance == null) return;

        var angles = RobotController.Instance.GetJointAngles();
        for (int i = 0; i < Mathf.Min(angles.Length, _sliders.Length); i++)
        {
            float deg = Mathf.RadToDeg(angles[i]);
            _valueLabels[i].Text = $"{deg:F1}°";
        }
    }

    private void OnRobotChanged(string robotName, int jointCount)
    {
        _sliders = System.Array.Empty<HSlider>();
        _valueLabels = System.Array.Empty<Label>();
        _updatingSliders = false;

        if (_jointList == null) return;
        foreach (var child in _jointList.GetChildren())
            child.QueueFree();

        if (_titleLabel != null)
            _titleLabel.Text = $"Joints — {robotName}";

        var joints = RobotController.Instance.Joints;
        for (int i = 0; i < joints.Length; i++)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

            var nameLabel = new Label
            {
                Text = joints[i].JointName,
                CustomMinimumSize = new Vector2(90, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var slider = new HSlider
            {
                MinValue = Mathf.RadToDeg(joints[i].LowerLimit),
                MaxValue = Mathf.RadToDeg(joints[i].UpperLimit),
                Step = 0.1,
                Value = 0.0,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(60, 0)
            };
            int idx = i;
            slider.Connect("value_changed", Callable.From((double val) => OnSliderChanged((float)val, idx)));

            var valueLabel = new Label
            {
                Text = "0.0°",
                CustomMinimumSize = new Vector2(60, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var limitsLabel = new Label
            {
                Text = $"{Mathf.RadToDeg(joints[i].LowerLimit):F0}~{Mathf.RadToDeg(joints[i].UpperLimit):F0}",
                CustomMinimumSize = new Vector2(80, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            row.AddChild(nameLabel);
            row.AddChild(slider);
            row.AddChild(valueLabel);
            row.AddChild(limitsLabel);
            _jointList.AddChild(row);

            System.Array.Resize(ref _sliders, i + 1);
            System.Array.Resize(ref _valueLabels, i + 1);
            _sliders[i] = slider;
            _valueLabels[i] = valueLabel;
        }
    }

    private void OnSliderChanged(float value, int index)
    {
        _updatingSliders = true;
        float radians = Mathf.DegToRad(value);
        var angles = RobotController.Instance.GetJointAngles();
        angles[index] = radians;
        RobotController.Instance.SetJointAngles(angles);
        _updatingSliders = false;
    }
}
