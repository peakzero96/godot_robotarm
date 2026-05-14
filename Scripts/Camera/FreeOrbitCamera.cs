using Godot;

namespace Grasp.Camera;

public partial class FreeOrbitCamera : Camera3D
{
    [Export] public float RotateSpeed { get; set; } = 0.005f;
    [Export] public float ZoomSpeed { get; set; } = 0.1f;
    [Export] public float PanSpeed { get; set; } = 0.005f;
    [Export] public float MinDistance { get; set; } = 0.5f;
    [Export] public float MaxDistance { get; set; } = 100.0f;
    [Export] public float MinTheta { get; set; } = 0.05f;
    [Export] public float MaxTheta { get; set; } = Mathf.Pi - 0.05f;

    private Vector3 _target;
    private float _phi;
    private float _theta;
    private float _distance;
    private Vector3 _defaultPosition;
    private Vector3 _defaultTarget;

    private bool _rotating;
    private bool _panning;
    private Vector2 _lastMousePos;

    public override void _Ready()
    {
        _defaultPosition = Position;

        // Infer orbit target from where the camera is actually looking.
        // Project origin onto the camera's forward ray to find the closest point.
        Vector3 forward = -Basis.Z;
        float t = -Position.Dot(forward);
        if (t > 0.01f)
        {
            _target = Position + forward * t;
            _distance = t;
        }
        else
        {
            _target = Vector3.Zero;
            _distance = Position.Length();
        }

        _defaultTarget = _target;

        Vector3 dir = (Position - _target).Normalized();
        _phi = Mathf.Atan2(dir.Y, dir.X);
        _theta = Mathf.Acos(Mathf.Clamp(dir.Z, -1f, 1f));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            switch (mb.ButtonIndex)
            {
                case MouseButton.Right:
                    _rotating = mb.Pressed;
                    if (_rotating) _lastMousePos = mb.Position;
                    break;
                case MouseButton.Middle:
                    _panning = mb.Pressed;
                    if (_panning) _lastMousePos = mb.Position;
                    break;
                case MouseButton.WheelUp:
                    _distance = Mathf.Max(MinDistance, _distance - ZoomSpeed * _distance);
                    UpdateTransform();
                    break;
                case MouseButton.WheelDown:
                    _distance = Mathf.Min(MaxDistance, _distance + ZoomSpeed * _distance);
                    UpdateTransform();
                    break;
            }
        }

        if (@event is InputEventMouseMotion motion)
        {
            Vector2 delta = motion.Position - _lastMousePos;
            _lastMousePos = motion.Position;

            if (_rotating)
            {
                _phi -= delta.X * RotateSpeed;
                _theta -= delta.Y * RotateSpeed;
                _theta = Mathf.Clamp(_theta, MinTheta, MaxTheta);
                UpdateTransform();
            }

            if (_panning)
            {
                Vector3 right = Basis.X;
                Vector3 up = Basis.Y;
                _target -= right * delta.X * PanSpeed * _distance;
                _target += up * delta.Y * PanSpeed * _distance;
                UpdateTransform();
            }
        }
    }

    private void UpdateTransform()
    {
        float h = Mathf.Sin(_theta) * _distance;
        Vector3 offset = new Vector3(
            h * Mathf.Cos(_phi),
            h * Mathf.Sin(_phi),
            Mathf.Cos(_theta) * _distance
        );

        Position = _target + offset;

        Vector3 forward = (_target - Position).Normalized();
        Vector3 right = forward.Cross(new Vector3(0, 0, 1)).Normalized();
        if (right.LengthSquared() < 0.001f)
            right = Vector3.Right;
        Vector3 up = right.Cross(forward).Normalized();
        Basis = new Basis(right, up, -forward);
    }

    private void ResetCamera()
    {
        _target = _defaultTarget;
        _distance = _defaultPosition.Length();

        Vector3 dir = (_defaultPosition - _defaultTarget).Normalized();
        _phi = Mathf.Atan2(dir.Y, dir.X);
        _theta = Mathf.Acos(Mathf.Clamp(dir.Z, -1f, 1f));
        UpdateTransform();
    }
}
