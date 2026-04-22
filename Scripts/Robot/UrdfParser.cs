using System.Xml;
using Godot;
using Grasp.Logger;

namespace Grasp.Robot;

public class LinkData
{
    public string Name { get; set; } = "";
}

public class JointData
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string ParentLink { get; set; } = "";
    public string ChildLink { get; set; } = "";
    public Vector3 OriginXyz { get; set; }
    public Vector3 OriginRpy { get; set; }
    public Vector3 Axis { get; set; } = Vector3.Up;
    public float Lower { get; set; }
    public float Upper { get; set; }
}

public class RobotData
{
    public string Name { get; set; } = "";
    public LinkData[] Links { get; set; } = System.Array.Empty<LinkData>();
    public JointData[] Joints { get; set; } = System.Array.Empty<JointData>();
    public int JointCount => Joints.Length;
}

public static class UrdfParser
{
    public static RobotData? Parse(string robotPath)
    {
        string urdfDir = $"{robotPath}/urdf/";
        string urdfFile = FindFirstUrdf(urdfDir);
        if (urdfFile == null)
        {
            Logger.Logger.Instance.Error("UrdfParser", $"No .urdf file found in {urdfDir}");
            return null;
        }

        Logger.Logger.Instance.Info("UrdfParser", $"Parsing URDF: {urdfFile}");

        try
        {
            var doc = new XmlDocument();
            string fullPath = $"res://{urdfFile}";
            using var file = FileAccess.Open(fullPath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                Logger.Logger.Instance.Error("UrdfParser", $"Failed to open URDF file: {fullPath}");
                return null;
            }
            doc.LoadXml(file.GetAsText());

            var robotNode = doc.SelectSingleNode("robot");
            if (robotNode == null)
            {
                Logger.Logger.Instance.Error("UrdfParser", "No <robot> element found in URDF");
                return null;
            }

            var robotData = new RobotData
            {
                Name = robotNode.Attributes?["name"]?.Value ?? "unknown"
            };

            // Extract links
            var linkNodes = robotNode.SelectNodes("link");
            var links = new System.Collections.Generic.List<LinkData>();
            foreach (XmlNode linkNode in linkNodes)
            {
                links.Add(new LinkData
                {
                    Name = linkNode.Attributes?["name"]?.Value ?? ""
                });
            }
            robotData.Links = links.ToArray();

            // Extract revolute joints only
            var jointNodes = robotNode.SelectNodes("joint");
            var joints = new System.Collections.Generic.List<JointData>();
            foreach (XmlNode jointNode in jointNodes)
            {
                string jointType = jointNode.Attributes?["type"]?.Value ?? "";
                if (jointType != "revolute" && jointType != "continuous" && jointType != "prismatic")
                    continue;

                var joint = new JointData
                {
                    Name = jointNode.Attributes?["name"]?.Value ?? "",
                    Type = jointType
                };

                var parent = jointNode.SelectSingleNode("parent");
                joint.ParentLink = parent?.Attributes?["link"]?.Value ?? "";

                var child = jointNode.SelectSingleNode("child");
                joint.ChildLink = child?.Attributes?["link"]?.Value ?? "";

                var origin = jointNode.SelectSingleNode("origin");
                if (origin != null)
                {
                    joint.OriginXyz = ParseVec3(origin.Attributes?["xyz"]?.Value);
                    joint.OriginRpy = ParseVec3(origin.Attributes?["rpy"]?.Value);
                }

                var axis = jointNode.SelectSingleNode("axis");
                if (axis != null)
                {
                    joint.Axis = ParseVec3(axis.Attributes?["xyz"]?.Value).Normalized();
                }

                var limit = jointNode.SelectSingleNode("limit");
                if (limit != null)
                {
                    float.TryParse(limit.Attributes?["lower"]?.Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out joint.Lower);
                    float.TryParse(limit.Attributes?["upper"]?.Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out joint.Upper);
                }

                joints.Add(joint);
            }

            // Topological sort: ensure parent joints come before child joints
            robotData.Joints = TopologicalSort(joints, robotData.Links);

            Logger.Logger.Instance.Info("UrdfParser",
                $"Parsed robot '{robotData.Name}': {robotData.Links.Length} links, {robotData.Joints.Length} revolute joints");

            return robotData;
        }
        catch (System.Exception e)
        {
            Logger.Logger.Instance.Error("UrdfParser", $"Error parsing URDF: {e.Message}");
            return null;
        }
    }

    private static string? FindFirstUrdf(string urdfDir)
    {
        using var dir = DirAccess.Open($"res://{urdfDir}");
        if (dir == null) return null;

        string[] files = dir.GetFiles();
        foreach (string file in files)
        {
            if (file.EndsWith(".urdf"))
                return urdfDir + file;
        }
        return null;
    }

    private static Vector3 ParseVec3(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Vector3.Zero;

        var parts = value.Trim().Split(new[] { ' ', '\t' },
            System.StringSplitOptions.RemoveEmptyEntries);
        float x = 0, y = 0, z = 0;
        if (parts.Length >= 1)
            float.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out x);
        if (parts.Length >= 2)
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out y);
        if (parts.Length >= 3)
            float.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out z);
        return new Vector3(x, y, z);
    }

    private static JointData[] TopologicalSort(
        System.Collections.Generic.List<JointData> joints, LinkData[] links)
    {
        // Find root link: a parent link that is not a child of any revolute joint
        var allParents = new System.Collections.Generic.HashSet<string>();
        var allChildren = new System.Collections.Generic.HashSet<string>();
        foreach (var joint in joints)
        {
            allParents.Add(joint.ParentLink);
            allChildren.Add(joint.ChildLink);
        }

        string? rootLink = null;
        foreach (var p in allParents)
        {
            if (!allChildren.Contains(p))
            {
                rootLink = p;
                break;
            }
        }

        // BFS from root
        var sorted = new System.Collections.Generic.List<JointData>();
        var queue = new System.Collections.Generic.Queue<string>();
        if (rootLink != null) queue.Enqueue(rootLink);

        var visited = new System.Collections.Generic.HashSet<string>();
        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            if (!visited.Add(current)) continue;

            foreach (var joint in joints)
            {
                if (joint.ParentLink == current && !visited.Contains(joint.ChildLink))
                {
                    sorted.Add(joint);
                    queue.Enqueue(joint.ChildLink);
                }
            }
        }

        // Add any joints not reached by BFS (shouldn't happen for well-formed URDF)
        foreach (var joint in joints)
        {
            if (!sorted.Contains(joint)) sorted.Add(joint);
        }

        return sorted.ToArray();
    }
}
