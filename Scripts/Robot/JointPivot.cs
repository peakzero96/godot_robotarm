using Godot;

namespace Grasp.Robot;

public partial class JointPivot : Node3D
{
    public string JointName { get; set; } = "";
    public Vector3 RotationAxis { get; set; } = Vector3.Up;
    public float LowerLimit { get; set; }
    public float UpperLimit { get; set; }

    private Quaternion _baseQuat = Quaternion.Identity;

    public void SetBaseRotation(Quaternion quat)
    {
        _baseQuat = quat;
    }

    public void SetAngle(float radians)
    {
        float clamped = Mathf.Clamp(radians, LowerLimit, UpperLimit);
        var combined = _baseQuat * new Quaternion(RotationAxis, clamped);
        Basis = new Basis(combined);
        _currentAngle = clamped;
    }

    public float GetAngle()
    {
        return _currentAngle;
    }

    private float _currentAngle;
}
