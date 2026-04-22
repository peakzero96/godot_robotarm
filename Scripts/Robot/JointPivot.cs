using Godot;

namespace Grasp.Robot;

public partial class JointPivot : Node3D
{
    public string JointName { get; set; } = "";
    public Vector3 RotationAxis { get; set; } = Vector3.Up;
    public float LowerLimit { get; set; }
    public float UpperLimit { get; set; }

    private Basis _baseRotation;

    public void SetBaseRotation(Vector3 rpy)
    {
        _baseRotation = Basis.FromEuler(rpy);
    }

    public void SetAngle(float radians)
    {
        float clamped = Mathf.Clamp(radians, LowerLimit, UpperLimit);
        Basis jointRotation = _baseRotation.Rotated(RotationAxis, clamped);
        Basis = jointRotation;
        _currentAngle = clamped;
    }

    public float GetAngle()
    {
        return _currentAngle;
    }

    private float _currentAngle;
}
