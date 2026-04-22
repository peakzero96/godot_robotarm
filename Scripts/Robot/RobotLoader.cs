using Godot;
using Grasp.Logger;

namespace Grasp.Robot;

public class RobotLoadResult
{
    public Node3D RootNode { get; set; } = null!;
    public JointPivot[] Joints { get; set; } = System.Array.Empty<JointPivot>();
    public int JointCount { get; set; }
    public Node3D? Gripper { get; set; }
    public string RobotName { get; set; } = "";
}

public static class RobotLoader
{
    public static RobotLoadResult? Load(string robotPath)
    {
        var robotData = UrdfParser.Parse(robotPath);
        if (robotData == null) return null;

        var root = new Node3D { Name = "RobotRoot" };

        // Find base link (not a child of any revolute joint)
        var childLinks = new System.Collections.Generic.HashSet<string>();
        foreach (var joint in robotData.Joints)
            childLinks.Add(joint.ChildLink);

        string? baseLinkName = null;
        foreach (var link in robotData.Links)
        {
            if (!childLinks.Contains(link.Name))
            {
                baseLinkName = link.Name;
                break;
            }
        }

        if (baseLinkName == null)
        {
            Logger.Logger.Instance.Error("RobotLoader", "Cannot find base link");
            return null;
        }

        // Create base link mesh
        var baseLinkNode = LoadLinkNode(baseLinkName, robotPath);
        if (baseLinkNode != null)
        {
            baseLinkNode.Name = baseLinkName;
            root.AddChild(baseLinkNode);
        }

        // Build joint hierarchy
        var joints = new System.Collections.Generic.List<JointPivot>();
        var linkNodes = new System.Collections.Generic.Dictionary<string, Node3D>();

        if (baseLinkNode != null)
            linkNodes[baseLinkName] = baseLinkNode;

        Node3D? lastLinkNode = baseLinkNode;
        Node3D? gripper = null;

        foreach (var joint in robotData.Joints)
        {
            // Find parent link node
            if (!linkNodes.TryGetValue(joint.ParentLink, out var parentNode))
            {
                Logger.Logger.Instance.Warn("RobotLoader",
                    $"Parent link '{joint.ParentLink}' not found for joint '{joint.Name}'");
                continue;
            }

            // Create joint pivot
            var pivot = new JointPivot
            {
                Name = $"{joint.Name}_pivot",
                JointName = joint.Name,
                RotationAxis = joint.Axis,
                LowerLimit = joint.Lower,
                UpperLimit = joint.Upper,
                Position = CoordinateConverter.ConvertPosition(joint.OriginXyz)
            };
            pivot.SetBaseRotation(CoordinateConverter.ConvertRotation(joint.OriginRpy));
            parentNode.AddChild(pivot);
            joints.Add(pivot);

            // Load child link mesh
            var childMesh = LoadLinkNode(joint.ChildLink, robotPath);
            if (childMesh != null)
            {
                childMesh.Name = joint.ChildLink;
                pivot.AddChild(childMesh);
                linkNodes[joint.ChildLink] = childMesh;
                lastLinkNode = childMesh;
            }
        }

        // Create gripper node at the last joint pivot
        if (joints.Count > 0)
        {
            gripper = new Node3D { Name = "gripper" };
            joints[^1].AddChild(gripper);
        }

        Logger.Logger.Instance.Info("RobotLoader",
            $"Loaded robot '{robotData.Name}': {joints.Count} joints");

        return new RobotLoadResult
        {
            RootNode = root,
            Joints = joints.ToArray(),
            JointCount = joints.Count,
            Gripper = gripper,
            RobotName = robotData.Name
        };
    }

    private static Node3D? LoadLinkNode(string linkName, string robotPath)
    {
        string meshPath = $"res://{robotPath}/meshes/visual/{linkName}.dae";

        if (!ResourceLoader.Exists(meshPath))
        {
            Logger.Logger.Instance.Warn("RobotLoader",
                $"Mesh not found: {meshPath}");
            return null;
        }

        try
        {
            var scene = ResourceLoader.Load<PackedScene>(meshPath);
            if (scene == null)
            {
                Logger.Logger.Instance.Warn("RobotLoader",
                    $"Failed to load scene: {meshPath}");
                return null;
            }

            var instance = scene.Instantiate();
            if (instance is Node3D node3D)
            {
                return node3D;
            }

            if (instance != null)
            {
                var wrapper = new Node3D { Name = $"{linkName}_wrapper" };
                wrapper.AddChild(instance);
                return wrapper;
            }

            return null;
        }
        catch (System.Exception e)
        {
            Logger.Logger.Instance.Error("RobotLoader",
                $"Error loading mesh '{linkName}': {e.Message}");
            return null;
        }
    }
}
