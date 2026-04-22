using Godot;

namespace Grasp.Logger;

public partial class Logger : Node
{
    private static Logger? _instance;
    public static Logger Instance => _instance!;

    private LogLevel _minLevel = LogLevel.Info;
    private bool _fileEnabled;
    private string _filePath = "Logs/grasp_{date}.log";

    public override void _Ready()
    {
        _instance = this;
    }

    public void Configure(LogLevel level, bool fileEnabled, string filePath)
    {
        _minLevel = level;
        _fileEnabled = fileEnabled;
        _filePath = filePath;
    }

    public void Debug(string source, string message)
    {
        Log(LogLevel.Debug, source, message);
    }

    public void Info(string source, string message)
    {
        Log(LogLevel.Info, source, message);
    }

    public void Warn(string source, string message)
    {
        Log(LogLevel.Warn, source, message);
    }

    public void Error(string source, string message)
    {
        Log(LogLevel.Error, source, message);
    }

    private void Log(LogLevel level, string source, string message)
    {
        if (level < _minLevel) return;

        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string levelStr = level switch
        {
            LogLevel.Debug => "DEBUG",
            LogLevel.Info => "INFO ",
            LogLevel.Warn => "WARN ",
            LogLevel.Error => "ERROR",
            _ => "?????"
        };
        string formatted = $"[{timestamp}] [{levelStr}] [{source}] {message}";

        if (level >= LogLevel.Warn)
        {
            GD.PrintErr(formatted);
        }
        else
        {
            GD.Print(formatted);
        }

        WriteToFile(formatted);
    }

    private void WriteToFile(string line)
    {
        if (!_fileEnabled) return;

        try
        {
            string path = _filePath.Replace("{date}",
                System.DateTime.Now.ToString("yyyy-MM-dd"));
            string dir = path.GetBaseDir();
            if (!DirAccess.DirExistsAbsolute(dir))
            {
                DirAccess.MakeDirRecursiveAbsolute(dir);
            }

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.ReadWrite);
            if (file != null)
            {
                file.SeekEnd();
                file.StoreLine(line);
            }
        }
        catch
        {
            // Silent fail for file logging
        }
    }
}
