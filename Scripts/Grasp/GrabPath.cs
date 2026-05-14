namespace Grasp.Workflow;

public class GrabPath
{
    public int BoxId;
    public WaypointPose[] Waypoints = System.Array.Empty<WaypointPose>();

    public ref WaypointPose BoxCenter => ref Waypoints[0];
    public ref WaypointPose GrabSurface => ref Waypoints[1];
    public ref WaypointPose Approach => ref Waypoints[2];
    public ref WaypointPose Lift => ref Waypoints[3];
    public ref WaypointPose Place => ref Waypoints[4];
}
