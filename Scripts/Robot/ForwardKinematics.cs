using Godot;

namespace Grasp.Robot;

public static class ForwardKinematics
{
    public static Transform3D ComputeEePose(JointPivot[] joints, float[] angles)
    {
        Transform3D current = Transform3D.Identity;

        for (int i = 0; i < Mathf.Min(angles.Length, joints.Length); i++)
        {
            var joint = joints[i];
            float angle = Mathf.Clamp(angles[i], joint.LowerLimit, joint.UpperLimit);

            // JointPivot.SetAngle: Basis = new Basis(_baseQuat * Quaternion(RotationAxis, angle))
            // Position is set once by RobotLoader and doesn't change
            var localRotation = new Basis(new Quaternion(joint.RotationAxis, angle));
            var localTransform = new Transform3D(localRotation, joint.Position);

            current = current * localTransform;
        }

        return current;
    }
}
