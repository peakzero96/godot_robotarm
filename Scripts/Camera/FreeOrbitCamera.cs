using Godot;

namespace Grasp.Camera;

public partial class FreeOrbitCamera : Camera3D
{
    [Export] public float RotateSpeed { get; set; } = 0.005f;
    [Export] public float ZoomSpeed { get; set; } = 0.1f;
    [Export] public float PanSpeed { get; set; } = 0.005f;
    [Export] public float MinDistance { get; set; } = 1.0f;
    [Export] public float MaxDistance { get; set; } = 50.0f;
    [Export] public float MinPitch { get; set; } = -Mathf.Pi / 2 + 0.01f;
    [Export] public float MaxPitch { get; set; } = Mathf.Pi / 2 - 0.01f;

    private Vector3 _target;
    private float _yaw;
    private float _pitch;
    private float _distance = 12.0f;
    private Basis _defaultBasis;
    private Vector3 _defaultPosition;

    public override void _Ready()
    {
        _defaultBasis = Basis;
        _defaultPosition = Position;

        Vector3 dir = -GlobalBasis.Z;
        _target = Position + dir * _distance;
        _yaw = Mathf.Atan2(-dir.X, -dir.Z);
        _pitch = Mathf.Asin(dir.Y);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.Pressed)
            {
                switch (mouseButton.ButtonIndex)
                {
                    case MouseButton.WheelUp:
                        _distance = Mathf.Max(MinDistance, _distance - ZoomSpeed * _distance);
                        UpdateTransform();
                        break;
                    case MouseButton.WheelDown:
                        _distance = Mathf.Min(MaxDistance, _distance + ZoomSpeed * _distance);
                        UpdateTransform();
                        break;
                    case MouseButton.Middle:
                        ResetCamera();
                        break;
                }
            }
        }

        if (@event is InputEventMouseMotion mouseMotion)
        {
            if (Input.IsActionPressed("orbit_rotate"))
            {
                _yaw -= mouseMotion.Relative.X * RotateSpeed;
                _pitch -= mouseMotion.Relative.Y * RotateSpeed;
                _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
                UpdateTransform();
            }

            if (Input.IsActionPressed("orbit_pan"))
            {
                Vector3 right = GlobalBasis.X;
                Vector3 up = GlobalBasis.Y;
                _target -= right * mouseMotion.Relative.X * PanSpeed * _distance;
                _target += up * mouseMotion.Relative.Y * PanSpeed * _distance;
                UpdateTransform();
            }
        }
    }

    private void UpdateTransform()
    {
        Vector3 offset = new Vector3(
            Mathf.Sin(_yaw) * Mathf.Cos(_pitch),
            Mathf.Sin(_pitch),
            Mathf.Cos(_yaw) * Mathf.Cos(_pitch)
        ) * _distance;

        Position = _target + offset;
        LookAt(_target, Vector3.Up);
    }

    private void ResetCamera()
    {
        Position = _defaultPosition;
        GlobalBasis = _defaultBasis;

        Vector3 dir = -GlobalBasis.Z;
        _target = Position + dir * _distance;
        _yaw = Mathf.Atan2(-dir.X, -dir.Z);
        _pitch = Mathf.Asin(dir.Y);
        _distance = 12.0f;
    }
}
