using Godot;
using Grasp.Logger;
using System.Collections.Generic;

namespace Grasp.BoxWall;

public class BoxWallLoadResult
{
    public MultiMeshInstance3D MeshInstance { get; set; } = null!;
    public BoxInstance[] Boxes { get; set; } = System.Array.Empty<BoxInstance>();
    public int TotalCount { get; set; }
}

public static class BoxWallLoader
{
    public static BoxWallLoadResult? Load(string jsonData)
    {
        var parsed = Json.ParseString(jsonData).AsGodotDictionary();
        if (parsed == null || !parsed.TryGetValue("boxes", out var boxesVariant))
        {
            Logger.Logger.Instance.Error("BoxWallLoader", "Failed to parse box wall JSON or missing 'boxes'");
            return null;
        }

        var boxesArray = boxesVariant.AsGodotArray();
        if (boxesArray == null || boxesArray.Count == 0)
        {
            Logger.Logger.Instance.Warn("BoxWallLoader", "No boxes in JSON data");
            return null;
        }

        string defaultColor = Grasp.Main.AppConfig.Instance.BoxDefaultColor;
        var boxes = new List<BoxInstance>();

        for (int i = 0; i < boxesArray.Count; i++)
        {
            var boxData = boxesArray[i].AsGodotDictionary();
            if (boxData == null) continue;

            float GetNum(Godot.Collections.Dictionary? dict, string key, float fallback)
            {
                if (dict == null || !dict.TryGetValue(key, out var v)) return fallback;
                return (float)v.AsDouble();
            }

            var pos = boxData.TryGetValue("position", out var pv)
                ? pv.AsGodotDictionary() : null;
            var rot = boxData.TryGetValue("rotation_deg", out var rv)
                ? rv.AsGodotDictionary() : null;
            var size = boxData.TryGetValue("size", out var sv)
                ? sv.AsGodotDictionary() : null;
            string colorStr = boxData.TryGetValue("color", out var cv)
                ? cv.AsString() : defaultColor;

            boxes.Add(new BoxInstance
            {
                Id = boxData.TryGetValue("id", out var idv) ? (int)idv.AsDouble() : i,
                Position = new Vector3(
                    GetNum(pos, "x", 0), GetNum(pos, "y", 0), GetNum(pos, "z", 0)),
                RotationDeg = new Vector3(
                    GetNum(rot, "x", 0), GetNum(rot, "y", 0), GetNum(rot, "z", 0)),
                Size = new Vector3(
                    GetNum(size, "x", 0.3f), GetNum(size, "y", 0.2f), GetNum(size, "z", 0.6f)),
                Color = ParseColor(colorStr),
                MultiMeshIndex = i
            });
        }

        return CreateMultiMesh(boxes);
    }

    private static BoxWallLoadResult CreateMultiMesh(List<BoxInstance> boxes)
    {
        var boxMesh = new BoxMesh();
        var multiMesh = new MultiMesh();
        multiMesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
        multiMesh.UseColors = true;
        multiMesh.Mesh = boxMesh;
        multiMesh.InstanceCount = boxes.Count;

        var meshInstance = new MultiMeshInstance3D { Name = "BoxWall", Multimesh = multiMesh };

        for (int i = 0; i < boxes.Count; i++)
        {
            var box = boxes[i];
            // Scale → Rotate → Translate (left-multiply in Godot = apply last to first)
            var basis = Basis.Identity
                .Scaled(box.Size)
                .Rotated(Vector3.Up, Mathf.DegToRad(box.RotationDeg.Y))
                .Rotated(Vector3.Right, Mathf.DegToRad(box.RotationDeg.X))
                .Rotated(Vector3.Forward, Mathf.DegToRad(box.RotationDeg.Z));
            var transform = new Transform3D(basis, box.Position);

            multiMesh.SetInstanceTransform(i, transform);
            multiMesh.SetInstanceColor(i, box.Color);
        }

        Logger.Logger.Instance.Info("BoxWallLoader",
            $"Created MultiMesh with {boxes.Count} boxes");

        return new BoxWallLoadResult
        {
            MeshInstance = meshInstance,
            Boxes = boxes.ToArray(),
            TotalCount = boxes.Count
        };
    }

    private static Color ParseColor(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return new Color("#C4A882");
        try
        {
            return new Color(hex);
        }
        catch
        {
            return new Color("#C4A882");
        }
    }
}
