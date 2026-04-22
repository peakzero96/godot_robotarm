using Godot;

namespace Grasp.Main;

public partial class AppConfig : Node
{
    public static AppConfig Instance { get; private set; } = null!;

    // Network
    public int ListenPort { get; private set; } = 5005;
    public int MaxConnections { get; private set; } = 1;
    public int HeartbeatIntervalMs { get; private set; } = 1000;
    public int ReconnectTimeoutMs { get; private set; } = 5000;

    // Robot
    public string RobotPath { get; private set; } = "robot_arm/abb_irb4600_60_205";
    public int UpdateRateHz { get; private set; } = 60;

    // Scene
    public string DefaultCamera { get; private set; } = "free_orbit";
    public string EnvironmentPath { get; private set; } = "Scenes/Environment/FactoryFloor.tscn";

    // Logging
    public string LogLevelStr { get; private set; } = "INFO";
    public bool FileLoggingEnabled { get; private set; } = true;
    public string LogFilePath { get; private set; } = "Logs/grasp_{date}.log";
    public int UiMaxLines { get; private set; } = 500;

    // Box Wall
    public string BoxDefaultColor { get; private set; } = "#C4A882";
    public string BoxGrabbedColor { get; private set; } = "#4CAF50";
    public int FadeOutDurationMs { get; private set; } = 2000;

    public override void _Ready()
    {
        Instance = this;
        LoadConfig();
    }

    private void LoadConfig()
    {
        string path = "res://Config/app.json";
        if (!FileAccess.FileExists(path))
        {
            GD.PrintErr($"[AppConfig] Config file not found: {path}, using defaults");
            return;
        }

        try
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"[AppConfig] Failed to open config file: {path}");
                return;
            }

            string json = file.GetAsText();
            var parsed = Json.ParseString(json).AsGodotDictionary();

            if (parsed == null)
            {
                GD.PrintErr("[AppConfig] Failed to parse JSON");
                return;
            }

            // Network
            if (parsed.TryGetValue("network", out var network))
            {
                var net = network.AsGodotDictionary();
                if (net != null)
                {
                    if (net.TryGetValue("listen_port", out var v)) ListenPort = (int)v.AsDouble();
                    if (net.TryGetValue("max_connections", out v)) MaxConnections = (int)v.AsDouble();
                    if (net.TryGetValue("heartbeat_interval_ms", out v)) HeartbeatIntervalMs = (int)v.AsDouble();
                    if (net.TryGetValue("reconnect_timeout_ms", out v)) ReconnectTimeoutMs = (int)v.AsDouble();
                }
            }

            // Robot
            if (parsed.TryGetValue("robot", out var robot))
            {
                var rob = robot.AsGodotDictionary();
                if (rob != null)
                {
                    if (rob.TryGetValue("default_path", out var v)) RobotPath = v.AsString();
                    if (rob.TryGetValue("update_rate_hz", out v)) UpdateRateHz = (int)v.AsDouble();
                }
            }

            // Scene
            if (parsed.TryGetValue("scene", out var scene))
            {
                var scn = scene.AsGodotDictionary();
                if (scn != null)
                {
                    if (scn.TryGetValue("default_camera", out var v)) DefaultCamera = v.AsString();
                    if (scn.TryGetValue("environment", out v)) EnvironmentPath = v.AsString();
                }
            }

            // Logging
            if (parsed.TryGetValue("logging", out var logging))
            {
                var log = logging.AsGodotDictionary();
                if (log != null)
                {
                    if (log.TryGetValue("level", out var v)) LogLevelStr = v.AsString();
                    if (log.TryGetValue("file_enabled", out var v2)) FileLoggingEnabled = v2.AsBool();
                    if (log.TryGetValue("file_path", out var v3)) LogFilePath = v3.AsString();
                    if (log.TryGetValue("ui_max_lines", out var v4)) UiMaxLines = (int)v4.AsDouble();
                }
            }

            // Box Wall
            if (parsed.TryGetValue("box_wall", out var boxWall))
            {
                var bw = boxWall.AsGodotDictionary();
                if (bw != null)
                {
                    if (bw.TryGetValue("default_color", out var v)) BoxDefaultColor = v.AsString();
                    if (bw.TryGetValue("grabbed_color", out var v2)) BoxGrabbedColor = v2.AsString();
                    if (bw.TryGetValue("fade_out_duration_ms", out var v3)) FadeOutDurationMs = (int)v3.AsDouble();
                }
            }

            GD.Print($"[AppConfig] Config loaded: robot={RobotPath}, port={ListenPort}");
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"[AppConfig] Error loading config: {e.Message}");
        }
    }
}
