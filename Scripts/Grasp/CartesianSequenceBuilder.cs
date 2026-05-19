using Godot;
using Grasp.Logger;
using Grasp.Robot;

namespace Grasp.Workflow;

public static class CartesianSequenceBuilder
{
    public static (JointKeyframe[] approachGrab, JointKeyframe[] transport, JointKeyframe[] returnHome) BuildSequence(JointPivot[] joints, GrabPath path, float[] homeAngles,
            float approachDur, float grabDur, float liftDur, float placeDur, float returnDur)
    {
        var wp = path.Waypoints;
        float[] prevAngles = (float[])homeAngles.Clone();

        // Solve IK sequentially: each waypoint uses previous solution as initial guess
        float[] approachAngles = SolveMultiStart(joints, wp[2], prevAngles, homeAngles);
        prevAngles = approachAngles;

        float[] grabAngles = SolveMultiStart(joints, wp[1], prevAngles, homeAngles);
        prevAngles = grabAngles;

        float[] liftAngles = SolveMultiStart(joints, wp[3], prevAngles, homeAngles);
        prevAngles = liftAngles;

        float[] placeAngles = SolveMultiStart(joints, wp[4], prevAngles, homeAngles);
        prevAngles = placeAngles;

        // Segment 1: Home → Approach → GrabSurface
        var approachGrab = new JointKeyframe[]
        {
            new() { Angles = (float[])homeAngles.Clone(), DurationSec = 0.01f },
            new() { Angles = approachAngles, DurationSec = approachDur },
            new() { Angles = grabAngles, DurationSec = grabDur },
        };

        // Segment 2: GrabSurface → Lift → Place
        var transport = new JointKeyframe[]
        {
            new() { Angles = liftAngles, DurationSec = liftDur },
            new() { Angles = placeAngles, DurationSec = placeDur },
        };

        // Segment 3: Place → Home
        var returnHome = new JointKeyframe[]
        {
            new() { Angles = (float[])homeAngles.Clone(), DurationSec = returnDur },
        };

        return (approachGrab, transport, returnHome);
    }

    private static float[] SolveMultiStart(JointPivot[] joints, WaypointPose wp, float[] prevAngles, float[] homeAngles)
    {
        // 尝试 1: 沿用上一段解
        var result = IkSolver.Solve(joints, wp.Position, wp.Basis, prevAngles);
        if (result != null) return result;

        // 尝试 2: 从零位开始
        Logger.Logger.Instance.Info("CartesianSequenceBuilder",
            $"Retrying {wp.Kind} from home angles");
        result = IkSolver.Solve(joints, wp.Position, wp.Basis, homeAngles);
        if (result != null) return result;

        // 尝试 3: 纯位置 IK（忽略朝向）
        Logger.Logger.Instance.Info("CartesianSequenceBuilder",
            $"Retrying {wp.Kind} position-only from prev angles");
        result = IkSolver.Solve(joints, wp.Position, wp.Basis, prevAngles, positionOnly: true);
        if (result != null)
        {
            Logger.Logger.Instance.Warn("CartesianSequenceBuilder",
                $"{wp.Kind} solved position-only (orientation ignored)");
            return result;
        }

        // 尝试 4: 多个随机扰动起点
        for (int i = 0; i < 5; i++)
        {
            var perturbed = new float[joints.Length];
            for (int j = 0; j < joints.Length; j++)
                perturbed[j] = Mathf.Clamp(
                    homeAngles[j] + (float)GD.RandRange(-1.0, 1.0),
                    joints[j].LowerLimit, joints[j].UpperLimit);
            result = IkSolver.Solve(joints, wp.Position, wp.Basis, perturbed);
            if (result != null) return result;
        }

        Logger.Logger.Instance.Warn("CartesianSequenceBuilder",
            $"IK failed for {wp.Kind} after all attempts, using prev angles");
        return (float[])prevAngles.Clone();
    }
}
