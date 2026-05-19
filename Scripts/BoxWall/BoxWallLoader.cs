using Godot;
using Grasp.Logger;
using System.Collections.Generic;

namespace Grasp.BoxWall;

public class BoxWallLoadResult
{
    public MultiMeshInstance3D MeshInstance { get; set; } = null!;
    public Node3D? AxesContainer { get; set; }
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

        // for (int i = 0; i < boxesArray.Count; i++)
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
            var quatData = boxData.TryGetValue("rotation_quat", out var qv)
                ? qv.AsGodotDictionary() : null;
            var size = boxData.TryGetValue("size", out var sv)
                ? sv.AsGodotDictionary() : null;
            string colorStr = boxData.TryGetValue("color", out var cv)
                ? cv.AsString() : defaultColor;

            Quaternion rotationQuat = Quaternion.Identity;
            if (quatData != null)
            {
                rotationQuat = new Quaternion(
                    GetNum(quatData, "x", 0), GetNum(quatData, "y", 0),
                    GetNum(quatData, "z", 0), GetNum(quatData, "w", 1)).Normalized();
            }

            var scale = new Vector3(
                    GetNum(size, "z", 0.6f), GetNum(size, "y", 0.2f), GetNum(size, "x", 0.3f));
            var basis = new Basis(rotationQuat).Scaled(scale);

            Logger.Logger.Instance.Info("BoxWallLoader",
                $"Loaded box {i}");
            Logger.Logger.Instance.Info("BoxWallLoader",
                $"Box {i} position: {pos}");
            Logger.Logger.Instance.Info("BoxWallLoader",
                $"Box {i} rotation: {quatData}");
            Logger.Logger.Instance.Info("BoxWallLoader",
                $"Box {i} size: {size}");
            Logger.Logger.Instance.Info("BoxWallLoader",
                $"Box {i} scale: {scale}");
            Logger.Logger.Instance.Info("BoxWallLoader",
                $"Box {i} basis: {basis}");
            Vector3 _position = new Vector3(
                    GetNum(pos, "x", 0) - 0.5f, GetNum(pos, "y", 0), GetNum(pos, "z", 0));
            Vector3 _messCenter = _position + basis.Z / 2f;

            Logger.Logger.Instance.Info("BoxWallLoader",
                $"Box {i} _messCenter: {_messCenter}");


            boxes.Add(new BoxInstance
            {
                Id = boxData.TryGetValue("id", out var idv) ? (int)idv.AsDouble() : i,
                // 此处Position为箱子表面识别中心点
                Position = _position,
                RotationQuat = rotationQuat,
                Size = new Vector3(
                    GetNum(size, "x", 0.3f), GetNum(size, "y", 0.2f), GetNum(size, "z", 0.6f)),
                Color = ParseColor(colorStr),
                MultiMeshIndex = i,
                MessCenter = _messCenter,//TODO： 此处硬编码箱子厚度z方向
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

            // 四元数定义的局部轴含义: Z=向内(depth), X=宽(height), Y=高(width)
            // Size 存储: (width, height, depth)，需要重排到 (depth, width, height) 对应 (X, Y, Z)
            var scale = new Vector3(box.Size.Z, box.Size.Y, box.Size.X);
            var basis = new Basis(box.RotationQuat).Scaled(scale);

            // Position 是表面中心 (Size.x * Size.y 面的中心)，沿 +Z(向内) 偏移 depth/2 到达箱体质心
            Vector3 center = box.MessCenter;         

            var transform = new Transform3D(basis, center);
            multiMesh.SetInstanceTransform(i, transform);
            multiMesh.SetInstanceColor(i, box.Color);
        }

        Logger.Logger.Instance.Info("BoxWallLoader",
            $"Created MultiMesh with {boxes.Count} boxes");

        // Wireframe edge overlay using ImmediateMesh (12 edges per box)
        var wireMesh = new ImmediateMesh();
        wireMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, null);

