using Godot;
using Grasp.BoxWall;
using Grasp.Logger;

namespace Grasp.Workflow;

public static class GrabPathCalculator
{
    public static GrabPath Compute(BoxInstance box, Basis endEffectorBasis, GrabPathConfig config)
    {
        var boxRotBasis = new Basis(box.RotationQuat);

        // Waypoint 0: BoxCenter
        Vector3 boxCenter = box.MessCenter;
        

        var boxCenterWp = new WaypointPose
        {
            Kind = WaypointKind.BoxCenter,
            Position = boxCenter,
            Orientation = box.RotationQuat,
            Basis = boxRotBasis
        };

        // Waypoint 1: GrabSurface (TCP 对齐到抓取面)
        var grabTransform = BoxAttachController.CalculateGrabTransform(box, endEffectorBasis, out _);
        Basis grabTcpBasis = grabTransform.Basis;
        Vector3 grabTcpPos = grabTransform.Origin;
        Logger.Logger.Instance.Info("Compute", $"grabTcpBasis: {grabTcpBasis}");
        Logger.Logger.Instance.Info("Compute", $"grabTcpPos: {grabTcpPos}");

        Logger.Logger.Instance.Info("Compute", $"config.TcpRotationOffset: {config.TcpRotationOffset}");
        // 反算 EE 位姿: EE = TCP - offset
        Basis grabEeBasis = TcpToEeBasis(grabTcpBasis, config.TcpRotationOffset);
        Vector3 grabEePos = TcpToEePos(grabTcpPos, grabEeBasis, config.TcpOffset);
        Logger.Logger.Instance.Info("Compute", $"grabEeBasis: {grabEeBasis}");
        Logger.Logger.Instance.Info("Compute", $"grabEePos: {grabEePos}");

        var grabWp = new WaypointPose
        {
            Kind = WaypointKind.GrabSurface,
            Position = grabEePos,
            Orientation = grabEeBasis.GetRotationQuaternion(),
            Basis = grabEeBasis
        };

        // Waypoints 2 & 3: Approach + Lift
        var (approachWp, liftWp) = ComputeTransitWaypoints(grabWp, config);

        // Waypoint 4: Place (固定放置位姿)
        Logger.Logger.Instance.Info("Compute", $"tcp basis: {new Basis(config.PlaceRotation)}");
        Logger.Logger.Instance.Info("Compute", $"tcp pos: {config.PlacePosition}");
        Basis placeEeBasis = TcpToEeBasis(new Basis(config.PlaceRotation), config.TcpRotationOffset);
        Vector3 placeEePos = TcpToEePos(config.PlacePosition, placeEeBasis, config.TcpOffset);
        Logger.Logger.Instance.Info("Compute", $"placeEeBasis: {placeEeBasis}");
        Logger.Logger.Instance.Info("Compute", $"placeEePos: {placeEePos}");

        var placeWp = new WaypointPose
        {
            Kind = WaypointKind.Place,
            Position = placeEePos,
            Orientation = placeEeBasis.GetRotationQuaternion(),
            Basis = placeEeBasis
        };

        return new GrabPath
        {
            BoxId = box.Id,
            Waypoints = new[] { boxCenterWp, grabWp, approachWp, liftWp, placeWp }
        };
    }

    /// <summary>
    /// 从 grab 位姿计算 approach 和 lift 路径点。
    /// approach: 沿 EE 的 -X 方向退后（即 TCP 远离抓取面）
    /// lift: 沿可配置方向偏移
    /// </summary>
    public static (WaypointPose approach, WaypointPose lift) ComputeTransitWaypoints(
        WaypointPose grab, GrabPathConfig config)
    {
        // Approach: 沿 grab Basis 的 -X 退后
        Vector3 approachPos = grab.Position - grab.Basis.X.Normalized() * config.ApproachDistance;
        var approachWp = new WaypointPose
        {
            Kind = WaypointKind.Approach,
            Position = approachPos,
            Orientation = grab.Orientation,
            Basis = grab.Basis
        };

        // Lift: 沿可配置方向偏移
        Vector3 liftPos = grab.Position + config.LiftDirection.Normalized() * config.LiftDistance;
        var liftWp = new WaypointPose
        {
            Kind = WaypointKind.Lift,
            Position = liftPos,
            Orientation = grab.Orientation,
            Basis = grab.Basis
        };

        return (approachWp, liftWp);
    }

    /// <summary>
    /// TCP Basis 反算 EE Basis: eeBasis = tcpBasis * tcpRotationOffset.Inverse()
    /// </summary>
    private static Basis TcpToEeBasis(Basis tcpBasis, Basis tcpRotationOffset)
    {
        return tcpBasis * tcpRotationOffset.Inverse();
    }

    /// <summary>
    /// TCP 位置反算 EE 位置: eePos = tcpPos - eeBasis.X * tcpOffset
    /// </summary>
    private static Vector3 TcpToEePos(Vector3 tcpPos, Basis eeBasis, float tcpOffset)
    {
        return tcpPos - eeBasis.X.Normalized() * tcpOffset;
    }

    public static void PrintPath(GrabPath path)
    {
        Logger.Logger.Instance.Info("GrabPath",
            $"=== GrabPath for Box {path.BoxId} ===");
        foreach (var wp in path.Waypoints)
        {
            Logger.Logger.Instance.Info("GrabPath",
                $"{wp.Kind}: " +
                $"pos:({wp.Position.X:F3}, {wp.Position.Y:F3}, {wp.Position.Z:F3}), " +
                $"quat:({wp.Orientation.X:F3}, {wp.Orientation.Y:F3}, {wp.Orientation.Z:F3}, {wp.Orientation.W:F3}), " +
                $"basis: X=({wp.Basis.X.X:F3},{wp.Basis.X.Y:F3},{wp.Basis.X.Z:F3}), " +
                $"Y=({wp.Basis.Y.X:F3},{wp.Basis.Y.Y:F3},{wp.Basis.Y.Z:F3}), " +
                $"Z=({wp.Basis.Z.X:F3},{wp.Basis.Z.Y:F3},{wp.Basis.Z.Z:F3})");
        }
    }
}
