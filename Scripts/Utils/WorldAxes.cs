using Godot;

namespace Grasp.Utils;

public partial class WorldAxes : Node3D
{
    [Export] public float Length { get; set; } = 2.0f;
    [Export] public float LineWidth { get; set; } = 0.02f;
    [Export] public bool VisibleByDefault { get; set; } = true;

    private MeshInstance3D? _meshInstance;
    private bool _toggled = true;

    public override void _Ready()
    {
        _toggled = VisibleByDefault;
        BuildAxes();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.G })
        {
            Toggle();
        }
    }

    public void Toggle()
    {
        _toggled = !_toggled;
        if (_meshInstance != null)
            _meshInstance.Visible = _toggled;
    }

    private void BuildAxes()
    {
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);

        // X axis - Red
        mesh.SurfaceSetColor(Colors.Red);
        mesh.SurfaceAddVertex(Vector3.Zero);
        mesh.SurfaceAddVertex(new Vector3(Length, 0, 0));

        // Y axis - Green
        mesh.SurfaceSetColor(Colors.Green);
        mesh.SurfaceAddVertex(Vector3.Zero);
        mesh.SurfaceAddVertex(new Vector3(0, Length, 0));

        // Z axis - Blue
        mesh.SurfaceSetColor(Colors.Blue);
        mesh.SurfaceAddVertex(Vector3.Zero);
        mesh.SurfaceAddVertex(new Vector3(0, 0, Length));

        mesh.SurfaceEnd();

        var mat = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Disabled
        };

        _meshInstance = new MeshInstance3D
        {
            Name = "WorldAxes",
            Mesh = mesh,
            MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _meshInstance.Visible = _toggled;

        AddChild(_meshInstance);
    }
}