        for (int i = 0; i < boxes.Count; i++)
        {
            var t = multiMesh.GetInstanceTransform(i);
            var s = new Vector3(1, 1, 1); // box is unit cube scaled by basis
            // 8 corners of unit cube
            Vector3[] corners =
            {
                new(-0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, -0.5f),
                new(0.5f, 0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f),
                new(-0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, 0.5f),
                new(0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f),
            };
            // Transform corners by the instance transform
            for (int c = 0; c < 8; c++)
                corners[c] = t * corners[c];

            int[][] edges =
            {
                new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 0 }, // back face
                new[] { 4, 5 }, new[] { 5, 6 }, new[] { 6, 7 }, new[] { 7, 4 }, // front face
                new[] { 0, 4 }, new[] { 1, 5 }, new[] { 2, 6 }, new[] { 3, 7 }, // connecting
            };
            foreach (var edge in edges)
            {
                wireMesh.SurfaceAddVertex(corners[edge[0]]);
                wireMesh.SurfaceAddVertex(corners[edge[1]]);
            }
        }
        wireMesh.SurfaceEnd();

        var wireMat = new StandardMaterial3D
        {
            ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.3f, 0.3f, 0.3f),
            VertexColorUseAsAlbedo = false
        };

        var wireInstance = new MeshInstance3D
        {
            Name = "BoxWallWireframe",
            Mesh = wireMesh,
            MaterialOverride = wireMat
        };
        meshInstance.AddChild(wireInstance);

        // [DEBUG] 每个箱子本地坐标系可视化 — 调试完毕后删除此段 + BoxWallManager 中 B 键切换逻辑
        float axisLen = 0.5f;
        var axesMesh = new ImmediateMesh();
        axesMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);

        for (int i = 0; i < boxes.Count; i++)
        {
            var t = multiMesh.GetInstanceTransform(i);
            Logger.Logger.Instance.Info("CreateMultiMesh", $"box basis: {t}");
            
            Vector3 o = t.Origin;
            // Basis contains box scale, normalize to get pure direction
            Vector3 bx = t.Basis.X.Normalized();
            Vector3 by = t.Basis.Y.Normalized();
            Vector3 bz = t.Basis.Z.Normalized();

            // X axis - Red
            axesMesh.SurfaceSetColor(new Color(1, 0.2f, 0.2f));
            axesMesh.SurfaceAddVertex(o);
            axesMesh.SurfaceAddVertex(o + bx * axisLen);
            // Y axis - Green
            axesMesh.SurfaceSetColor(new Color(0.2f, 1, 0.2f));
            axesMesh.SurfaceAddVertex(o);
            axesMesh.SurfaceAddVertex(o + by * axisLen);
            // Z axis - Blue
            axesMesh.SurfaceSetColor(new Color(0.2f, 0.2f, 1));
            axesMesh.SurfaceAddVertex(o);
            axesMesh.SurfaceAddVertex(o + bz * axisLen);
        }

        axesMesh.SurfaceEnd();

        var axesMat = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Disabled
        };

        var axesInstance = new MeshInstance3D
        {
            Name = "BoxWallAxes",
            Mesh = axesMesh,
            MaterialOverride = axesMat
        };
        meshInstance.AddChild(axesInstance);

        return new BoxWallLoadResult
        {
            MeshInstance = meshInstance,
            AxesContainer = axesInstance,
            Boxes = boxes.ToArray(),
            TotalCount = boxes.Count
        };
    }

    public static void RefreshOverlays(MultiMeshInstance3D meshInstance, BoxInstance[] boxes)
    {
        if (meshInstance == null) return;
        var multiMesh = meshInstance.Multimesh;
        if (multiMesh == null) return;

        // Remove old wireframe and axes children
        foreach (var child in meshInstance.GetChildren())
        {
            if (child.Name == "BoxWallWireframe" || child.Name == "BoxWallAxes")
                child.QueueFree();
        }

        // Rebuild wireframe, skipping grabbed/released boxes
        var wireMesh = new ImmediateMesh();
        wireMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, null);

        Vector3[] corners =
        {
            new(-0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, -0.5f),
            new(0.5f, 0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f),
            new(-0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, 0.5f),
            new(0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f),
        };
        int[][] edges =
        {
            new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 0 },
            new[] { 4, 5 }, new[] { 5, 6 }, new[] { 6, 7 }, new[] { 7, 4 },
            new[] { 0, 4 }, new[] { 1, 5 }, new[] { 2, 6 }, new[] { 3, 7 },
        };

        // Rebuild axes
        float axisLen = 0.5f;
        var axesMesh = new ImmediateMesh();
        axesMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);

        for (int i = 0; i < boxes.Length; i++)
        {
            var box = boxes[i];
            if (box.State == BoxState.Grabbed || box.State == BoxState.Released) continue;

            var t = multiMesh.GetInstanceTransform(i);

            // Wireframe
            Vector3[] c = new Vector3[8];
            for (int c2 = 0; c2 < 8; c2++)
                c[c2] = t * corners[c2];
            foreach (var edge in edges)
            {
                wireMesh.SurfaceAddVertex(c[edge[0]]);
                wireMesh.SurfaceAddVertex(c[edge[1]]);
            }

            // Axes
            Vector3 o = t.Origin;
            Vector3 bx = t.Basis.X.Normalized();
            Vector3 by = t.Basis.Y.Normalized();
            Vector3 bz = t.Basis.Z.Normalized();
            axesMesh.SurfaceSetColor(new Color(1, 0.2f, 0.2f));
            axesMesh.SurfaceAddVertex(o);
            axesMesh.SurfaceAddVertex(o + bx * axisLen);
            axesMesh.SurfaceSetColor(new Color(0.2f, 1, 0.2f));
            axesMesh.SurfaceAddVertex(o);
            axesMesh.SurfaceAddVertex(o + by * axisLen);
            axesMesh.SurfaceSetColor(new Color(0.2f, 0.2f, 1));
            axesMesh.SurfaceAddVertex(o);
            axesMesh.SurfaceAddVertex(o + bz * axisLen);
        }

        wireMesh.SurfaceEnd();
        var wireMat = new StandardMaterial3D
        {
            ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.3f, 0.3f, 0.3f),
            VertexColorUseAsAlbedo = false
        };
        var wireInstance = new MeshInstance3D
        {
            Name = "BoxWallWireframe",
            Mesh = wireMesh,
            MaterialOverride = wireMat
        };
        meshInstance.AddChild(wireInstance);

        axesMesh.SurfaceEnd();
        var axesMat = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true
        };
        var axesInstance = new MeshInstance3D
        {
            Name = "BoxWallAxes",
            Mesh = axesMesh,
            MaterialOverride = axesMat
        };
        meshInstance.AddChild(axesInstance);
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
