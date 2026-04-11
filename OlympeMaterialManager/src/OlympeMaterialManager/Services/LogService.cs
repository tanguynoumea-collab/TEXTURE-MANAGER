using System.IO;

namespace Olympe.MaterialManager.Services;

/// <summary>
/// Service de logging fichier pour le diagnostic.
/// Ecrit dans %APPDATA%/Olympe/MaterialManager/olympe.log
/// </summary>
public static class LogService
{
    private static readonly string _logPath;
    private static readonly object _lock = new();

    static LogService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Olympe", "MaterialManager");
        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, "olympe.log");

        // Vider le log au demarrage
        try { File.WriteAllText(_logPath, $"=== Olympe MaterialManager Log - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n"); }
        catch { }
    }

    public static void Log(string message)
    {
        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
            }
            catch { }
        }
    }

    public static void Error(string message, Exception? ex = null)
    {
        Log($"ERROR: {message}");
        if (ex != null)
            Log($"  Exception: {ex.GetType().Name}: {ex.Message}\n  StackTrace: {ex.StackTrace}");
    }

    public static string LogPath => _logPath;
}
